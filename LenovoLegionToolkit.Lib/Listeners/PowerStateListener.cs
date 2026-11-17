using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Features.Hybrid.Notify;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Power;
using Windows.Win32.UI.WindowsAndMessaging;

namespace LenovoLegionToolkit.Lib.Listeners;

/// <summary>
/// 电源状态监听器：综合监听 <see cref="SystemEvents.PowerModeChanged"/> 与
/// PowerRegisterSuspendResumeNotification，捕获睡眠/唤醒/开机/电源适配器插拔等事件，
/// 并在唤醒或接入电源后重新应用电池模式、灯效归属、性能模式与独显通知等依赖状态。
/// </summary>
public class PowerStateListener : IListener<PowerStateListener.ChangedEventArgs>
{
    /// <summary>
    /// 电源状态变更事件参数。
    /// </summary>
    public class ChangedEventArgs(PowerStateEvent powerStateEvent, bool powerAdapterStateChanged) : EventArgs
    {
        /// <summary>
        /// 电源状态事件类型。
        /// </summary>
        public PowerStateEvent PowerStateEvent { get; } = powerStateEvent;

        /// <summary>
        /// 本次事件中电源适配器连接状态是否发生变化。
        /// </summary>
        public bool PowerAdapterStateChanged { get; } = powerAdapterStateChanged;
    }

    private readonly SafeHandle _recipientHandle;
    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    // 回调委托必须在字段中长期持有，否则会被 GC 回收导致原生侧回调野指针
    private readonly PDEVICE_NOTIFY_CALLBACK_ROUTINE _callback;

    private readonly PowerModeFeature _powerModeFeature;
    private readonly BatteryFeature _batteryFeature;
    private readonly DGPUNotify _dgpuNotify;
    private readonly RGBKeyboardBacklightController _rgbController;

    private bool _started;
    private HPOWERNOTIFY _handle;
    private PowerAdapterStatus? _lastPowerAdapterState;

    /// <summary>
    /// 电源状态变更事件。在唤醒、开机及（非睡眠状态下的）其他电源事件处理后触发。
    /// </summary>
    public event EventHandler<ChangedEventArgs>? Changed;

    /// <summary>
    /// 初始化电源状态监听器。
    /// </summary>
    /// <param name="powerModeFeature">性能模式功能，用于唤醒后校正 Windows 电源设置。</param>
    /// <param name="batteryFeature">电池功能，用于唤醒后校正电池模式。</param>
    /// <param name="dgpuNotify">独显通知器，用于唤醒后重新通知独显。</param>
    /// <param name="rgbController">RGB 键盘控制器，用于唤醒后恢复灯效归属。</param>
    public unsafe PowerStateListener(PowerModeFeature powerModeFeature, BatteryFeature batteryFeature, DGPUNotify dgpuNotify, RGBKeyboardBacklightController rgbController)
    {
        _powerModeFeature = powerModeFeature;
        _batteryFeature = batteryFeature;
        _dgpuNotify = dgpuNotify;
        _rgbController = rgbController;

        // 把回调函数绑定到接收者结构体，供 PowerRegisterSuspendResumeNotification 回调
        _callback = Callback;
        _recipientHandle = new StructSafeHandle<DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS>(new DEVICE_NOTIFY_SUBSCRIBE_PARAMETERS
        {
            Callback = _callback,
            Context = null,
        });
    }

    /// <inheritdoc/>
    public async Task StartAsync()
    {
        if (_started)
            return;

        // 记录启动时的适配器连接状态，用于后续判断插拔是否真实发生变化
        _lastPowerAdapterState = await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false);

        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
        RegisterSuspendResumeNotification();

        _started = true;
    }

    /// <inheritdoc/>
    public Task StopAsync()
    {
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
        UnRegisterSuspendResumeNotification();

        _started = false;

        return Task.CompletedTask;
    }

    private async void SystemEvents_PowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Event received: {e.Mode}");

        var powerMode = e.Mode switch
        {
            PowerModes.StatusChange => PowerStateEvent.StatusChange,
            PowerModes.Resume => PowerStateEvent.Resume,
            PowerModes.Suspend => PowerStateEvent.Suspend,
            _ => PowerStateEvent.Unknown
        };

        if (powerMode is PowerStateEvent.Unknown)
            return;

        await HandleAsync(powerMode).ConfigureAwait(false);
    }

    // 原生回调要求立即返回，因此把实际处理放到线程池，避免阻塞电源管理线程
    private unsafe uint Callback(void* context, uint type, void* setting)
    {
        _ = Task.Run(() => CallbackAsync(type));
        return (uint)WIN32_ERROR.ERROR_SUCCESS;
    }

    private async Task CallbackAsync(uint type)
    {
        // 仅在支持 Always On AC 的机型上处理该回调，其余机型保持原有 PowerModeChanged 行为
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        if (!mi.Properties.SupportsAlwaysOnAc.status)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Ignoring, AO AC not enabled...");

            return;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Event value: {type}");

        var powerMode = type switch
        {
            PInvoke.PBT_APMRESUMEAUTOMATIC => PowerStateEvent.Resume,
            _ => PowerStateEvent.Unknown
        };

        if (powerMode is not PowerStateEvent.Resume)
            return;

        await HandleAsync(powerMode).ConfigureAwait(false);
    }

    private async Task HandleAsync(PowerStateEvent powerStateEvent)
    {
        var powerAdapterState = await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Handle {powerStateEvent}. [newState={powerAdapterState}]");

        if (powerStateEvent is PowerStateEvent.Resume)
        {
            // 唤醒后驱动与固件状态可能被重置，异步重新应用各项设置；放后台执行避免阻塞事件回调
            _ = Task.Run(async () =>
            {
                try
                {
                    if (await _batteryFeature.IsSupportedAsync().ConfigureAwait(false))
                        await _batteryFeature.EnsureCorrectBatteryModeIsSetAsync().ConfigureAwait(false);

                    if (await _rgbController.IsSupportedAsync().ConfigureAwait(false))
                        await _rgbController.SetLightControlOwnerAsync(true, true).ConfigureAwait(false);

                    if (await _powerModeFeature.IsSupportedAsync().ConfigureAwait(false))
                    {
                        await _powerModeFeature.EnsureCorrectWindowsPowerSettingsAreSetAsync().ConfigureAwait(false);
                        await _powerModeFeature.EnsureGodModeStateIsAppliedAsync().ConfigureAwait(false);
                    }

                    if (await _dgpuNotify.IsSupportedAsync().ConfigureAwait(false))
                    {
                        // 唤醒后显卡驱动就绪需要时间，延迟 5 秒再发起独显切换通知
                        await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                        await _dgpuNotify.NotifyAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    // 后台恢复任务失败时仅记录日志，避免未观察异常
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Failed to re-apply settings after resume.", ex);
                }
            });
        }

        if (powerStateEvent is PowerStateEvent.StatusChange && powerAdapterState is PowerAdapterStatus.Connected)
        {
            // 接入电源时重新应用 God Mode 并通知独显，确保性能状态与供电状态一致
            _ = Task.Run(async () =>
            {
                try
                {
                    if (await _powerModeFeature.IsSupportedAsync().ConfigureAwait(false))
                        await _powerModeFeature.EnsureGodModeStateIsAppliedAsync().ConfigureAwait(false);

                    if (await _dgpuNotify.IsSupportedAsync().ConfigureAwait(false))
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                        await _dgpuNotify.NotifyAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    // 后台恢复任务失败时仅记录日志，避免未观察异常
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Failed to re-apply settings after power status change.", ex);
                }
            });
        }

        // 对比上次状态以判断适配器是否真正插拔，避免重复通知
        var powerAdapterStateChanged = powerAdapterState != _lastPowerAdapterState;
        _lastPowerAdapterState = powerAdapterState;

        if (powerStateEvent is PowerStateEvent.Suspend or PowerStateEvent.Unknown)
            return;

        if (powerAdapterStateChanged)
            Notify(powerAdapterState);

        Changed?.Invoke(this, new(powerStateEvent, powerAdapterStateChanged));
    }

    /// <summary>
    /// 注册睡眠/恢复通知（回调方式），用于捕捉 <see cref="SystemEvents.PowerModeChanged"/> 可能遗漏的恢复事件。
    /// </summary>
    private unsafe void RegisterSuspendResumeNotification()
    {
        _handle = PInvoke.PowerRegisterSuspendResumeNotification(REGISTER_NOTIFICATION_FLAGS.DEVICE_NOTIFY_CALLBACK, _recipientHandle, out var handle) == WIN32_ERROR.ERROR_SUCCESS
            ? new HPOWERNOTIFY(new IntPtr(handle))
            : HPOWERNOTIFY.Null;
    }

    /// <summary>
    /// 注销睡眠/恢复通知。
    /// </summary>
    private void UnRegisterSuspendResumeNotification()
    {
        PInvoke.PowerUnregisterSuspendResumeNotification(_handle);
        _handle = HPOWERNOTIFY.Null;
    }

    /// <summary>
    /// 根据适配器状态发布插拔通知气泡。
    /// </summary>
    /// <param name="newState">新的适配器状态。</param>
    private static void Notify(PowerAdapterStatus newState)
    {
        switch (newState)
        {
            case PowerAdapterStatus.Connected:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.ACAdapterConnected));
                break;
            case PowerAdapterStatus.ConnectedLowWattage:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.ACAdapterConnectedLowWattage));
                break;
            case PowerAdapterStatus.Disconnected:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.ACAdapterDisconnected));
                break;
        }
    }
}

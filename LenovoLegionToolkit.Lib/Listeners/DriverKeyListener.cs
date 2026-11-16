using System;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Features.WhiteKeyboardBacklight;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using Windows.Win32;

namespace LenovoLegionToolkit.Lib.Listeners;

/// <summary>
/// 联想驱动功能键监听器：通过 Lenovo Energy Management 驱动的 IOCTL 接口阻塞等待 Fn 组合键
/// （如 Fn+F4 麦克风、Fn+F8 飞行模式、Fn+F10 触控板、Fn+Space 键盘背光），
/// 按键触发后执行对应功能切换并发布通知消息。
/// </summary>
public class DriverKeyListener(
    FnKeysDisabler fnKeysDisabler,
    MicrophoneFeature microphoneFeature,
    TouchpadLockFeature touchpadLockFeature,
    WhiteKeyboardBacklightFeature whiteKeyboardBacklightFeature)
    : IListener<DriverKeyListener.ChangedEventArgs>
{
    /// <summary>
    /// 功能键按下事件参数。
    /// </summary>
    public class ChangedEventArgs(DriverKey driverKey) : EventArgs
    {
        /// <summary>
        /// 按下的功能键。
        /// </summary>
        public DriverKey DriverKey { get; } = driverKey;
    }

    /// <summary>
    /// 功能键按下事件。当 Energy Management 驱动报告按键且 FnKeys 未被本工具接管时触发。
    /// </summary>
    public event EventHandler<ChangedEventArgs>? Changed;

    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _listenTask;

    /// <inheritdoc/>
    public Task StartAsync()
    {
        // 幂等启动：后台监听任务已在运行则直接返回
        if (_listenTask is not null)
            return Task.CompletedTask;

        _cancellationTokenSource = new();
        _listenTask = Task.Run(() => HandlerAsync(_cancellationTokenSource.Token));

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        // 先取消令牌唤醒阻塞中的监听循环，再等待任务真正退出，确保下次可安全重启
        if (_cancellationTokenSource is not null)
            await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);

        _cancellationTokenSource = null;

        if (_listenTask is not null)
            await _listenTask;

        _listenTask = null;
    }

    /// <summary>
    /// 监听主循环：绑定等待句柄到驱动后循环阻塞，收到按键事件时读取键值并分发处理。
    /// </summary>
    /// <param name="token">用于在停止监听时取消阻塞等待的取消令牌。</param>
    private async Task HandlerAsync(CancellationToken token)
    {
        try
        {
            var resetEvent = new ManualResetEvent(false);
            // 将 resetEvent 的内核句柄注册给 Energy 驱动，驱动在有按键时会触发该句柄
            var setHandleResult = BindListener(resetEvent);
            if (!setHandleResult)
                PInvokeExtensions.ThrowIfWin32Error("DeviceIoControl, setHandleResult");

            GetValue(out _); // Clear register

            while (true)
            {
                // 同时等待驱动按键句柄与取消令牌：按键到达或停止监听时唤醒
                WaitHandle.WaitAny([resetEvent, token.WaitHandle]);

                token.ThrowIfCancellationRequested();

                // 若 Lenovo FnKeys 软件在运行（未被禁用），按键由其自行处理，本监听器让出
                if (await fnKeysDisabler.GetStatusAsync().ConfigureAwait(false) == SoftwareStatus.Enabled)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Ignoring, FnKeys are enabled.");

                    resetEvent.Reset();
                    continue;
                }

                var getValueResult = GetValue(out var value);
                if (!getValueResult)
                    PInvokeExtensions.ThrowIfWin32Error("DeviceIoControl, getValueResult");

                var key = (DriverKey)value;
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Event received. [key={key}, value={value}]");

                await OnChangedAsync(key).ConfigureAwait(false);
                Changed?.Invoke(this, new(key));

                resetEvent.Reset();
            }
        }
        // 取消与线程中止属于正常停止路径，静默处理；其余异常仅记录，监听线程随之结束
        catch (OperationCanceledException) { }
        catch (ThreadAbortException) { }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Unknown error.", ex);
        }
    }

    /// <summary>
    /// 按键的具体业务处理：切换麦克风、打开飞行模式、切换触控板、循环键盘背光，并发布对应通知。
    /// </summary>
    /// <param name="value">按下的功能键。</param>
    private async Task OnChangedAsync(DriverKey value)
    {
        try
        {
            if (value.HasFlag(DriverKey.FnF4))
            {
                if (await microphoneFeature.IsSupportedAsync().ConfigureAwait(false))
                {
                    switch (await microphoneFeature.GetStateAsync().ConfigureAwait(false))
                    {
                        case MicrophoneState.On:
                            await microphoneFeature.SetStateAsync(MicrophoneState.Off).ConfigureAwait(false);
                            MessagingCenter.Publish(new NotificationMessage(NotificationType.MicrophoneOff));
                            break;
                        case MicrophoneState.Off:
                            await microphoneFeature.SetStateAsync(MicrophoneState.On).ConfigureAwait(false);
                            MessagingCenter.Publish(new NotificationMessage(NotificationType.MicrophoneOn));
                            break;
                    }
                }
            }

            if (value.HasFlag(DriverKey.FnF8))
                AirplaneMode.Open();

            if (value.HasFlag(DriverKey.FnF10))
            {
                if (await touchpadLockFeature.IsSupportedAsync().ConfigureAwait(false))
                {
                    var status = await touchpadLockFeature.GetStateAsync().ConfigureAwait(false);
                    MessagingCenter.Publish(status == TouchpadLockState.Off
                        ? new NotificationMessage(NotificationType.TouchpadOn)
                        : new NotificationMessage(NotificationType.TouchpadOff));
                }
            }

            if (value.HasFlag(DriverKey.FnSpace))
            {
                if (await whiteKeyboardBacklightFeature.IsSupportedAsync().ConfigureAwait(false))
                {
                    var state = await whiteKeyboardBacklightFeature.GetStateAsync().ConfigureAwait(false);
                    MessagingCenter.Publish(state == WhiteKeyboardBacklightState.Off
                        ? new NotificationMessage(NotificationType.WhiteKeyboardBacklightOff, state.GetDisplayName())
                        : new NotificationMessage(NotificationType.WhiteKeyboardBacklightChanged, state.GetDisplayName()));
                }
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't handle key press. [value={value}]", ex);
        }
    }

    /// <summary>
    /// 把等待句柄（内核事件对象）的句柄传给 Energy 驱动，建立按键通知通道。
    /// </summary>
    /// <param name="waitHandle">要在按键到达时被驱动的信号置位的等待句柄。</param>
    /// <returns>DeviceIoControl 调用是否成功。</returns>
    private static unsafe bool BindListener(WaitHandle waitHandle)
    {
        var handle = (uint)waitHandle.SafeWaitHandle.DangerousGetHandle();
        return PInvoke.DeviceIoControl(Drivers.GetEnergy(),
            Drivers.IOCTL_KEY_WAIT_HANDLE,
            &handle,
            16,
            null,
            0,
            null,
            null);
    }

    /// <summary>
    /// 通过 IOCTL 从驱动读取最近一次按键值（读取同时清除寄存器），避免同一按键被重复处理。
    /// </summary>
    /// <param name="value">读取到的按键原始值。</param>
    /// <returns>DeviceIoControl 调用是否成功。</returns>
    private static unsafe bool GetValue(out uint value)
    {
        uint inBuff = 0;
        uint outBuff = 0;
        var result = PInvoke.DeviceIoControl(Drivers.GetEnergy(),
            Drivers.IOCTL_KEY_VALUE,
            &inBuff,
            4,
            &outBuff,
            4,
            null,
            null);
        value = outBuff;
        return result;
    }
}

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Features.Hybrid.Notify;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Power;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace LenovoLegionToolkit.Lib.Listeners;

/// <summary>
/// 原生 Windows 消息监听器：创建一个隐藏的消息窗口，统一接收设备插拔、显示器开关、
/// 盖子开合、电源事件以及低级键盘钩子消息，并将其翻译为内部事件。
/// </summary>
public class NativeWindowsMessageListener : NativeWindow, IListener<NativeWindowsMessageListener.ChangedEventArgs>
{
    /// <summary>
    /// 原生消息事件参数。
    /// </summary>
    public class ChangedEventArgs(NativeWindowsMessage message, object? data = null) : EventArgs
    {
        /// <summary>
        /// 转换后的消息类型。
        /// </summary>
        public NativeWindowsMessage Message { get; } = message;

        /// <summary>
        /// 附加数据（如设备实例 ID），无附加数据时为 null。
        /// </summary>
        public object? Data { get; } = data;
    }

    private readonly IMainThreadDispatcher _mainThreadDispatcher;
    private readonly DGPUNotify _dgpuNotify;
    private readonly SmartFnLockController _smartFnLockController;
    private readonly PowerModeFeature _powerModeFeature;

    private readonly HOOKPROC _kbProc;

    private readonly TaskCompletionSource _isMonitorOnTaskCompletionSource = new();
    private readonly TaskCompletionSource _isLidOpenTaskCompletionSource = new();

    private HDEVNOTIFY _deviceNotificationHandle;
    private HPOWERNOTIFY _consoleDisplayStateNotificationHandle;
    private HPOWERNOTIFY _lidSwitchStateChangeNotificationHandle;
    private HPOWERNOTIFY _powerSavingStateChangeNotificationHandle;
    private HHOOK _kbHook;

    /// <summary>
    /// 当前显示器是否处于点亮状态。
    /// </summary>
    public bool IsMonitorOn { get; private set; }

    /// <summary>
    /// 当前笔记本盖子是否处于打开状态。
    /// </summary>
    public bool IsLidOpen { get; private set; }

    /// <summary>
    /// 原生消息事件。收到设备插拔、显示器开关、盖子开合、节能模式开启等消息时触发。
    /// </summary>
    public event EventHandler<ChangedEventArgs>? Changed;

    /// <summary>
    /// 初始化原生消息监听器。
    /// </summary>
    /// <param name="mainThreadDispatcher">主线程调度器，消息窗口的创建/销毁必须在 UI 线程执行。</param>
    /// <param name="dgpuNotify">独显通知器，显示器设备到达时用于触发通知。</param>
    /// <param name="smartFnLockController">智能 Fn 锁控制器，处理键盘钩子中的按键事件。</param>
    /// <param name="powerModeFeature">性能模式功能，节能模式开启时用于校正电源设置。</param>
    public NativeWindowsMessageListener(IMainThreadDispatcher mainThreadDispatcher, DGPUNotify dgpuNotify, SmartFnLockController smartFnLockController, PowerModeFeature powerModeFeature)
    {
        _mainThreadDispatcher = mainThreadDispatcher;
        _dgpuNotify = dgpuNotify;
        _smartFnLockController = smartFnLockController;
        _powerModeFeature = powerModeFeature;

        // 需提前缓存委托实例，SetWindowsHookEx 依赖该委托在钩子存续期间不被回收
        _kbProc = LowLevelKeyboardProc;
    }

    /// <summary>
    /// 关闭显示器：延时 1 秒后在消息窗口线程发送 SC_MONITORPOWER 系统命令（参数 2 表示关闭）。
    /// </summary>
    public async Task TurnOffMonitorAsync()
    {
        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        await _mainThreadDispatcher.DispatchAsync(() =>
        {
            PInvoke.SendMessage(new HWND(Handle), PInvoke.WM_SYSCOMMAND, new WPARAM(PInvoke.SC_MONITORPOWER), new LPARAM(2));
            return Task.CompletedTask;
        }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task StartAsync() => _mainThreadDispatcher.DispatchAsync(() =>
    {
        // 创建消息窗口：Parent = -3 (HWND_MESSAGE) 表示仅用于接收消息的消息专用窗口
        CreateHandle(new CreateParams
        {
            Caption = "LenovoLegionToolkit_MessageWindow",
            Parent = new IntPtr(-3)
        });

        // 安装低级键盘钩子，用于检测 CapsLock/NumLock 与智能 Fn 锁
        _kbHook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _kbProc, HINSTANCE.Null, 0);

        // 分别注册设备接口通知与电源设置通知，后续通过 WndProc 接收对应消息
        _deviceNotificationHandle = RegisterDeviceNotification(Handle);
        _consoleDisplayStateNotificationHandle = RegisterPowerNotification(PInvoke.GUID_CONSOLE_DISPLAY_STATE);
        _lidSwitchStateChangeNotificationHandle = RegisterPowerNotification(PInvoke.GUID_LIDSWITCH_STATE_CHANGE);
        _powerSavingStateChangeNotificationHandle = RegisterPowerNotification(PInvoke.GUID_POWER_SAVING_STATUS);

        return WaitForInit();
    });

    /// <inheritdoc/>
    public Task StopAsync() => _mainThreadDispatcher.DispatchAsync(() =>
    {
        // 按注册的逆序注销所有原生通知，避免句柄泄漏
        PInvoke.UnhookWindowsHookEx(_kbHook);

        PInvoke.UnregisterDeviceNotification(_deviceNotificationHandle);
        PInvoke.UnregisterPowerSettingNotification(_consoleDisplayStateNotificationHandle);
        PInvoke.UnregisterPowerSettingNotification(_lidSwitchStateChangeNotificationHandle);
        PInvoke.UnregisterPowerSettingNotification(_powerSavingStateChangeNotificationHandle);

        _kbHook = default;
        _deviceNotificationHandle = default;
        _consoleDisplayStateNotificationHandle = default;

        ReleaseHandle();

        return Task.CompletedTask;
    });

    /// <summary>
    /// 消息窗口过程：负责解析 WM_DEVICECHANGE 与 WM_POWERBROADCAST，并分发到对应的处理方法。
    /// </summary>
    /// <param name="m">窗口消息。</param>
    protected override unsafe void WndProc(ref Message m)
    {
        if (m.Msg == PInvoke.WM_DEVICECHANGE && m.LParam != IntPtr.Zero)
        {
            // 将 lParam 解释为设备广播头，只有设备接口类型才继续解析具体接口信息
            ref var devBroadcastHdr = ref Unsafe.AsRef<DEV_BROADCAST_HDR>((void*)m.LParam);
            if (devBroadcastHdr.dbch_devicetype == DEV_BROADCAST_HDR_DEVICE_TYPE.DBT_DEVTYP_DEVICEINTERFACE)
            {
                ref var devBroadcastDeviceInterface = ref Unsafe.AsRef<DEV_BROADCAST_DEVICEINTERFACE_W>((void*)m.LParam);
                // dbcc_name 是柔性数组，按结构体总长度减去固定头部长度计算字符数（宽字符）
                var length = ((int)devBroadcastDeviceInterface.dbcc_size - sizeof(DEV_BROADCAST_DEVICEINTERFACE_W)) / sizeof(char);
                var name = devBroadcastDeviceInterface.dbcc_name.AsSpan(length).ToString();

                var state = (uint)m.WParam.ToInt32();
                switch (state)
                {
                    case PInvoke.DBT_DEVICEARRIVAL:
                        {
                            if (Log.Instance.IsTraceEnabled)
                                Log.Instance.Trace($"Event received: Device Arrival [name={name}]");

                            OnDeviceConnected(name);
                            break;
                        }
                    case PInvoke.DBT_DEVICEREMOVECOMPLETE:
                        {
                            if (Log.Instance.IsTraceEnabled)
                                Log.Instance.Trace($"Event received: Device Removal Complete [name={name}]");

                            OnDeviceDisconnected(name);
                            break;
                        }
                }

                if (devBroadcastDeviceInterface.dbcc_classguid == PInvoke.GUID_DISPLAY_DEVICE_ARRIVAL)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Event received: Display Device Arrival");

                    OnDisplayDeviceArrival();
                }

                if (devBroadcastDeviceInterface.dbcc_classguid == PInvoke.GUID_DEVINTERFACE_MONITOR)
                {
                    var id = InternalDisplay.Get();
                    var isExternal = !name.Equals(id?.DevicePath, StringComparison.Ordinal);

                    switch (state)
                    {
                        case PInvoke.DBT_DEVICEARRIVAL:
                            {
                                if (Log.Instance.IsTraceEnabled)
                                    Log.Instance.Trace($"Event received: Monitor Connected");

                                OnMonitorConnected(isExternal);
                                break;
                            }
                        case PInvoke.DBT_DEVICEREMOVECOMPLETE:
                            {
                                if (Log.Instance.IsTraceEnabled)
                                    Log.Instance.Trace($"Event received: Monitor Disconnected");

                                OnMonitorDisconnected(isExternal);
                                break;
                            }
                    }
                }
            }
        }

        if (m.Msg == PInvoke.WM_POWERBROADCAST && m.WParam == (IntPtr)PInvoke.PBT_POWERSETTINGCHANGE && m.LParam != IntPtr.Zero)
        {
            // lParam 指向 POWERBROADCAST_SETTING，按 PowerSetting GUID 区分显示器/盖子/节能等不同电源事件
            ref var str = ref Unsafe.AsRef<POWERBROADCAST_SETTING>((void*)m.LParam);

            if (str.PowerSetting == PInvoke.GUID_CONSOLE_DISPLAY_STATE)
            {
                var state = (PInvokeExtensions.CONSOLE_DISPLAY_STATE)str.Data[0];
                switch (state)
                {
                    case PInvokeExtensions.CONSOLE_DISPLAY_STATE.On:
                        {
                            if (Log.Instance.IsTraceEnabled)
                                Log.Instance.Trace($"Event received: Monitor On");

                            OnMonitorOn();
                            break;
                        }
                    case PInvokeExtensions.CONSOLE_DISPLAY_STATE.Off:
                        {
                            if (Log.Instance.IsTraceEnabled)
                                Log.Instance.Trace($"Event received: Monitor Off");

                            OnMonitorOff();
                            break;
                        }
                }
            }

            if (str.PowerSetting == PInvoke.GUID_LIDSWITCH_STATE_CHANGE)
            {
                var isOpened = str.Data[0] != 0;
                if (isOpened)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Event received: Lid Opened");

                    OnLidOpened();
                }
                else
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Event received: Lid Closed");

                    OnLidClosed();
                }
            }

            if (str.PowerSetting == PInvoke.GUID_POWER_SAVING_STATUS && str.Data[0] == 0)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Event received: Battery Saver enabled");

                OnBatterySaverEnabled();
            }
        }

        base.WndProc(ref m);
    }

    /// <summary>
    /// 等待显示器与盖子状态首次上报完成，最多等待 3 秒；超时则记录警告，避免阻塞启动流程。
    /// </summary>
    private async Task WaitForInit()
    {
        var delayTask = Task.Delay(TimeSpan.FromSeconds(3));
        var task = Task.WhenAll(
            _isMonitorOnTaskCompletionSource.Task,
            _isLidOpenTaskCompletionSource.Task
        );

        var completed = await Task.WhenAny(task, delayTask).ConfigureAwait(false);

        if (completed == delayTask)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Delay expired, state might be inconsistent! [IsMonitorOn={IsMonitorOn}, IsLidOpen={IsLidOpen}]");
        }
    }

    private void OnMonitorOn()
    {
        IsMonitorOn = true;
        _isMonitorOnTaskCompletionSource.TrySetResult();

        RaiseChanged(NativeWindowsMessage.MonitorOn);
    }

    private void OnMonitorOff()
    {
        IsMonitorOn = false;
        _isMonitorOnTaskCompletionSource.TrySetResult();

        RaiseChanged(NativeWindowsMessage.MonitorOff);
    }

    private void OnLidOpened()
    {
        IsLidOpen = true;
        _isLidOpenTaskCompletionSource.TrySetResult();

        RaiseChanged(NativeWindowsMessage.LidOpened);
    }

    private void OnLidClosed()
    {
        IsLidOpen = false;
        _isLidOpenTaskCompletionSource.TrySetResult();

        RaiseChanged(NativeWindowsMessage.LidClosed);
    }

    private void OnBatterySaverEnabled()
    {
        Task.Run(_powerModeFeature.EnsureCorrectWindowsPowerSettingsAreSetAsync);

        RaiseChanged(NativeWindowsMessage.BatterySaverEnabled);
    }

    private void OnDeviceConnected(string name)
    {
        RaiseChanged(NativeWindowsMessage.DeviceConnected, ConvertDeviceNameToDeviceInstanceId(name));
    }

    private void OnDeviceDisconnected(string name)
    {
        RaiseChanged(NativeWindowsMessage.DeviceDisconnected, ConvertDeviceNameToDeviceInstanceId(name));
    }

    private void OnMonitorConnected(bool isExternal)
    {
        RaiseChanged(NativeWindowsMessage.MonitorConnected);

        if (isExternal)
            RaiseChanged(NativeWindowsMessage.ExternalMonitorConnected);
    }

    private void OnMonitorDisconnected(bool isExternal)
    {
        RaiseChanged(NativeWindowsMessage.MonitorDisconnected);

        if (isExternal)
            RaiseChanged(NativeWindowsMessage.ExternalMonitorDisconnected);
    }

    private void OnDisplayDeviceArrival()
    {
        Task.Run(async () =>
        {
            if (await _dgpuNotify.IsSupportedAsync().ConfigureAwait(false))
                await _dgpuNotify.NotifyAsync().ConfigureAwait(false);
        });

        RaiseChanged(NativeWindowsMessage.OnDisplayDeviceArrival);
    }

    private void RaiseChanged(NativeWindowsMessage message, object? data = null) => Changed?.Invoke(this, new ChangedEventArgs(message, data));

    /// <summary>
    /// 低级键盘钩子回调：把按键交给智能 Fn 锁处理，并在按键抬起时检测 CapsLock/NumLock 状态并发布通知。
    /// </summary>
    /// <param name="nCode">钩子代码，小于 0 时应直接传递给下一个钩子。</param>
    /// <param name="wParam">按键消息类型（按下/抬起等）。</param>
    /// <param name="lParam">指向 <see cref="KBDLLHOOKSTRUCT"/> 的指针。</param>
    /// <returns>钩子处理结果。</returns>
    private unsafe LRESULT LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode != PInvoke.HC_ACTION)
            return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);

        ref var kbStruct = ref Unsafe.AsRef<KBDLLHOOKSTRUCT>((void*)lParam.Value);

        _smartFnLockController.OnKeyboardEvent(wParam.Value, kbStruct);

        if (wParam.Value != PInvoke.WM_KEYUP)
            return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);

        if (kbStruct.vkCode == (ulong)VIRTUAL_KEY.VK_CAPITAL)
        {
            var isOn = (PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_CAPITAL) & 0x1) != 0;
            var type = isOn ? NotificationType.CapsLockOn : NotificationType.CapsLockOff;
            MessagingCenter.Publish(new NotificationMessage(type));
        }

        if (kbStruct.vkCode == (ulong)VIRTUAL_KEY.VK_NUMLOCK)
        {
            var isOn = (PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_NUMLOCK) & 0x1) != 0;
            var type = isOn ? NotificationType.NumLockOn : NotificationType.NumLockOff;
            MessagingCenter.Publish(new NotificationMessage(type));
        }

        return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);
    }

    /// <summary>
    /// 注册设备接口变更通知，接收所有接口类别的设备到达/移除消息。
    /// </summary>
    /// <param name="handle">接收消息的窗口句柄。</param>
    /// <returns>设备通知句柄，用于后续注销。</returns>
    private static unsafe HDEVNOTIFY RegisterDeviceNotification(IntPtr handle)
    {
        var ptr = IntPtr.Zero;
        try
        {
            var str = new DEV_BROADCAST_DEVICEINTERFACE_W();
            str.dbcc_size = (uint)Marshal.SizeOf(str);
            str.dbcc_devicetype = (uint)DEV_BROADCAST_HDR_DEVICE_TYPE.DBT_DEVTYP_DEVICEINTERFACE;
            ptr = Marshal.AllocHGlobal(Marshal.SizeOf(str));
            Marshal.StructureToPtr(str, ptr, true);
            return PInvoke.RegisterDeviceNotification(new HANDLE(handle),
                ptr.ToPointer(),
                REGISTER_NOTIFICATION_FLAGS.DEVICE_NOTIFY_WINDOW_HANDLE | REGISTER_NOTIFICATION_FLAGS.DEVICE_NOTIFY_ALL_INTERFACE_CLASSES);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    /// <summary>
    /// 注册指定 GUID 的电源设置变更通知。
    /// </summary>
    /// <param name="guid">电源设置 GUID（如显示器状态、盖子状态、节能状态）。</param>
    /// <returns>电源通知句柄，用于后续注销。</returns>
    private unsafe HPOWERNOTIFY RegisterPowerNotification(Guid guid)
    {
        return PInvoke.RegisterPowerSettingNotification(new HANDLE(Handle), &guid, 0);
    }

    /// <summary>
    /// 将设备接口路径（形如 \\?\USB#VID_xxxx#...#{guid}）转换为标准设备实例 ID；
    /// 格式不符时返回 null。
    /// </summary>
    /// <param name="name">设备接口路径。</param>
    /// <returns>大写的设备实例 ID，无法解析时为 null。</returns>
    private static string? ConvertDeviceNameToDeviceInstanceId(string name)
    {
        var parts = name.Split('#');
        if (parts.Length < 3)
            return null;

        var part1 = parts[0].TrimStart('\\', '?');
        var part2 = parts[1].Replace('#', '\\');
        var part3 = parts[2];
        return $@"{part1}\{part2}\{part3}".ToUpperInvariant();
    }
}

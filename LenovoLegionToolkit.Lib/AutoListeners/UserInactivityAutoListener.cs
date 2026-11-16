using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using LenovoLegionToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Timer = System.Threading.Timer;

namespace LenovoLegionToolkit.Lib.AutoListeners;

/// <summary>
/// 用户空闲监听器：通过全局键鼠钩子 + 定时器累计"无操作时长"，并在用户恢复操作时上报。
/// 时间为粗粒度估算：每 <see cref="_timerResolution"/> 增加一个计数，用户有任何输入即清零。
/// </summary>
public class UserInactivityAutoListener(IMainThreadDispatcher mainThreadDispatcher)
    : AbstractAutoListener<UserInactivityAutoListener.ChangedEventArgs>
{
    /// <summary>
    /// 空闲状态变化事件参数。
    /// </summary>
    public class ChangedEventArgs(TimeSpan timerResolution, uint tickCount) : EventArgs
    {
        public TimeSpan TimerResolution { get; } = timerResolution; // 单个计数代表的时间片

        public uint TickCount { get; } = tickCount; // 连续空闲的时间片数量，0 表示刚恢复活跃
    }

    /// <summary>
    /// 隐藏消息窗口，承载全局键鼠钩子（低级钩子需要消息循环，故依附于一个窗口）。
    /// </summary>
    private class UserInactivityWindow : NativeWindow, IDisposable
    {
        private readonly Action _callback;

        private readonly HOOKPROC _hookProc;

        private HHOOK _kbHook;
        private HHOOK _mouseHook;

        public UserInactivityWindow(Action callback)
        {
            _callback = callback;

            // 保持委托实例引用，防止被 GC 回收导致钩子回调崩溃
            _hookProc = HookProc;
        }

        public void Create()
        {
            CreateHandle(new CreateParams
            {
                Caption = "LenovoLegionToolkit_UserInactivityListenerWindow",
                Parent = new IntPtr(-3) // HWND_MESSAGE (-3)：创建仅接收消息的消息窗口，不显示
            });

            // 低级键盘/鼠标钩子，用于感知用户输入
            _kbHook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _hookProc, HINSTANCE.Null, 0);
            _mouseHook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_MOUSE_LL, _hookProc, HINSTANCE.Null, 0);
        }

        private LRESULT HookProc(int nCode, WPARAM wParam, LPARAM lParam)
        {
            _callback();
            // 必须调用下一个钩子，否则会阻塞系统的输入处理
            return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            PInvoke.UnhookWindowsHookEx(_kbHook);
            PInvoke.UnhookWindowsHookEx(_mouseHook);

            _kbHook = HHOOK.Null;
            _mouseHook = HHOOK.Null;

            ReleaseHandle();

            GC.SuppressFinalize(this);
        }
    }

    private readonly TimeSpan _timerResolution = TimeSpan.FromSeconds(10); // 空闲计时精度
    private readonly object _lock = new();

    private UserInactivityWindow? _window;
    private uint _tickCount;
    private Timer? _timer;

    /// <summary>
    /// 当前连续空闲时长 = 计时精度 × 已累计的时间片数。
    /// </summary>
    public TimeSpan InactivityTimeSpan => _timerResolution * _tickCount;

    // 注意：钩子与窗口必须在 UI 线程创建，故统一切换到主线程执行
    protected override Task StartAsync() => mainThreadDispatcher.DispatchAsync(() =>
    {
        lock (_lock)
        {
            _timer = new Timer(TimerCallback, null, _timerResolution, _timerResolution);
            _tickCount = 0;

            var window = new UserInactivityWindow(WindowCallback);
            window.Create();
            _window = window;
        }

        return Task.CompletedTask;
    });

    protected override Task StopAsync() => mainThreadDispatcher.DispatchAsync(() =>
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            _tickCount = 0;

            _window?.Dispose();
            _window = null;
        }

        return Task.CompletedTask;
    });

    // 有任意键鼠输入：重置计时器，若此前处于空闲则上报"恢复活跃"
    private void WindowCallback()
    {
        lock (_lock)
        {
            _timer?.Change(_timerResolution, _timerResolution);

            if (_tickCount < 1)
                return;

            _tickCount = 0;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"User became active.");

            RaiseChanged(new ChangedEventArgs(_timerResolution, 0));
        }
    }

    // 定时器到时：无输入则累计空闲时间片并上报当前空闲时长
    private void TimerCallback(object? state)
    {
        lock (_lock)
        {
            _tickCount++;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"User is not active [time={_timerResolution * _tickCount}]");

            RaiseChanged(new ChangedEventArgs(_timerResolution, _tickCount));
        }
    }
}

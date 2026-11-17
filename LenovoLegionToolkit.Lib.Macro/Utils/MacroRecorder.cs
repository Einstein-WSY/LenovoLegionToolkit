using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace LenovoLegionToolkit.Lib.Macro.Utils;

/// <summary>
/// 宏录制器：通过全局低级键盘/鼠标钩子捕获输入，并转换为带时间戳的 <see cref="MacroEvent"/> 序列。
/// </summary>
internal class MacroRecorder
{
    /// <summary>
    /// 录制到单个事件时的事件参数。
    /// </summary>
    public class ReceivedEventArgs : EventArgs
    {
        /// <summary>本次录制到的事件。</summary>
        public MacroEvent MacroEvent { get; init; }
    }

    /// <summary>
    /// 录制停止时的事件参数。
    /// </summary>
    public class StoppedEventArgs : EventArgs
    {
        /// <summary>录制是否因会话切换等外部原因被中断。</summary>
        public bool Interrupted { get; init; }
    }

    /// <summary>
    /// 事件比较器：忽略方向与延迟，仅按来源和键值判断是否为同一个"物理键"。
    /// 用于识别按住不放时系统反复产生的重复按下消息。
    /// </summary>
    private class MacroEventEqualityComparer : IEqualityComparer<MacroEvent>
    {
        public bool Equals(MacroEvent x, MacroEvent y) => x.Source == y.Source && x.Key == y.Key;

        public int GetHashCode(MacroEvent obj) => HashCode.Combine(obj.Source, obj.Key);
    }

    // 记录当前处于按下状态的键，用于过滤按键自动重复（key rollover）产生的重复事件
    private readonly HashSet<MacroEvent> _rolloverCache = new(new MacroEventEqualityComparer());

    // 委托实例需长期持有，避免被 GC 回收后钩子回调失效
    private readonly HOOKPROC _kbProc;
    private readonly HOOKPROC _mouseProc;

    private HHOOK _kbHook;
    private HHOOK _mouseHook;
    private TimeSpan _timeFromLastEvent;
    private bool _interrupted;
    private MacroRecorderSettings _settings;

    /// <summary>
    /// 是否正在录制（键盘与鼠标钩子均已安装）。
    /// </summary>
    public bool IsRecording => _kbHook != HHOOK.Null && _mouseHook != HHOOK.Null;

    /// <summary>录制到新事件时触发。</summary>
    public event EventHandler<ReceivedEventArgs>? Received;

    /// <summary>录制停止时触发。</summary>
    public event EventHandler<StoppedEventArgs>? Stopped;

    /// <summary>
    /// 创建录制器并缓存钩子回调委托。
    /// </summary>
    public MacroRecorder()
    {
        _kbProc = LowLevelKeyboardProc;
        _mouseProc = LowLevelMouseProc;
    }

    /// <summary>
    /// 安装键盘与鼠标低级钩子并开始录制。
    /// </summary>
    /// <param name="settings">需要捕获的输入类别。</param>
    public void StartRecording(MacroRecorderSettings settings)
    {
        // 已经安装了键盘钩子则视为正在录制，直接返回
        if (_kbHook != HHOOK.Null)
            return;

        _interrupted = false;
        _timeFromLastEvent = TimeSpan.Zero;
        _settings = settings;

        // 清除上一会话遗留的按下状态，避免跨会话误判自动重复
        _rolloverCache.Clear();

        var kbHook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _kbProc, HINSTANCE.Null, 0);
        var mouseHook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_MOUSE_LL, _mouseProc, HINSTANCE.Null, 0);

        // 任一钩子安装失败时卸载已成功的钩子，避免留下泄漏的钩子
        if (kbHook == HHOOK.Null || mouseHook == HHOOK.Null)
        {
            if (kbHook != HHOOK.Null)
                PInvoke.UnhookWindowsHookEx(kbHook);
            if (mouseHook != HHOOK.Null)
                PInvoke.UnhookWindowsHookEx(mouseHook);

            return;
        }

        _kbHook = kbHook;
        _mouseHook = mouseHook;

        // 锁屏、注销、远程连接切换时钩子可能失效，需要在会话切换时主动结束录制
        SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
    }

    /// <summary>
    /// 卸载钩子并结束录制，随后触发 <see cref="Stopped"/> 事件。
    /// </summary>
    public void StopRecording()
    {
        var kbHookInstalled = _kbHook != HHOOK.Null;
        var mouseHookInstalled = _mouseHook != HHOOK.Null;

        if (!kbHookInstalled && !mouseHookInstalled)
            return;

        var wasInterrupted = _interrupted;

        SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;

        // 两个钩子分别判断并卸载，避免整体早退导致部分钩子泄漏
        if (kbHookInstalled)
        {
            PInvoke.UnhookWindowsHookEx(_kbHook);
            _kbHook = HHOOK.Null;
        }

        if (mouseHookInstalled)
        {
            PInvoke.UnhookWindowsHookEx(_mouseHook);
            _mouseHook = HHOOK.Null;
        }

        _interrupted = false;
        _timeFromLastEvent = TimeSpan.Zero;
        _settings = default;

        Stopped?.Invoke(this, new() { Interrupted = wasInterrupted });
    }

    /// <summary>
    /// 会话切换处理：遇到锁屏、注销、断开连接等情况时中断录制。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">会话切换事件参数。</param>
    private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        var interrupt = new[]
        {
            SessionSwitchReason.ConsoleDisconnect,
            SessionSwitchReason.SessionLock,
            SessionSwitchReason.SessionLogoff,
            SessionSwitchReason.SessionRemoteControl,
            SessionSwitchReason.RemoteDisconnect
        }.Contains(e.Reason);

        if (!interrupt)
            return;

        _interrupted = true;
        StopRecording();
    }

    /// <summary>
    /// 全局低级键盘钩子回调：把按键消息转换为宏事件并向上层转发。
    /// 按下 ESC 会终止录制，按下的按键不会传递给其它程序。
    /// </summary>
    /// <param name="nCode">钩子代码，小于 0 时应直接传递给下一个钩子。</param>
    /// <param name="wParam">键盘消息类型。</param>
    /// <param name="lParam">指向 <see cref="KBDLLHOOKSTRUCT"/> 的指针。</param>
    /// <returns>处理结果；返回非零值表示吞掉该按键。</returns>
    private unsafe LRESULT LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode < 0)
            return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);

        // 录制期间吞掉所有按键，返回任意非零值即可
        var result = new LRESULT(69);

        ref var kbStruct = ref Unsafe.AsRef<KBDLLHOOKSTRUCT>((void*)lParam.Value);

        var macroEvent = ConvertToMacroEvent(wParam, kbStruct, _timeFromLastEvent);

        if (macroEvent.IsUndefined())
            return result;

        if (macroEvent.Direction == MacroDirection.Down)
        {
            // ESC 作为停止录制的快捷键，不写入宏序列
            if (macroEvent.Key == (ulong)VIRTUAL_KEY.VK_ESCAPE)
            {
                StopRecording();
                return result;
            }

            // 按键处于按下状态时再次收到按下消息即为自动重复，直接忽略
            if (_rolloverCache.Contains(macroEvent))
                return result;
        }

        if (!_settings.HasFlag(MacroRecorderSettings.Keyboard))
            return result;

        Received?.Invoke(this, new ReceivedEventArgs { MacroEvent = macroEvent });

        // 记录本次事件时间戳，作为下一个事件延迟的计算基准
        _timeFromLastEvent = TimeSpan.FromMilliseconds(kbStruct.time);

        if (macroEvent.Direction == MacroDirection.Down)
            _rolloverCache.Add(macroEvent);
        else
            _rolloverCache.Remove(macroEvent);

        return result;
    }

    /// <summary>
    /// 全局低级鼠标钩子回调：把鼠标消息转换为宏事件并向上层转发。
    /// 鼠标移动消息会继续传递，其余消息被吞掉。
    /// </summary>
    /// <param name="nCode">钩子代码，小于 0 时应直接传递给下一个钩子。</param>
    /// <param name="wParam">鼠标消息类型。</param>
    /// <param name="lParam">指向 <see cref="MSLLHOOKSTRUCT"/> 的指针。</param>
    /// <returns>处理结果；鼠标移动除外均返回非零值以吞掉消息。</returns>
    private unsafe LRESULT LowLevelMouseProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode < 0)
            return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);

        ref var mouseStruct = ref Unsafe.AsRef<MSLLHOOKSTRUCT>((void*)lParam.Value);

        var macroEvent = ConvertToMacroEvent(wParam, mouseStruct, _timeFromLastEvent);

        if (macroEvent.IsUndefined())
            return Result();

        if (macroEvent.Direction == MacroDirection.Down && _rolloverCache.Contains(macroEvent))
            return Result();

        if (!_settings.HasFlag(MacroRecorderSettings.Mouse))
            return Result();

        // 未勾选"移动"时丢弃鼠标移动事件，避免产生海量轨迹数据
        if (macroEvent.Direction == MacroDirection.Move && !_settings.HasFlag(MacroRecorderSettings.Movement))
            return Result();

        Received?.Invoke(this, new ReceivedEventArgs { MacroEvent = macroEvent });

        _timeFromLastEvent = TimeSpan.FromMilliseconds(mouseStruct.time);

        if (macroEvent.Direction == MacroDirection.Down)
            _rolloverCache.Add(macroEvent);
        else
            _rolloverCache.Remove(macroEvent);

        return Result();

        // 鼠标移动需要继续传递，否则录制期间光标会卡住；其它消息吞掉即可
        LRESULT Result() => wParam == PInvoke.WM_MOUSEMOVE ? PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam) : new LRESULT(96);
    }

    /// <summary>
    /// 把键盘钩子消息转换为宏事件，并计算相对于上一个事件的时间延迟。
    /// </summary>
    /// <param name="wParam">键盘消息类型。</param>
    /// <param name="kbStruct">低级键盘钩子结构体。</param>
    /// <param name="timeFromLastEvent">上一个事件的时间戳；为 <see cref="TimeSpan.Zero"/> 时以当前事件为基准。</param>
    /// <returns>转换得到的宏事件，无法识别的消息方向为 <see cref="MacroDirection.Unknown"/>。</returns>
    private static MacroEvent ConvertToMacroEvent(WPARAM wParam, KBDLLHOOKSTRUCT kbStruct, TimeSpan timeFromLastEvent)
    {
        // 首个事件没有基准时间，延迟记为 0
        if (timeFromLastEvent == TimeSpan.Zero)
            timeFromLastEvent = TimeSpan.FromMilliseconds(kbStruct.time);

        // kbStruct.time 为系统启动以来的毫秒计数，两次事件相减即为间隔
        var delay = TimeSpan.FromMilliseconds(kbStruct.time) - timeFromLastEvent;

        var macroEvent = new MacroEvent
        {
            Source = MacroSource.Keyboard,
            Direction = (uint)wParam switch
            {
                PInvoke.WM_KEYUP or PInvoke.WM_SYSKEYUP => MacroDirection.Up,
                PInvoke.WM_KEYDOWN or PInvoke.WM_SYSKEYDOWN => MacroDirection.Down,
                _ => MacroDirection.Unknown
            },
            Key = kbStruct.vkCode,
            Delay = delay
        };

        return macroEvent;
    }

    /// <summary>
    /// 把鼠标钩子消息转换为宏事件，并计算相对于上一个事件的时间延迟。
    /// </summary>
    /// <param name="wParam">鼠标消息类型。</param>
    /// <param name="mouseStruct">低级鼠标钩子结构体。</param>
    /// <param name="timeFromLastEvent">上一个事件的时间戳；为 <see cref="TimeSpan.Zero"/> 时以当前事件为基准。</param>
    /// <returns>转换得到的宏事件，无法识别的消息方向为 <see cref="MacroDirection.Unknown"/>。</returns>
    private static MacroEvent ConvertToMacroEvent(WPARAM wParam, MSLLHOOKSTRUCT mouseStruct, TimeSpan timeFromLastEvent)
    {
        if (timeFromLastEvent == TimeSpan.Zero)
            timeFromLastEvent = TimeSpan.FromMilliseconds(mouseStruct.time);

        var delay = TimeSpan.FromMilliseconds(mouseStruct.time) - timeFromLastEvent;

        var macroEvent = new MacroEvent
        {
            Source = MacroSource.Mouse,
            Direction = (uint)wParam switch
            {
                PInvoke.WM_LBUTTONUP or PInvoke.WM_RBUTTONUP or PInvoke.WM_MBUTTONUP or PInvoke.WM_XBUTTONUP => MacroDirection.Up,
                PInvoke.WM_LBUTTONDOWN or PInvoke.WM_RBUTTONDOWN or PInvoke.WM_MBUTTONDOWN or PInvoke.WM_XBUTTONDOWN => MacroDirection.Down,
                PInvoke.WM_MOUSEWHEEL => MacroDirection.Wheel,
                PInvoke.WM_MOUSEHWHEEL => MacroDirection.HorizontalWheel,
                PInvoke.WM_MOUSEMOVE => MacroDirection.Move,
                _ => MacroDirection.Unknown
            },
            // XBUTTON 标识与滚轮增量都存放于 mouseData 中，XBUTTON 需左移 16 位还原为高位值
            Key = (uint)wParam switch
            {
                PInvoke.WM_LBUTTONUP or PInvoke.WM_LBUTTONDOWN => 1,
                PInvoke.WM_RBUTTONUP or PInvoke.WM_RBUTTONDOWN => 2,
                PInvoke.WM_MBUTTONUP or PInvoke.WM_MBUTTONDOWN => 3,
                PInvoke.WM_XBUTTONUP or PInvoke.WM_XBUTTONDOWN => mouseStruct.mouseData,
                PInvoke.WM_MOUSEWHEEL => (uint)((int)mouseStruct.mouseData >> 16),
                PInvoke.WM_MOUSEHWHEEL => (uint)((int)mouseStruct.mouseData >> 16),
                _ => 0
            },
            // 仅移动事件记录坐标，且使用屏幕坐标
            Point = (uint)wParam switch
            {
                PInvoke.WM_MOUSEMOVE => mouseStruct.pt,
                _ => Point.Empty,
            },
            Delay = delay
        };

        return macroEvent;
    }
}

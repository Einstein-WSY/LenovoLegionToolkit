using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;
using NeoSmart.AsyncLock;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace LenovoLegionToolkit.Lib.Macro.Utils;

/// <summary>
/// 宏回放器：把 <see cref="MacroSequence"/> 中的事件转换为 Win32 输入并注入系统。
/// </summary>
internal class MacroPlayer
{
    // 写入 dwExtraInfo 的标记值，用于把自身注入的输入与真实硬件输入区分开，避免回放被再次录制或触发新的宏
    private const int MAGIC_NUMBER = 1337;

    // 由钩子线程写入、回放线程读取，使用线程安全布尔值保证可见性
    private readonly ThreadSafeBool _isPlayingInterruptableSequence = new();

    private Task _playTask = Task.CompletedTask;
    private CancellationTokenSource _cancellationTokenSource = new();

    // 串行化 StartPlayingAsync 的“取消旧任务-等待-替换”序列，避免并发调用产生竞态
    private readonly AsyncLock _startLock = new();

    /// <summary>
    /// 若当前正在播放可中断的宏，且检测到真实用户输入，则取消当前回放。
    /// </summary>
    /// <param name="kbStruct">低级键盘钩子收到的按键信息。</param>
    public void InterruptIfNeeded(KBDLLHOOKSTRUCT kbStruct)
    {
        if (!_isPlayingInterruptableSequence.Value)
            return;
        // 仅响应真实硬件按键，忽略注入事件
        if (kbStruct.flags != 0)
            return;
        // 忽略回放自身注入的按键，否则第一条注入事件就会把回放打断
        if (kbStruct.dwExtraInfo == MAGIC_NUMBER)
            return;

        _cancellationTokenSource.Cancel();
    }

    /// <summary>
    /// 播放宏序列。若已有回放正在进行，会先取消并等待其结束，保证同一时间只有一个回放。
    /// </summary>
    /// <param name="sequence">待播放的宏序列。</param>
    /// <returns>回放任务完成的异步任务。</returns>
    public async Task StartPlayingAsync(MacroSequence sequence)
    {
        // 旧回放任务不会获取 _startLock，持锁等待其退出不会死锁
        using (await _startLock.LockAsync().ConfigureAwait(false))
        {
            // 取消上一次回放，并等待其退出，避免两次回放的输入交错
            await _cancellationTokenSource.CancelAsync();
            try { await _playTask; }
            catch (OperationCanceledException) { }

            _cancellationTokenSource = new();
            var token = _cancellationTokenSource.Token;

            _playTask = Task.Run(async () =>
            {
                _isPlayingInterruptableSequence.Value = sequence.InterruptOnOtherKey;

                for (var i = 0; i < sequence.RepeatCount; i++)
                {
                    foreach (var macroEvent in sequence.Events ?? [])
                    {
                        // 事件延迟是相对上一事件的相对时间，按录制顺序依次等待即可还原原始节奏
                        if (!sequence.IgnoreDelays)
                            await Task.Delay(macroEvent.Delay, token).ConfigureAwait(false);

                        token.ThrowIfCancellationRequested();

                        try
                        {
                            // SendInput 的 MOUSEEVENTF_ABSOLUTE 坐标按整个屏幕边界归一化到 0-65535
                            var input = ToInput(macroEvent, Screen.PrimaryScreen?.Bounds ?? Rectangle.Empty);
                            var result = PInvoke.SendInput(MemoryMarshal.CreateSpan(ref input, 1), Marshal.SizeOf<INPUT>());
                            if (result == 0)
                                PInvokeExtensions.ThrowIfWin32Error($"Failed to send input. Return code was {result}.");
                        }
                        catch (Exception ex)
                        {
                            // 单个事件失败不应中断整个宏，仅记录日志后继续
                            if (Log.Instance.IsTraceEnabled)
                                Log.Instance.Trace($"Failed to send input for event {macroEvent}", ex);
                        }
                    }
                }
            }, token);
        }
    }

    /// <summary>
    /// 按事件来源把宏事件转换为对应的 Win32 输入结构。
    /// </summary>
    /// <param name="macroEvent">待转换的宏事件。</param>
    /// <param name="screenArea">用于归一化鼠标绝对坐标的目标区域。</param>
    /// <returns>可直接交给 <c>SendInput</c> 的输入结构。</returns>
    /// <exception cref="ArgumentException">事件来源为 <see cref="MacroSource.Unknown"/>。</exception>
    /// <exception cref="ArgumentOutOfRangeException">事件来源超出已知枚举范围。</exception>
    private static INPUT ToInput(MacroEvent macroEvent, Rectangle screenArea) => macroEvent.Source switch
    {
        MacroSource.Keyboard => ToKeyboardInput(macroEvent),
        MacroSource.Mouse => ToMouseInput(macroEvent, screenArea),
        MacroSource.Unknown => throw new ArgumentException(null, nameof(macroEvent)),
        _ => throw new ArgumentOutOfRangeException(nameof(macroEvent))
    };

    /// <summary>
    /// 构造键盘输入事件。
    /// </summary>
    /// <param name="macroEvent">键盘宏事件，其 <see cref="MacroEvent.Key"/> 为虚拟键码。</param>
    /// <returns>键盘输入结构。</returns>
    private static INPUT ToKeyboardInput(MacroEvent macroEvent) => new()
    {
        type = INPUT_TYPE.INPUT_KEYBOARD,
        Anonymous = new INPUT._Anonymous_e__Union
        {
            ki = new KEYBDINPUT
            {
                wVk = (VIRTUAL_KEY)macroEvent.Key,
                // 抬起事件必须带 KEYEVENTF_KEYUP，否则会被系统视为又一次按下
                dwFlags = macroEvent.Direction switch
                {
                    MacroDirection.Up => KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
                    _ => 0
                },
                dwExtraInfo = MAGIC_NUMBER
            }
        }
    };

    /// <summary>
    /// 构造鼠标输入事件（按键、滚轮或移动）。
    /// </summary>
    /// <param name="macroEvent">鼠标宏事件。</param>
    /// <param name="screenArea">用于把屏幕坐标换算为绝对坐标的目标区域。</param>
    /// <returns>鼠标输入结构。</returns>
    private static INPUT ToMouseInput(MacroEvent macroEvent, Rectangle screenArea) => new()
    {
        type = INPUT_TYPE.INPUT_MOUSE,
        Anonymous = new INPUT._Anonymous_e__Union
        {
            mi = new MOUSEINPUT
            {
                // 按键编号 1/2/3 对应左/右/中键，大于 0xFF 表示 XBUTTON（第 4、5 键）
                dwFlags = (macroEvent.Direction, macroEvent.Key) switch
                {
                    (MacroDirection.Up, 1) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP,
                    (MacroDirection.Down, 1) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN,
                    (MacroDirection.Up, 2) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP,
                    (MacroDirection.Down, 2) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN,
                    (MacroDirection.Up, 3) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEUP,
                    (MacroDirection.Down, 3) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_MIDDLEDOWN,
                    (MacroDirection.Up, > 0xFF) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_XUP,
                    (MacroDirection.Down, > 0xFF) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_XDOWN,
                    (MacroDirection.Wheel, _) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_WHEEL,
                    (MacroDirection.Move, _) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE | MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE,
                    _ => 0
                },
                // XBUTTON 的标识放在 mouseData 的高 16 位；滚轮使用带符号的滚动量
                mouseData = (macroEvent.Direction, macroEvent.Key) switch
                {
                    (MacroDirection.Up, >= 0xFF) => macroEvent.Key >> 16,
                    (MacroDirection.Down, >= 0xFF) => macroEvent.Key >> 16,
                    (MacroDirection.Wheel, _) => macroEvent.Key,
                    _ => 0
                },
                // 绝对坐标需把屏幕像素按目标区域宽度线性映射到 0-65535 的整数范围
                dx = macroEvent.Direction switch
                {
                    MacroDirection.Move => (int)(65535.0f * (macroEvent.Point.X / (float)screenArea.Width) + 0.5f),
                    _ => 0
                },
                dy = macroEvent.Direction switch
                {
                    MacroDirection.Move => (int)(65535.0f * (macroEvent.Point.Y / (float)screenArea.Height) + 0.5f),
                    _ => 0
                },
                dwExtraInfo = MAGIC_NUMBER
            }
        }
    };
}

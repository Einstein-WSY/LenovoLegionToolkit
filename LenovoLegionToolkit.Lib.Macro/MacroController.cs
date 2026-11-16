using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Macro.Utils;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace LenovoLegionToolkit.Lib.Macro;

/// <summary>
/// 宏功能入口：负责安装全局键盘钩子、触发宏回放，并把录制结果转发给上层。
/// </summary>
public class MacroController
{
    /// <summary>
    /// 录制到单个事件时的事件参数。
    /// </summary>
    public class RecorderReceivedEventArgs : EventArgs
    {
        /// <summary>本次录制到的事件。</summary>
        public MacroEvent MacroEvent { get; init; }
    }

    /// <summary>
    /// 录制结束时的事件参数。
    /// </summary>
    public class RecorderStoppedEventArgs : EventArgs
    {
        /// <summary>录制是否因外部原因（如会话锁定）被中断。</summary>
        public bool Interrupted { get; init; }
    }

    // 仅允许用数字小键盘 0-9 触发宏，避免误触普通按键
    private static readonly uint[] AllowedKeys = [0x60, 0x61, 0x62, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69];
    /// <summary>可选的宏重复播放次数。</summary>
    public static readonly int[] AllowedRepeatCounts = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    private readonly MacroRecorder _recorder = new();
    private readonly MacroPlayer _player = new();

    // 委托实例需长期持有，否则被 GC 回收后钩子回调会崩溃
    private readonly HOOKPROC _kbProc;
    private readonly MacroSettings _settings;

    private HHOOK _kbHook;

    /// <summary>录制到新事件时触发。</summary>
    public event EventHandler<RecorderReceivedEventArgs>? RecorderReceived;

    /// <summary>录制停止时触发。</summary>
    public event EventHandler<RecorderStoppedEventArgs>? RecorderStopped;

    /// <summary>宏功能是否已启用（启用时键盘钩子才会触发回放）。</summary>
    public bool IsEnabled => _settings.Store.IsEnabled;

    /// <summary>
    /// 创建宏控制器并订阅内部录制器的事件。
    /// </summary>
    /// <param name="settings">宏设置存储。</param>
    public MacroController(MacroSettings settings)
    {
        _settings = settings;

        _kbProc = LowLevelKeyboardProc;

        _recorder.Received += Recorder_Received;
        _recorder.Stopped += Recorder_Stopped;
    }

    private void Recorder_Received(object? sender, MacroRecorder.ReceivedEventArgs e) => RecorderReceived?.Invoke(this, new() { MacroEvent = e.MacroEvent });

    private void Recorder_Stopped(object? sender, MacroRecorder.StoppedEventArgs e) => RecorderStopped?.Invoke(this, new() { Interrupted = e.Interrupted });

    /// <summary>
    /// 启用或停用宏功能，并立即写入配置文件。
    /// </summary>
    /// <param name="enabled">为 <see langword="true"/> 时启用宏。</param>
    public void SetEnabled(bool enabled)
    {
        _settings.Store.IsEnabled = enabled;
        _settings.SynchronizeStore();
    }

    /// <summary>
    /// 获取当前所有宏序列（键为触发用的输入标识）。
    /// </summary>
    /// <returns>宏序列字典。</returns>
    public Dictionary<MacroIdentifier, MacroSequence> GetSequences() => _settings.Store.Sequences;

    /// <summary>
    /// 保存宏序列，保存前会清理不完整与空序列。
    /// </summary>
    /// <param name="sequences">待保存的宏序列字典。</param>
    public void SetSequences(Dictionary<MacroIdentifier, MacroSequence> sequences)
    {
        CleanUp(ref sequences);

        _settings.Store.Sequences = sequences;
        _settings.SynchronizeStore();
    }

    /// <summary>
    /// 安装全局低级键盘钩子。若钩子已存在则直接返回，可重复调用。
    /// </summary>
    public void Start()
    {
        if (_kbHook != default)
            return;

        // WH_KEYBOARD_LL 为全局低级键盘钩子，回调在安装钩子的线程消息循环中执行
        _kbHook = PInvoke.SetWindowsHookEx(WINDOWS_HOOK_ID.WH_KEYBOARD_LL, _kbProc, HINSTANCE.Null, 0);
    }

    /// <summary>
    /// 开始录制宏事件。
    /// </summary>
    /// <param name="settings">需要捕获的输入类别，默认仅键盘。</param>
    public void StartRecording(MacroRecorderSettings settings = MacroRecorderSettings.Keyboard) => _recorder.StartRecording(settings);

    /// <summary>
    /// 停止录制宏事件。
    /// </summary>
    public void StopRecording() => _recorder.StopRecording();

    /// <summary>
    /// 全局低级键盘钩子回调：判断按下的键是否绑定宏，并触发回放。
    /// </summary>
    /// <param name="nCode">钩子代码，小于 0 时应直接传递给下一个钩子。</param>
    /// <param name="wParam">键盘消息类型（WM_KEYDOWN、WM_KEYUP 等）。</param>
    /// <param name="lParam">指向 <see cref="KBDLLHOOKSTRUCT"/> 的指针。</param>
    /// <returns>处理结果；返回非零值表示吞掉该按键，不再传递给其它程序。</returns>
    private unsafe LRESULT LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode != PInvoke.HC_ACTION)
            return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);

        if (!IsEnabled)
            return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);

        ref var kbStruct = ref Unsafe.AsRef<KBDLLHOOKSTRUCT>((void*)lParam.Value);

        // 先处理"按键打断回放"，保证任意按下的键都能停止正在播放的宏
        _player.InterruptIfNeeded(kbStruct);

        var shouldRun = !_recorder.IsRecording;
        // flags 为 0 表示事件来自真实硬件，忽略由 SendInput 注入的按键以防递归触发
        shouldRun &= kbStruct.flags == 0;
        shouldRun &= _settings.Store.Sequences.GetValueOrNull(new(MacroSource.Keyboard, kbStruct.vkCode))?.Events?.Length > 0;
        shouldRun &= AllowedKeys.Contains(kbStruct.vkCode);

        if (!shouldRun)
            return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);

        var sequence = _settings.Store.Sequences[new(MacroSource.Keyboard, kbStruct.vkCode)];
        Play(sequence);

        // 返回非零值（96 为任意非零值）以吞掉该按键，避免触发键本身被其它程序接收
        return new LRESULT(96);
    }

    /// <summary>
    /// 在后台线程播放宏序列，避免阻塞钩子回调。
    /// </summary>
    /// <param name="sequence">待播放的宏序列。</param>
    private void Play(MacroSequence sequence) => Task.Run(() => _player.StartPlayingAsync(sequence));

    /// <summary>
    /// 保存前的清理：移除没有配对的按下事件以及事件列表为空的序列，保证回放时不会留下"卡住"的按键。
    /// </summary>
    /// <param name="sequences">待清理的宏序列字典，会被替换为清理后的新字典。</param>
    private static void CleanUp(ref Dictionary<MacroIdentifier, MacroSequence> sequences)
    {
        sequences = ClearDownsWithoutUps(sequences);
        sequences = ClearEmptySequences(sequences);
    }

    /// <summary>
    /// 过滤掉事件列表为空的宏序列。
    /// </summary>
    /// <param name="sequences">待过滤的宏序列字典。</param>
    /// <returns>仅包含非空序列的新字典。</returns>
    private static Dictionary<MacroIdentifier, MacroSequence> ClearEmptySequences(Dictionary<MacroIdentifier, MacroSequence> sequences)
    {
        return sequences.Where(kv => kv.Value.Events?.Length > 0)
            .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    /// <summary>
    /// 对所有宏序列去除孤立的按下事件。
    /// </summary>
    /// <param name="sequences">待处理的宏序列字典。</param>
    /// <returns>清理后的新字典。</returns>
    private static Dictionary<MacroIdentifier, MacroSequence> ClearDownsWithoutUps(Dictionary<MacroIdentifier, MacroSequence> sequences)
    {
        var result = new Dictionary<MacroIdentifier, MacroSequence>();
        foreach (var kv in sequences)
            result[kv.Key] = ClearDownsWithoutUps(kv.Value);
        return result;
    }

    /// <summary>
    /// 去除单个序列中找不到对应抬起事件的按下事件。
    /// 从后向前遍历，以便删除元素时不影响尚未检查的下标。
    /// </summary>
    /// <param name="sequence">待清理的宏序列。</param>
    /// <returns>事件数组已清理的新序列。</returns>
    private static MacroSequence ClearDownsWithoutUps(MacroSequence sequence)
    {
        var macroEvents = new List<MacroEvent>(sequence.Events ?? []);
        for (var i = macroEvents.Count - 1; i >= 0; i--)
        {
            var macroEvent = macroEvents[i];

            switch (macroEvent.Direction)
            {
                case MacroDirection.Down:
                    {
                        var remove = true;

                        // 在同来源、同键值的事件中向后查找抬起事件，找到则保留本次按下
                        for (var j = i; j < macroEvents.Count; j++)
                        {
                            if (macroEvents[j].Direction != MacroDirection.Up || macroEvents[j].Key != macroEvent.Key || macroEvents[j].Source != macroEvent.Source)
                                continue;

                            remove = false;
                            break;
                        }

                        if (remove)
                            macroEvents.RemoveAt(i);
                        break;
                    }
                case MacroDirection.Up:
                case MacroDirection.Unknown:
                default:
                    continue;
            }
        }

        return sequence with { Events = [.. macroEvents] };
    }
}

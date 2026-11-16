using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;
using NeoSmart.AsyncLock;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace LenovoLegionToolkit.Lib.Controllers;

/// <summary>
/// 智能 Fn 锁控制器：在用户按住指定修饰键（Ctrl/Shift/Alt）时临时关闭 Fn 锁，
/// 松开后自动恢复，使功能键在需要组合键时能正常作为 F1-F12 使用。
/// </summary>
public class SmartFnLockController(FnLockFeature feature, ApplicationSettings settings)
{
    private readonly AsyncLock _lock = new(); // 串行处理键盘事件，避免并发读写 Fn 锁状态

    // 三个修饰键的按下状态（由低级键盘钩子持续跟踪）
    private bool _ctrlDepressed;
    private bool _shiftDepressed;
    private bool _altDepressed;
    private bool _restoreFnLock; // 是否由本控制器临时关闭了 Fn 锁、待松开后恢复

    /// <summary>
    /// 处理一次键盘事件（由全局键盘钩子调用）。
    /// 为避免阻塞钩子回调，实际处理被投递到线程池执行。
    /// </summary>
    /// <param name="wParam">钩子消息类型（WM_KEYDOWN/WM_KEYUP 等）。</param>
    /// <param name="kbStruct">底层键盘事件数据。</param>
    public void OnKeyboardEvent(nuint wParam, KBDLLHOOKSTRUCT kbStruct)
    {
        // 用户未启用智能 Fn 锁（标志位为 0）时直接跳过，保持钩子开销最小
        if (settings.Store.SmartFnLockFlags == 0)
            return;

        Task.Run(async () =>
        {
            try
            {
                using (await _lock.LockAsync().ConfigureAwait(false))
                    await OnKeyboardEventAsync(wParam, kbStruct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to handle keyboard event.", ex);
            }
        });
    }

    private async Task OnKeyboardEventAsync(nuint wParam, KBDLLHOOKSTRUCT kbStruct)
    {
        if (IsModifierKeyPressed(wParam, kbStruct))
        {
            // 已经临时关闭过则不重复操作（按住期间会持续产生按键事件）
            if (_restoreFnLock)
                return;

            var state = await feature.GetStateAsync().ConfigureAwait(false);
            if (state == FnLockState.Off)
                return;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Disabling Fn Lock temporarily...");

            await feature.SetStateAsync(FnLockState.Off).ConfigureAwait(false);
            _restoreFnLock = true;
        }
        else if (_restoreFnLock)
        {
            // 修饰键已全部松开：恢复用户原本的 Fn 锁状态
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Re-enabling Fn Lock...");

            await feature.SetStateAsync(FnLockState.On).ConfigureAwait(false);
            _restoreFnLock = false;
        }
    }

    // 判断"用户配置的修饰键"当前是否被按下
    private bool IsModifierKeyPressed(nuint wParam, KBDLLHOOKSTRUCT kbStruct)
    {
        var isKeyDown = wParam is PInvoke.WM_KEYDOWN or PInvoke.WM_SYSKEYDOWN;
        var vkKeyCode = (VIRTUAL_KEY)kbStruct.vkCode;

        // 左右键分别跟踪，任何一个按下即视为该修饰键处于按下状态
        if (vkKeyCode is VIRTUAL_KEY.VK_LCONTROL or VIRTUAL_KEY.VK_RCONTROL)
            _ctrlDepressed = isKeyDown;

        if (vkKeyCode is VIRTUAL_KEY.VK_LSHIFT or VIRTUAL_KEY.VK_RSHIFT)
            _shiftDepressed = isKeyDown;

        if (vkKeyCode is VIRTUAL_KEY.VK_LMENU or VIRTUAL_KEY.VK_RMENU)
            _altDepressed = isKeyDown;

        // 无任何修饰键按下时提前返回
        if (!_ctrlDepressed && !_shiftDepressed && !_altDepressed)
            return false;

        var result = false;
        var flags = settings.Store.SmartFnLockFlags;

        // 只有用户启用的修饰键才参与判断
        if (flags.HasFlag(ModifierKey.Ctrl))
            result |= _ctrlDepressed;

        if (flags.HasFlag(ModifierKey.Shift))
            result |= _shiftDepressed;

        if (flags.HasFlag(ModifierKey.Alt))
            result |= _altDepressed;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Modifier key is depressed: {result} [ctrl={_ctrlDepressed}, shift={_shiftDepressed}, alt={_altDepressed}, flags={flags}]");

        return result;
    }
}

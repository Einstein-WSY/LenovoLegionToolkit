using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.System.Power;

namespace LenovoLegionToolkit.Lib.GameDetection;

/// <summary>
/// 通过 Windows "有效电源模式" 通知来检测游戏模式是否处于激活状态。
/// <para>
/// Windows 会在全屏游戏等场景下自动切换到 GameMode 有效电源模式，
/// 本类注册系统回调以实时感知该状态变化，并对外触发 <see cref="Changed"/> 事件。
/// </para>
/// </summary>
internal unsafe class EffectiveGameModeDetector
{
    // 回调委托必须作为字段长期持有，否则会被 GC 回收导致系统回调时访问已释放的委托
    private readonly EFFECTIVE_POWER_MODE_CALLBACK _callbackPointer;

    private IntPtr _handle;
    private bool? _lastState;

    /// <summary>
    /// 当游戏模式状态发生变化时触发，事件参数为当前是否处于游戏模式。
    /// </summary>
    public event EventHandler<bool>? Changed;

    /// <summary>
    /// 初始化检测器。
    /// </summary>
    public EffectiveGameModeDetector()
    {
        _callbackPointer = Callback;
    }

    /// <summary>
    /// 注册有效电源模式通知，开始监听游戏模式变化。
    /// </summary>
    /// <returns>表示操作完成的任务。</returns>
    public Task StartAsync()
    {
        var result = PInvoke.PowerRegisterForEffectivePowerModeNotifications(PInvoke.EFFECTIVE_POWER_MODE_V2, _callbackPointer, null, out var handle);
        // 返回 0 表示注册成功，保存句柄用于后续反注册
        if (result == 0)
            _handle = new IntPtr(handle);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 取消注册，停止监听游戏模式变化。
    /// </summary>
    /// <returns>表示操作完成的任务。</returns>
    public Task StopAsync()
    {
        PInvoke.PowerUnregisterFromEffectivePowerModeNotifications(_handle.ToPointer());
        _handle = IntPtr.Zero;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 系统电源模式变化的回调入口。
    /// </summary>
    /// <param name="mode">当前有效电源模式。</param>
    /// <param name="context">注册时传入的上下文指针（此处未使用）。</param>
    private void Callback(EFFECTIVE_POWER_MODE mode, void* context)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Effective power mode is {mode}.");

        var state = mode == EFFECTIVE_POWER_MODE.EffectivePowerModeGameMode;

        // 首次回调仅记录初始状态，不触发事件
        _lastState ??= state;

        if (_lastState == state)
            return;

        _lastState = state;

        Changed?.Invoke(this, state);
    }
}

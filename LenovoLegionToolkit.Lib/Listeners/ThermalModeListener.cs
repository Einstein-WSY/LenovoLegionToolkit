using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Listeners;

/// <summary>
/// 散热模式监听器：监听 WMI 的 LenovoGameZoneThermalModeEvent，感知散热模式被硬件快捷键切换，
/// 并据此同步 Windows 电源模式与电源计划。支持"抑制下一次"机制以避免由本工具自身写入引发的回环。
/// </summary>
public class ThermalModeListener(
    WindowsPowerModeController windowsPowerModeController,
    WindowsPowerPlanController windowsPowerPlanController)
    : AbstractWMIListener<ThermalModeListener.ChangedEventArgs, ThermalModeState, int>(WMI.LenovoGameZoneThermalModeEvent.Listen)
{
    /// <summary>
    /// 散热模式变更事件参数。
    /// </summary>
    public class ChangedEventArgs(ThermalModeState state) : EventArgs
    {
        /// <summary>
        /// 变更后的散热模式。
        /// </summary>
        public ThermalModeState State { get; } = state;
    }

    // 计数器用于"抑制下一次"事件：本工具主动切换散热模式时，硬件随后上报的事件应被忽略
    private readonly ThreadSafeCounter _suppressCounter = new();

    /// <inheritdoc/>
    protected override ThermalModeState GetValue(int value)
    {
        var state = (ThermalModeState)value;

        // 新机型可能上报枚举中尚未定义的散热模式，转为 Unknown 以安全忽略
        if (!Enum.IsDefined(state))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Unknown value received: {value}");

            state = ThermalModeState.Unknown;
        }

        return state;
    }

    /// <inheritdoc/>
    protected override ChangedEventArgs GetEventArgs(ThermalModeState value) => new(value);

    /// <inheritdoc/>
    protected override async Task OnChangedAsync(ThermalModeState state)
    {
        // 若处于抑制窗口内，消费一次计数并跳过本次同步，避免自触发回环
        if (!_suppressCounter.Decrement())
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Suppressed.");
            return;
        }

        if (state == ThermalModeState.Unknown)
            return;

        var powerModeState = state switch
        {
            ThermalModeState.Quiet => PowerModeState.Quiet,
            ThermalModeState.Balance => PowerModeState.Balance,
            ThermalModeState.Performance => PowerModeState.Performance,
            ThermalModeState.GodMode => PowerModeState.GodMode,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };

        // 将散热模式映射为对应的 Windows 电源模式与电源计划，保证性能行为一致
        await windowsPowerModeController.SetPowerModeAsync(powerModeState).ConfigureAwait(false);
        await windowsPowerPlanController.SetPowerPlanAsync(powerModeState).ConfigureAwait(false);
    }

    /// <summary>
    /// 抑制下一次散热模式变更事件的处理（通常在本工具即将主动切换散热模式时调用）。
    /// </summary>
    public void SuppressNext()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Suppressing next...");

        _suppressCounter.Increment();
    }
}

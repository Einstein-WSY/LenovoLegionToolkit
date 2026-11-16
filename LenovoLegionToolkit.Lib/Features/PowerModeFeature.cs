using System;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Controllers.GodMode;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// 在未连接电源适配器时尝试切换到高性能/自定义模式所抛出的异常。
/// </summary>
/// <param name="powerMode">被拒绝的目标电源模式。</param>
public class PowerModeUnavailableWithoutACException(PowerModeState powerMode) : Exception
{
    /// <summary>被拒绝的目标电源模式。</summary>
    public PowerModeState PowerMode { get; } = powerMode;
}

/// <summary>
/// 电源模式特性（安静 / 均衡 / 性能 / 自定义）。
/// 通过 WMI Lenovo_GameZone_Data 的 SmartFanMode 接口读写，
/// 同时联动 Windows 电源模式、电源计划、自定义模式控制器与热模式监听器。
/// 注意：WMI 值比枚举值大 1（构造参数 offset = 1）。
/// </summary>
public class PowerModeFeature(
    GodModeController godModeController,
    WindowsPowerModeController windowsPowerModeController,
    WindowsPowerPlanController windowsPowerPlanController,
    ThermalModeListener thermalModeListener,
    PowerModeListener powerModeListener)
    : AbstractWmiFeature<PowerModeState>(WMI.LenovoGameZoneData.GetSmartFanModeAsync, WMI.LenovoGameZoneData.SetSmartFanModeAsync, WMI.LenovoGameZoneData.IsSupportSmartFanAsync, 1)
{
    /// <summary>
    /// 为 <c>true</c> 时允许在电池供电下切换到性能/自定义模式（默认仅接通电源时允许）。
    /// </summary>
    public bool AllowAllPowerModesOnBattery { get; set; }

    /// <inheritdoc/>
    public override async Task<PowerModeState[]> GetAllStatesAsync()
    {
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        // 仅支持自定义模式（GodMode）的机型才提供第四档
        return mi.Properties.SupportsGodMode
            ? [PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance, PowerModeState.GodMode]
            : [PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance];
    }

    /// <inheritdoc/>
    public override async Task SetStateAsync(PowerModeState state)
    {
        var allStates = await GetAllStatesAsync().ConfigureAwait(false);
        if (!allStates.Contains(state))
            throw new InvalidOperationException($"Unsupported power mode {state}");

        // 性能模式与自定义模式功耗较高，电池供电下默认禁止切换
        if (state is PowerModeState.Performance or PowerModeState.GodMode
            && !AllowAllPowerModesOnBattery
            && await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false) is PowerAdapterStatus.Disconnected)
            throw new PowerModeUnavailableWithoutACException(state);

        var currentState = await GetStateAsync().ConfigureAwait(false);

        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

        // 部分机型存在"安静模式直接切性能模式"的固件 bug：
        // 需先切到均衡模式过渡，等待 500ms 让固件稳定，再切到目标模式
        if (mi.Properties.HasQuietToPerformanceModeSwitchingBug && currentState == PowerModeState.Quiet && state == PowerModeState.Performance)
        {
            thermalModeListener.SuppressNext();
            await base.SetStateAsync(PowerModeState.Balance).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);
        }

        // 部分机型存在"从自定义模式切出"的固件 bug：
        // 需要先切到一个特定的中间模式，等待固件稳定后再切到目标模式
        if (mi.Properties.HasGodModeToOtherModeSwitchingBug && currentState == PowerModeState.GodMode && state != PowerModeState.GodMode)
        {
            thermalModeListener.SuppressNext();

            switch (state)
            {
                case PowerModeState.Quiet:
                    await base.SetStateAsync(PowerModeState.Performance).ConfigureAwait(false);
                    break;
                case PowerModeState.Balance:
                    await base.SetStateAsync(PowerModeState.Quiet).ConfigureAwait(false);
                    break;
                case PowerModeState.Performance:
                    await base.SetStateAsync(PowerModeState.Balance).ConfigureAwait(false);
                    break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);

        }

        // 抑制本次切换引发的热模式事件，避免被自身监听器重复处理
        thermalModeListener.SuppressNext();
        await base.SetStateAsync(state).ConfigureAwait(false);

        // 广播电源模式变化，通知其他组件（如风扇控制、OSD）
        await powerModeListener.NotifyAsync(state).ConfigureAwait(false);
    }

    /// <summary>
    /// 确保 Windows 电源模式与电源计划与当前硬件电源模式一致（例如程序启动或系统唤醒后调用）。
    /// </summary>
    public async Task EnsureCorrectWindowsPowerSettingsAreSetAsync()
    {
        var state = await GetStateAsync().ConfigureAwait(false);
        await windowsPowerModeController.SetPowerModeAsync(state).ConfigureAwait(false);
        await windowsPowerPlanController.SetPowerPlanAsync(state, true).ConfigureAwait(false);
    }

    /// <summary>
    /// 若当前处于自定义（GodMode）模式，则重新应用自定义风扇/功耗配置
    /// （系统更新或驱动重载后这些设置可能被重置）。
    /// </summary>
    public async Task EnsureGodModeStateIsAppliedAsync()
    {
        var state = await GetStateAsync().ConfigureAwait(false);
        if (state != PowerModeState.GodMode)
            return;

        await godModeController.ApplyStateAsync().ConfigureAwait(false);
    }
}

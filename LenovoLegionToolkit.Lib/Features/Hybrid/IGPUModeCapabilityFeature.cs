using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features.Hybrid;

/// <summary>
/// iGPU 模式特性 —— 基于 Capability（能力数据）机制的新机型实现。
/// 通过 WMI Lenovo_Other_Method 读写 CapabilityID.IGPUMode，
/// 并通过 CapabilityID.IGPUModeChangeStatus 判断切换是否真正完成。
/// </summary>
public class IGPUModeCapabilityFeature : IFeature<IGPUModeState>
{
    /// <inheritdoc/>
    public async Task<bool> IsSupportedAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            // 必须同时满足：机器信息来源于 CapabilityData 且声明确实支持 iGPU 模式
            return mi is { Features.Source: MachineInformation.FeatureData.SourceType.CapabilityData, Properties.SupportsIGPUMode: true };
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public Task<IGPUModeState[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<IGPUModeState>());

    /// <inheritdoc/>
    public async Task<IGPUModeState> GetStateAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting state...");

        var value = await WMI.LenovoOtherMethod.GetFeatureValueAsync(CapabilityID.IGPUMode).ConfigureAwait(false);
        var result = (IGPUModeState)value;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State is {result}");

        return result;
    }

    /// <inheritdoc/>
    public async Task SetStateAsync(IGPUModeState state)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting state to {state}...");

        await WMI.LenovoOtherMethod.SetFeatureValueAsync(CapabilityID.IGPUMode, (int)state).ConfigureAwait(false);
        // 固件在写入后会置位 ChangeStatus；若为 0 说明 dGPU 检查失败（无法断电/切回），需让上层感知
        if (await WMI.LenovoOtherMethod.GetFeatureValueAsync(CapabilityID.IGPUModeChangeStatus).ConfigureAwait(false) == 0)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Set state to {state}, but dGPU check failed.");

            throw new IGPUModeChangeException(state);
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Set state to {state}");
    }
}

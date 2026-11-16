using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features.Hybrid;

/// <summary>
/// iGPU 模式特性 —— 基于 FeatureFlags（设备当前支持特性位）的实验性实现。
/// 通过 WMI Lenovo_Other_Method 的 Get/SetDeviceCurrentSupportFeature 接口读写，
/// 用位标志表示当前所处的 iGPU 模式（bit0=纯核显，bit1=自动）。
/// </summary>
public class IGPUModeFeatureFlagsFeature : IFeature<IGPUModeState>
{
    /// <inheritdoc/>
    public async Task<bool> IsSupportedAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            // 必须同时满足：机器信息来源于 Flags 且声明支持 iGPU 模式
            return mi is { Features.Source: MachineInformation.FeatureData.SourceType.Flags, Properties.SupportsIGPUMode: true };
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

        var flags = await WMI.LenovoOtherMethod.GetDeviceCurrentSupportFeatureAsync().ConfigureAwait(false);

        // 默认状态为 Default（混合模式），再按位覆盖
        var result = IGPUModeState.Default;

        // bit0 表示纯核显模式
        if (flags.IsBitSet(0))
            result = IGPUModeState.IGPUOnly;
        // bit1 表示自动模式（优先级更高，覆盖前者）
        if (flags.IsBitSet(1))
            result = IGPUModeState.Auto;


        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State is {result}");

        return result;
    }

    /// <inheritdoc/>
    public async Task SetStateAsync(IGPUModeState state)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting state to {state}...");

        // 第一个参数 1 表示写入"当前支持特性位"；返回 0 表示 dGPU 检查失败
        var result = await WMI.LenovoOtherMethod.SetDeviceCurrentSupportFeatureAsync(1, (int)state).ConfigureAwait(false);
        if (result == 0)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Set state to {state}, but dGPU check failed.");

            throw new IGPUModeChangeException(state);
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Set state to {state}");
    }
}

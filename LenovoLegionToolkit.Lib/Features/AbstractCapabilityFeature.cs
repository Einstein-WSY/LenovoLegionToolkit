using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// 基于 Lenovo "Capability"（能力数据）机制的特性基类。
/// 通过 WMI Lenovo_Other_Method 的 Get/Set FeatureValue 接口读写状态，
/// 并借助机器信息中的 CapabilityData 表判断硬件是否支持。
/// <typeparamref name="T"/> 为特性状态枚举。
/// </summary>
/// <typeparam name="T">特性状态枚举类型。</typeparam>
/// <param name="capabilityID">对应的 CapabilityID，用于 WMI 调用与支持性查询。</param>
public abstract class AbstractCapabilityFeature<T>(CapabilityID capabilityID)
    : IFeature<T> where T : struct, Enum, IComparable, IConvertible
{
    /// <inheritdoc/>
    public async Task<bool> IsSupportedAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            // 仅当机器信息来源于 CapabilityData 且该 capability 位被置 1 时才视为支持
            return mi.Features.Source == MachineInformation.FeatureData.SourceType.CapabilityData && mi.Features[capabilityID];
        }
        catch
        {
            // 获取机器信息失败（如老机型无 CapabilityData）时静默视为不支持
            return false;
        }
    }

    /// <inheritdoc/>
    public Task<T[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<T>());

    /// <inheritdoc/>
    public async Task<T> GetStateAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting state... [feature={GetType().Name}]");

        var value = await WMI.LenovoOtherMethod.GetFeatureValueAsync(capabilityID).ConfigureAwait(false);
        // WMI 返回 int，需转换回枚举；若返回值未在枚举中定义则说明固件返回了意外数据
        var result = (T)Enum.ToObject(typeof(T), value);
        if (!Enum.IsDefined(result))
            throw new InvalidOperationException($"Undefined value received: {result} [type={typeof(T)}, feature={GetType().Name}]");

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State is {result} [feature={GetType().Name}]");

        return result;
    }

    /// <inheritdoc/>
    public async Task SetStateAsync(T state)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting state to {state}... [feature={GetType().Name}]");

        // 枚举转 int 后直接写入 WMI，由固件负责实际生效
        await WMI.LenovoOtherMethod.SetFeatureValueAsync(capabilityID, Convert.ToInt32(state)).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Set state to {state} [feature={GetType().Name}]");
    }
}

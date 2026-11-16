using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// 基于 Lenovo Lighting WMI 接口的灯效特性基类。
/// 通过 Lenovo_Lighting_Data / Lenovo_Lighting_Method 读写指定灯效 ID 的状态，
/// 状态由 (stateType, level) 二元组表示，子类负责与枚举互转。
/// </summary>
/// <typeparam name="T">特性状态枚举类型。</typeparam>
/// <param name="lightingID">灯效区域/设备的 ID。</param>
/// <param name="controlInterface">控制接口编号，用于判断该灯效控制通道是否存在。</param>
/// <param name="type">灯效类型标识。</param>
public abstract class AbstractLenovoLightingFeature<T>(int lightingID, int controlInterface, int type) : IFeature<T> where T : struct, Enum, IComparable
{
    /// <summary>为 <c>true</c> 时强制判定为不支持（供用户在设置中手动禁用灯效时使用）。</summary>
    public bool ForceDisable { get; set; }

    /// <inheritdoc/>
    public virtual async Task<bool> IsSupportedAsync()
    {
        if (ForceDisable)
            return false;

        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            // 部分机型（如某些 ThinkBook）虽暴露灯效 WMI，但实际不可用，需排除
            if (mi.Properties.IsExcludedFromLenovoLighting)
                return false;

            var isSupported = await WMI.LenovoLightingData.ExistsAsync(lightingID, controlInterface, type).ConfigureAwait(false);

            if (!isSupported)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Control interface not found [feature={GetType().Name}]");
                return false;
            }

            // 进一步尝试实际读取一次，确保接口可正常工作
            _ = await GetStateAsync().ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Supported [feature={GetType().Name}]");

            return true;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to check support [feature={GetType().Name}]", ex);

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

        var (stateType, level) = await WMI.LenovoLightingMethod.GetLightingCurrentStatusAsync(lightingID).ConfigureAwait(false);
        var result = FromInternal(stateType, level);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State is {result} [feature={GetType().Name}]");

        return result;
    }

    /// <inheritdoc/>
    public async Task SetStateAsync(T state)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting state to {state}... [feature={GetType().Name}]");

        var (stateType, level) = ToInternal(state);

        await WMI.LenovoLightingMethod.SetLightingCurrentStatusAsync(lightingID, stateType, level).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Set state to {state} [feature={GetType().Name}]");
    }

    // 将 WMI 返回的 (状态类型, 亮度等级) 转换为状态枚举
    protected abstract T FromInternal(int stateType, int level);

    // 将状态枚举转换为 WMI 所需的 (状态类型, 亮度等级)
    protected abstract (int stateType, int level) ToInternal(T state);
}

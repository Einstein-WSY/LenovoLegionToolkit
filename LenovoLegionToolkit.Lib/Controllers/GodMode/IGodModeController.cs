using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Controllers.GodMode;

/// <summary>
/// 自定义模式（God Mode）控制器接口：负责预设的读取/保存/应用与默认值管理。
/// </summary>
public interface IGodModeController
{
    /// <summary>
    /// 当前激活的预设发生变化时触发，参数为新的预设 ID。
    /// </summary>
    event EventHandler<Guid> PresetChanged;

    /// <summary>
    /// 应用自定义模式时是否需要先禁用 Lenovo Vantage（避免其覆盖设置）。
    /// </summary>
    Task<bool> NeedsVantageDisabledAsync();

    /// <summary>
    /// 应用自定义模式时是否需要先禁用 Legion Zone。
    /// </summary>
    Task<bool> NeedsLegionZoneDisabledAsync();

    /// <summary>
    /// 获取当前激活预设的 ID。
    /// </summary>
    Task<Guid> GetActivePresetIdAsync();

    /// <summary>
    /// 获取当前激活预设的名称。
    /// </summary>
    Task<string?> GetActivePresetNameAsync();

    /// <summary>
    /// 读取完整的自定义模式状态（全部预设 + 激活项）。
    /// </summary>
    Task<GodModeState> GetStateAsync();

    /// <summary>
    /// 保存自定义模式状态到设置存储。
    /// </summary>
    /// <param name="state">要保存的状态。</param>
    Task SetStateAsync(GodModeState state);

    /// <summary>
    /// 将当前激活预设实际写入硬件（功耗墙、风扇曲线等）。
    /// </summary>
    Task ApplyStateAsync();

    /// <summary>
    /// 获取默认风扇曲线表。
    /// </summary>
    Task<FanTable> GetDefaultFanTableAsync();

    /// <summary>
    /// 获取允许的最小风扇曲线表（用于校验与 UI 下限）。
    /// </summary>
    Task<FanTable> GetMinimumFanTableAsync();

    /// <summary>
    /// 读取其他电源模式（安静/均衡/性能）下的默认参数，供"复制/参考"使用。
    /// </summary>
    Task<Dictionary<PowerModeState, GodModeDefaults>> GetDefaultsInOtherPowerModesAsync();

    /// <summary>
    /// 把指定电源模式的默认参数写入自定义模式（恢复默认）。
    /// </summary>
    /// <param name="state">要恢复默认值的电源模式。</param>
    Task RestoreDefaultsInOtherPowerModeAsync(PowerModeState state);
}

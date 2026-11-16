using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Controllers.GodMode;

/// <summary>
/// V2 自定义模式实现：所有参数统一通过"能力 ID + 值"的通用接口（LenovoOtherMethod）读写，
/// 机型支持哪些参数完全由 WMI 返回的能力数据决定，因此适配性更好。
/// V2 同时与 Vantage 和 Legion Zone 冲突。
/// </summary>
public class GodModeControllerV2(
    GodModeSettings settings,
    VantageDisabler vantageDisabler,
    LegionZoneDisabler legionZoneDisabler)
    : AbstractGodModeController(settings)
{
    public override Task<bool> NeedsVantageDisabledAsync() => Task.FromResult(true);
    public override Task<bool> NeedsLegionZoneDisabledAsync() => Task.FromResult(true);

    public override async Task ApplyStateAsync()
    {
        // 两个官方软件都会争抢同一套 WMI 设置，运行期间写入无效
        if (await vantageDisabler.GetStatusAsync().ConfigureAwait(false) == SoftwareStatus.Enabled)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Can't correctly apply state when Vantage is running.");
            return;
        }

        if (await legionZoneDisabler.GetStatusAsync().ConfigureAwait(false) == SoftwareStatus.Enabled)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Can't correctly apply state when Legion Zone is running.");
            return;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Applying state...");

        var (presetId, preset) = await GetActivePresetAsync().ConfigureAwait(false);

        // 待写入的预设值；值为 null 表示该预设未设置该项，需要回退到"性能模式"的默认值
        var settings = new Dictionary<CapabilityID, StepperValue?>
        {
            { CapabilityID.CPULongTermPowerLimit, preset.CPULongTermPowerLimit },
            { CapabilityID.CPUShortTermPowerLimit, preset.CPUShortTermPowerLimit },
            { CapabilityID.CPUPeakPowerLimit, preset.CPUPeakPowerLimit },
            { CapabilityID.CPUCrossLoadingPowerLimit, preset.CPUCrossLoadingPowerLimit },
            { CapabilityID.CPUPL1Tau, preset.CPUPL1Tau },
            { CapabilityID.APUsPPTPowerLimit, preset.APUsPPTPowerLimit },
            { CapabilityID.CPUTemperatureLimit, preset.CPUTemperatureLimit },
            { CapabilityID.GPUPowerBoost, preset.GPUPowerBoost },
            { CapabilityID.GPUConfigurableTGP, preset.GPUConfigurableTGP },
            { CapabilityID.GPUTemperatureLimit, preset.GPUTemperatureLimit },
            { CapabilityID.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline, preset.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline },
            { CapabilityID.GPUToCPUDynamicBoost, preset.GPUToCPUDynamicBoost },
        };

        var defaultPresets = await GetDefaultsInOtherPowerModesAsync().ConfigureAwait(false);
        var defaultPerformancePreset = defaultPresets.GetValueOrNull(PowerModeState.Performance);

        // 兜底默认值：未自定义的项沿用性能模式，保证硬件不会停留在未定义状态
        var defaultPerformanceSettings = new Dictionary<CapabilityID, int?>
        {
            { CapabilityID.CPULongTermPowerLimit, defaultPerformancePreset?.CPULongTermPowerLimit },
            { CapabilityID.CPUShortTermPowerLimit, defaultPerformancePreset?.CPUShortTermPowerLimit },
            { CapabilityID.CPUPeakPowerLimit, defaultPerformancePreset?.CPUPeakPowerLimit },
            { CapabilityID.CPUCrossLoadingPowerLimit, defaultPerformancePreset?.CPUCrossLoadingPowerLimit },
            { CapabilityID.CPUPL1Tau, defaultPerformancePreset?.CPUPL1Tau },
            { CapabilityID.APUsPPTPowerLimit, defaultPerformancePreset?.APUsPPTPowerLimit },
            { CapabilityID.CPUTemperatureLimit, defaultPerformancePreset?.CPUTemperatureLimit },
            { CapabilityID.GPUPowerBoost, defaultPerformancePreset?.GPUPowerBoost },
            { CapabilityID.GPUConfigurableTGP, defaultPerformancePreset?.GPUConfigurableTGP  },
            { CapabilityID.GPUTemperatureLimit, defaultPerformancePreset?.GPUTemperatureLimit },
            { CapabilityID.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline, defaultPerformancePreset?.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline },
            { CapabilityID.GPUToCPUDynamicBoost, defaultPerformancePreset?.GPUToCPUDynamicBoost },
        };

        // GPU 相关参数在无独显/驱动不支持的机型上可能写入失败，此类失败不视为致命错误
        var failAllowedSettings = new[]
        {
            CapabilityID.GPUPowerBoost,
            CapabilityID.GPUConfigurableTGP,
            CapabilityID.GPUTemperatureLimit,
            CapabilityID.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline,
            CapabilityID.GPUToCPUDynamicBoost,
        };

        var fanTable = preset.FanTable ?? await GetDefaultFanTableAsync().ConfigureAwait(false);
        var fanFullSpeed = preset.FanFullSpeed ?? false;

        foreach (var (id, value) in settings)
        {
            if (value.HasValue)
            {
                try
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Applying {id}: {value}...");

                    await SetValueAsync(id, value.Value).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Failed to apply {id}. [value={value}]", ex);

                    if (!failAllowedSettings.Contains(id))
                        throw;
                }
            }
            else if (defaultPerformanceSettings.GetValueOrDefault(id) is { } defaultPerformanceValue)
            {
                try
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Applying default {id}: {defaultPerformanceValue}...");

                    await SetValueAsync(id, defaultPerformanceValue).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Failed to apply default {id}. [value={defaultPerformanceValue}]", ex);

                    if (!failAllowedSettings.Contains(id))
                        throw;
                }
            }
            else
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to apply {id}, because neither value nor default value was available.");
            }
        }

        if (fanFullSpeed)
        {
            try
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Applying Fan Full Speed {fanFullSpeed}...");

                await SetFanFullSpeedAsync(fanFullSpeed).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Apply failed. [setting=fanFullSpeed]", ex);
                throw;
            }
        }
        else
        {
            try
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Making sure Fan Full Speed is false...");

                await SetFanFullSpeedAsync(false).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Apply failed. [setting=fanFullSpeed]", ex);
                throw;
            }

            try
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Applying Fan Table {fanTable}...");

                if (!await IsValidFanTableAsync(fanTable).ConfigureAwait(false))
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Fan table invalid, replacing with default...");

                    fanTable = await GetDefaultFanTableAsync().ConfigureAwait(false);
                }

                await SetFanTable(fanTable).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Apply failed. [setting=fanTable]", ex);
                throw;
            }
        }

        RaisePresetChanged(presetId);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State applied. [name={preset.Name}, id={presetId}]");
    }

    /// <summary>
    /// V2 的最小风扇曲线：前 8 档最低为 1，后两档为 3/5。
    /// </summary>
    public override Task<FanTable> GetMinimumFanTableAsync()
    {
        var fanTable = new FanTable([1, 1, 1, 1, 1, 1, 1, 1, 3, 5]);
        return Task.FromResult(fanTable);
    }

    /// <summary>
    /// 从能力数据（LenovoCapabilityData01）中读取各电源模式下的默认参数。
    /// 能力 ID 的高字节被编码为电源模式，因此需要先按模式调整 ID。
    /// </summary>
    public override async Task<Dictionary<PowerModeState, GodModeDefaults>> GetDefaultsInOtherPowerModesAsync()
    {
        try
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Getting defaults in other power modes...");

            var result = new Dictionary<PowerModeState, GodModeDefaults>();

            var allCapabilityData = await WMI.LenovoCapabilityData01.ReadAsync().ConfigureAwait(false);
            allCapabilityData = allCapabilityData.ToArray();

            foreach (var powerMode in new[] { PowerModeState.Quiet, PowerModeState.Balance, PowerModeState.Performance })
            {
                var defaults = new GodModeDefaults
                {
                    CPULongTermPowerLimit = GetDefaultCapabilityIdValueInPowerMode(allCapabilityData, CapabilityID.CPULongTermPowerLimit, powerMode),
                    CPUShortTermPowerLimit = GetDefaultCapabilityIdValueInPowerMode(allCapabilityData, CapabilityID.CPUShortTermPowerLimit, powerMode),
                    CPUPeakPowerLimit = GetDefaultCapabilityIdValueInPowerMode(allCapabilityData, CapabilityID.CPUPeakPowerLimit, powerMode),
                    CPUCrossLoadingPowerLimit = GetDefaultCapabilityIdValueInPowerMode(allCapabilityData, CapabilityID.CPUCrossLoadingPowerLimit, powerMode),
                    CPUPL1Tau = GetDefaultCapabilityIdValueInPowerMode(allCapabilityData, CapabilityID.CPUPL1Tau, powerMode),
                    APUsPPTPowerLimit = GetDefaultCapabilityIdValueInPowerMode(allCapabilityData, CapabilityID.APUsPPTPowerLimit, powerMode),
                    CPUTemperatureLimit = GetDefaultCapabilityIdValueInPowerMode(allCapabilityData, CapabilityID.CPUTemperatureLimit, powerMode),
                    GPUPowerBoost = GetDefaultCapabilityIdValueInPowerMode(allCapabilityData, CapabilityID.GPUPowerBoost, powerMode),
                    GPUConfigurableTGP = GetDefaultCapabilityIdValueInPowerMode(allCapabilityData, CapabilityID.GPUConfigurableTGP, powerMode),
                    GPUTemperatureLimit = GetDefaultCapabilityIdValueInPowerMode(allCapabilityData, CapabilityID.GPUTemperatureLimit, powerMode),
                    GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline = GetDefaultCapabilityIdValueInPowerMode(allCapabilityData, CapabilityID.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline, powerMode),
                    GPUToCPUDynamicBoost = GetDefaultCapabilityIdValueInPowerMode(allCapabilityData, CapabilityID.GPUToCPUDynamicBoost, powerMode),
                    FanTable = await GetDefaultFanTableAsync().ConfigureAwait(false),
                    FanFullSpeed = false
                };

                result[powerMode] = defaults;
            }

            if (Log.Instance.IsTraceEnabled)
            {
                Log.Instance.Trace($"Defaults in other power modes retrieved:");
                foreach (var (powerMode, defaults) in result)
                    Log.Instance.Trace($" - {powerMode}: {defaults}");
            }

            return result;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to get defaults in other power modes.", ex);

            return [];
        }
    }

    // V2 的恢复默认通过"清空预设值 + 应用"实现（未设置的项会自动回退到性能模式默认值），故此处无需操作
    public override Task RestoreDefaultsInOtherPowerModeAsync(PowerModeState _) => Task.CompletedTask;

    /// <summary>
    /// 依据 WMI 返回的能力数据构造默认预设：
    /// 区间能力（Step/范围）来自 CapabilityData01，离散候选值来自 DiscreteData。
    /// </summary>
    protected override async Task<GodModePreset> GetDefaultStateAsync()
    {
        var allCapabilityData = await WMI.LenovoCapabilityData01.ReadAsync().ConfigureAwait(false);
        allCapabilityData = allCapabilityData.ToArray();

        var capabilityData = allCapabilityData
            .Where(d => Enum.IsDefined(d.Id))
            .ToArray();

        var allDiscreteData = await WMI.LenovoDiscreteData.ReadAsync().ConfigureAwait(false);
        allDiscreteData = allDiscreteData.ToArray();

        var discreteData = allDiscreteData
            .Where(d => Enum.IsDefined(d.Id))
            .GroupBy(d => d.Id, d => d.Value, (id, values) => (id, values))
            .ToDictionary(d => d.id, d => d.values.ToArray());

        var stepperValues = new Dictionary<CapabilityID, StepperValue>();

        foreach (var c in capabilityData)
        {
            // 优先使用硬件当前值，读取失败则退回能力数据中的默认值
            var value = await GetValueAsync(c.Id).OrNullIfException().ConfigureAwait(false) ?? c.DefaultValue;
            var steps = discreteData.GetValueOrDefault(c.Id) ?? [];

            // 既无步进又无离散候选值的项无法调节，直接跳过
            if (c.Step == 0 && steps.Length < 1)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Skipping {c.Id}... [idRaw={(int)c.Id:X}, defaultValue={c.DefaultValue}, min={c.Min}, max={c.Max}, step={c.Step}, steps={string.Join(", ", steps)}]");

                continue;
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Creating StepperValue {c.Id}... [idRaw={(int)c.Id:X}, defaultValue={c.DefaultValue}, min={c.Min}, max={c.Max}, step={c.Step}, steps={string.Join(", ", steps)}]");

            var stepperValue = new StepperValue(value, c.Min, c.Max, c.Step, steps, c.DefaultValue);
            stepperValues[c.Id] = stepperValue;
        }

        var fanTableData = await GetFanTableDataAsync().ConfigureAwait(false);

        var preset = new GodModePreset
        {
            Name = "Default",
            CPULongTermPowerLimit = stepperValues.GetValueOrNull(CapabilityID.CPULongTermPowerLimit),
            CPUShortTermPowerLimit = stepperValues.GetValueOrNull(CapabilityID.CPUShortTermPowerLimit),
            CPUPeakPowerLimit = stepperValues.GetValueOrNull(CapabilityID.CPUPeakPowerLimit),
            CPUCrossLoadingPowerLimit = stepperValues.GetValueOrNull(CapabilityID.CPUCrossLoadingPowerLimit),
            CPUPL1Tau = stepperValues.GetValueOrNull(CapabilityID.CPUPL1Tau),
            APUsPPTPowerLimit = stepperValues.GetValueOrNull(CapabilityID.APUsPPTPowerLimit),
            CPUTemperatureLimit = stepperValues.GetValueOrNull(CapabilityID.CPUTemperatureLimit),
            GPUPowerBoost = stepperValues.GetValueOrNull(CapabilityID.GPUPowerBoost),
            GPUConfigurableTGP = stepperValues.GetValueOrNull(CapabilityID.GPUConfigurableTGP),
            GPUTemperatureLimit = stepperValues.GetValueOrNull(CapabilityID.GPUTemperatureLimit),
            GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline = stepperValues.GetValueOrNull(CapabilityID.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline),
            GPUToCPUDynamicBoost = stepperValues.GetValueOrNull(CapabilityID.GPUToCPUDynamicBoost),
            FanTableInfo = fanTableData is null ? null : new FanTableInfo(fanTableData, await GetDefaultFanTableAsync().ConfigureAwait(false)),
            FanFullSpeed = await GetFanFullSpeedAsync().ConfigureAwait(false),
            MinValueOffset = 0,
            MaxValueOffset = 0
        };

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Default state retrieved: {preset}");

        return preset;
    }

    // 能力 ID 的低字节在 V2 中表示电源模式：清除低字节后填入 (模式+1)<<8，得到"某模式下的该参数"ID
    private static CapabilityID AdjustCapabilityIdForPowerMode(CapabilityID id, PowerModeState powerMode)
    {
        var idRaw = (uint)id & 0xFFFF00FF;
        var powerModeRaw = ((uint)powerMode + 1) << 8;
        return (CapabilityID)(idRaw + powerModeRaw);
    }

    private static int? GetDefaultCapabilityIdValueInPowerMode(IEnumerable<RangeCapability> capabilities, CapabilityID id, PowerModeState powerMode)
    {
        var adjustedId = AdjustCapabilityIdForPowerMode(id, powerMode);
        var value = capabilities
            .Where(c => c.Id == adjustedId)
            .Select(c => c.DefaultValue)
            .DefaultIfEmpty(-1)
            .First();
        return value < 0 ? null : value;
    }

    #region Get/Set Value

    // 0xFF00 是"模式无关"的占位字节，实际读写时必须清零，否则固件找不到对应能力
    private static Task<int> GetValueAsync(CapabilityID id)
    {
        var idRaw = (uint)id & 0xFFFF00FF;
        return WMI.LenovoOtherMethod.GetFeatureValueAsync(idRaw);
    }

    private static Task SetValueAsync(CapabilityID id, StepperValue value) => SetValueAsync(id, value.Value);

    private static Task SetValueAsync(CapabilityID id, int value)
    {
        var idRaw = (uint)id & 0xFFFF00FF;
        return WMI.LenovoOtherMethod.SetFeatureValueAsync(idRaw, value);
    }

    #endregion

    #region Fan Table

    /// <summary>
    /// 读取指定电源模式（默认自定义模式）的风扇表数据，并映射风扇/传感器编号到类型。
    /// 任何一条数据编号未知或档位数不为 10 时返回 null。
    /// </summary>
    private static async Task<FanTableData[]?> GetFanTableDataAsync(PowerModeState powerModeState = PowerModeState.GodMode)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Reading fan table data...");

        var data = await WMI.LenovoFanTableData.ReadAsync().ConfigureAwait(false);

        var fanTableData = data
            // 固件模式编号从 1 开始，而枚举从 0 开始，故 +1 过滤
            .Where(d => d.mode == (int)powerModeState + 1)
            .Select(d =>
            {
                // V2 的风扇/传感器编号与 V1 不同，映射表按 V2 机型定义
                var type = (d.fanId, d.sensorId) switch
                {
                    (1, 4) => FanTableType.CPU,
                    (1, 1) => FanTableType.CPUSensor,
                    (2, 5) => FanTableType.GPU,
                    (3, 5) => FanTableType.GPU2,
                    _ => FanTableType.Unknown,
                };
                return new FanTableData(type, d.fanId, d.sensorId, d.fanTableData, d.sensorTableData);
            })
            .ToArray();

        var length = fanTableData.Where(ftd => ftd.Type != FanTableType.Unknown).Count();

        if (fanTableData.Length != length)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Bad fan table length: {string.Join(", ", fanTableData)}");

            return null;
        }

        if (fanTableData.Count(ftd => ftd.FanSpeeds.Length == 10) != length)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Bad fan table fan speeds length: {string.Join(", ", fanTableData)}");

            return null;
        }

        if (fanTableData.Count(ftd => ftd.Temps.Length == 10) != length)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Bad fan table temps length: {string.Join(", ", fanTableData)}");

            return null;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Fan table data: {string.Join(", ", fanTableData)}");

        return fanTableData;
    }

    private static Task SetFanTable(FanTable fanTable) => WMI.LenovoFanMethod.FanSetTableAsync(fanTable.GetBytes());

    #endregion

    #region Fan Full Speed

    // 风扇全速同样走通用能力接口，值为非 0 即表示开启
    private static async Task<bool> GetFanFullSpeedAsync()
    {
        var value = await WMI.LenovoOtherMethod.GetFeatureValueAsync(CapabilityID.FanFullSpeed).ConfigureAwait(false);
        return value != 0;
    }

    private static Task SetFanFullSpeedAsync(bool enabled) => WMI.LenovoOtherMethod.SetFeatureValueAsync(CapabilityID.FanFullSpeed, enabled ? 1 : 0);

    #endregion

}

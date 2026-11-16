using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Controllers.GodMode;

/// <summary>
/// 自定义模式控制器基类：负责预设的持久化读写、参数校验与默认值生成；
/// 与硬件交互的部分（应用功耗墙、读写风扇表等）由子类按版本实现。
/// </summary>
public abstract class AbstractGodModeController(GodModeSettings settings)
    : IGodModeController
{
    public event EventHandler<Guid>? PresetChanged;

    public abstract Task<bool> NeedsVantageDisabledAsync();

    public abstract Task<bool> NeedsLegionZoneDisabledAsync();

    /// <summary>
    /// 激活预设 ID 直接取自设置存储。
    /// </summary>
    public Task<Guid> GetActivePresetIdAsync() => Task.FromResult(settings.Store.ActivePresetId);

    /// <summary>
    /// 从设置存储中查找激活预设的名称；不存在时返回 null。
    /// </summary>
    public Task<string?> GetActivePresetNameAsync()
    {
        var store = settings.Store;
        var name = store.Presets
            .Where(p => p.Key == store.ActivePresetId)
            .Select(p => p.Value.Name)
            .FirstOrDefault();
        return Task.FromResult(name);
    }

    /// <summary>
    /// 读取自定义模式状态；若存储为空或已损坏（激活项缺失），则生成一份默认预设并返回。
    /// </summary>
    public async Task<GodModeState> GetStateAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting state...");

        var store = settings.Store;
        var defaultState = await GetDefaultStateAsync().ConfigureAwait(false);

        if (!IsValidStore(store))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Loading default state...");

            // 新建设置时生成一个随机 ID 的默认预设，保证 UI 至少有一个可用预设
            var id = Guid.NewGuid();
            return new GodModeState
            {
                ActivePresetId = id,
                Presets = new Dictionary<Guid, GodModePreset> { { id, defaultState } }.AsReadOnlyDictionary()
            };
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Loading state from store...");

        return await LoadStateFromStoreAsync(store, defaultState).ConfigureAwait(false);
    }

    /// <summary>
    /// 把运行时预设转换为可持久化的存储结构并保存；风扇表只保存二进制表本体。
    /// </summary>
    public Task SetStateAsync(GodModeState state)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting state...");

        var activePresetId = state.ActivePresetId;
        var presets = new Dictionary<Guid, GodModeSettings.GodModeSettingsStore.Preset>();

        foreach (var (id, preset) in state.Presets)
        {
            presets.Add(id, new()
            {
                Name = preset.Name,
                CPULongTermPowerLimit = preset.CPULongTermPowerLimit,
                CPUShortTermPowerLimit = preset.CPUShortTermPowerLimit,
                CPUPeakPowerLimit = preset.CPUPeakPowerLimit,
                CPUCrossLoadingPowerLimit = preset.CPUCrossLoadingPowerLimit,
                CPUPL1Tau = preset.CPUPL1Tau,
                APUsPPTPowerLimit = preset.APUsPPTPowerLimit,
                CPUTemperatureLimit = preset.CPUTemperatureLimit,
                GPUPowerBoost = preset.GPUPowerBoost,
                GPUConfigurableTGP = preset.GPUConfigurableTGP,
                GPUTemperatureLimit = preset.GPUTemperatureLimit,
                GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline = preset.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline,
                GPUToCPUDynamicBoost = preset.GPUToCPUDynamicBoost,
                FanTable = preset.FanTableInfo?.Table,
                FanFullSpeed = preset.FanFullSpeed,
                MinValueOffset = preset.MinValueOffset,
                MaxValueOffset = preset.MaxValueOffset,
            });
        }

        settings.Store.ActivePresetId = activePresetId;
        settings.Store.Presets = presets;
        settings.SynchronizeStore();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State saved.");

        return Task.CompletedTask;
    }

    public abstract Task ApplyStateAsync();

    /// <summary>
    /// 返回固定递增的默认风扇曲线（1..10），作为无自定义曲线时的基线。
    /// </summary>
    public Task<FanTable> GetDefaultFanTableAsync()
    {
        var fanTable = new FanTable([1, 2, 3, 4, 5, 6, 7, 8, 9, 10]);
        return Task.FromResult(fanTable);
    }

    public abstract Task<FanTable> GetMinimumFanTableAsync();

    public abstract Task<Dictionary<PowerModeState, GodModeDefaults>> GetDefaultsInOtherPowerModesAsync();

    public abstract Task RestoreDefaultsInOtherPowerModeAsync(PowerModeState state);

    /// <summary>
    /// 生成当前机型的默认预设（各项功耗/温度的默认档位）。
    /// </summary>
    protected abstract Task<GodModePreset> GetDefaultStateAsync();

    /// <summary>
    /// 通知订阅者激活预设已变更。
    /// </summary>
    /// <param name="presetId">新的激活预设 ID。</param>
    protected void RaisePresetChanged(Guid presetId) => PresetChanged?.Invoke(this, presetId);

    /// <summary>
    /// 获取激活预设；若存储无效会先重建默认状态再返回。
    /// </summary>
    /// <returns>激活预设的 ID 与存储内容。</returns>
    /// <exception cref="InvalidOperationException">激活预设 ID 在集合中不存在时抛出。</exception>
    protected async Task<(Guid, GodModeSettings.GodModeSettingsStore.Preset)> GetActivePresetAsync()
    {
        if (!IsValidStore(settings.Store))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Invalid store, generating default one.");

            var state = await GetStateAsync().ConfigureAwait(false);
            await SetStateAsync(state).ConfigureAwait(false);
        }

        var activePresetId = settings.Store.ActivePresetId;
        var presets = settings.Store.Presets;

        if (presets.TryGetValue(activePresetId, out var activePreset))
            return (activePresetId, activePreset);

        throw new InvalidOperationException($"Preset with ID {activePresetId} not found");
    }

    /// <summary>
    /// 校验风扇曲线：每个档位不得低于最小曲线，且不得超过 10。
    /// </summary>
    protected async Task<bool> IsValidFanTableAsync(FanTable fanTable)
    {
        var minimumFanTable = await GetMinimumFanTableAsync().ConfigureAwait(false);
        var minimum = minimumFanTable.GetTable();
        return fanTable.GetTable().Where((t, i) => t < minimum[i] || t > 10u).IsEmpty();
    }

    // 存储有效的基本条件：至少有一个预设，且激活 ID 存在于预设集合中
    private static bool IsValidStore(GodModeSettings.GodModeSettingsStore store) => store.Presets.Count != 0 && store.Presets.ContainsKey(store.ActivePresetId);

    // 把存储中的旧预设与当前机型默认值合并：缺省项回退默认值，越界值自动修正
    private async Task<GodModeState> LoadStateFromStoreAsync(GodModeSettings.GodModeSettingsStore store, GodModePreset defaultState)
    {
        var states = new Dictionary<Guid, GodModePreset>();

        foreach (var (id, preset) in store.Presets)
        {
            states.Add(id, new GodModePreset
            {
                Name = preset.Name,
                CPULongTermPowerLimit = CreateStepperValue(defaultState.CPULongTermPowerLimit, preset.CPULongTermPowerLimit, preset.MinValueOffset, preset.MaxValueOffset),
                CPUShortTermPowerLimit = CreateStepperValue(defaultState.CPUShortTermPowerLimit, preset.CPUShortTermPowerLimit, preset.MinValueOffset, preset.MaxValueOffset),
                CPUPeakPowerLimit = CreateStepperValue(defaultState.CPUPeakPowerLimit, preset.CPUPeakPowerLimit, preset.MinValueOffset, preset.MaxValueOffset),
                CPUCrossLoadingPowerLimit = CreateStepperValue(defaultState.CPUCrossLoadingPowerLimit, preset.CPUCrossLoadingPowerLimit, preset.MinValueOffset, preset.MaxValueOffset),
                CPUPL1Tau = CreateStepperValue(defaultState.CPUPL1Tau, preset.CPUPL1Tau, preset.MinValueOffset, preset.MaxValueOffset),
                APUsPPTPowerLimit = CreateStepperValue(defaultState.APUsPPTPowerLimit, preset.APUsPPTPowerLimit, preset.MinValueOffset, preset.MaxValueOffset),
                CPUTemperatureLimit = CreateStepperValue(defaultState.CPUTemperatureLimit, preset.CPUTemperatureLimit, preset.MinValueOffset, preset.MaxValueOffset),
                GPUPowerBoost = CreateStepperValue(defaultState.GPUPowerBoost, preset.GPUPowerBoost, preset.MinValueOffset, preset.MaxValueOffset),
                GPUConfigurableTGP = CreateStepperValue(defaultState.GPUConfigurableTGP, preset.GPUConfigurableTGP, preset.MinValueOffset, preset.MaxValueOffset),
                GPUTemperatureLimit = CreateStepperValue(defaultState.GPUTemperatureLimit, preset.GPUTemperatureLimit, preset.MinValueOffset, preset.MaxValueOffset),
                GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline = CreateStepperValue(defaultState.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline,
                    preset.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline,
                    preset.MinValueOffset,
                    preset.MaxValueOffset),
                GPUToCPUDynamicBoost = CreateStepperValue(defaultState.GPUToCPUDynamicBoost, preset.GPUToCPUDynamicBoost),
                FanTableInfo = await GetFanTableInfoAsync(preset, defaultState.FanTableInfo?.Data).ConfigureAwait(false),
                FanFullSpeed = preset.FanFullSpeed,
                MinValueOffset = preset.MinValueOffset ?? defaultState.MinValueOffset,
                MaxValueOffset = preset.MaxValueOffset ?? defaultState.MaxValueOffset
            });
        }

        return new GodModeState
        {
            ActivePresetId = store.ActivePresetId,
            Presets = states.AsReadOnlyDictionary()
        };
    }

    /// <summary>
    /// 构造可调数值项：优先沿用存储中的值，并根据机型偏移修正取值范围。
    /// </summary>
    /// <param name="state">当前机型的默认值定义（提供范围与步进）。</param>
    /// <param name="store">之前保存的值，可为 null。</param>
    /// <param name="minValueOffset">最小值偏移（不同机型的下限修正）。</param>
    /// <param name="maxValueOffset">最大值偏移（不同机型的上限修正）。</param>
    /// <returns>修正后的可调数值项；默认定义无效时返回 null。</returns>
    private static StepperValue? CreateStepperValue(StepperValue? state, StepperValue? store = null, int? minValueOffset = 0, int? maxValueOffset = 0)
    {
        if (state is not { } stateValue)
            return null;

        // 情况一：只允许离散取值（Steps 列表），越界值就近吸附到最近的合法档位
        if (stateValue.Steps.Length > 0)
        {
            var value = store?.Value ?? stateValue.Value;
            var steps = stateValue.Steps;
            var defaultValue = stateValue.DefaultValue;

            if (!steps.Contains(value))
            {
                var valueTemp = value;
                value = steps.MinBy(v => Math.Abs((long)v - valueTemp));
            }

            return new(value, 0, 0, 0, steps, defaultValue);
        }

        // 情况二：连续区间 + 步进，先按步进取整再钳制到 [min, max]
        if (stateValue.Step > 0)
        {
            var value = store?.Value ?? stateValue.Value;
            var min = Math.Max(0, stateValue.Min + (minValueOffset ?? 0));
            var max = stateValue.Max + (maxValueOffset ?? 0);
            var step = stateValue.Step;
            var defaultValue = stateValue.DefaultValue;

            value = MathExtensions.RoundNearest(value, step);

            // 越界时优先回退到默认值，其次钳制到边界
            if (value < min || value > max)
                value = defaultValue ?? Math.Clamp(value, min, max);

            return new(value, min, max, step, [], defaultValue);
        }

        return null;
    }

    // 组装风扇曲线信息；数据缺失或曲线非法时回退到默认曲线
    private async Task<FanTableInfo?> GetFanTableInfoAsync(GodModeSettings.GodModeSettingsStore.Preset preset, FanTableData[]? fanTableData)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting fan table info...");

        if (fanTableData is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Fan table data is null");
            return null;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Fan table data retrieved: {fanTableData}");

        var fanTable = preset.FanTable ?? await GetDefaultFanTableAsync().ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Fan table retrieved: {fanTable}");

        if (!await IsValidFanTableAsync(fanTable).ConfigureAwait(false))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Fan table invalid, replacing with default...");

            fanTable = await GetDefaultFanTableAsync().ConfigureAwait(false);
        }

        return new FanTableInfo(fanTableData, fanTable);
    }
}

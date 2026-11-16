using System;
using System.Collections.Generic;

namespace LenovoLegionToolkit.Lib.Settings;


/// <summary>
/// 自定义模式（God Mode）设置：持久化到 godmode.json，保存多个超频/功耗配置预设及当前激活的预设。
/// </summary>
public class GodModeSettings() : AbstractSettings<GodModeSettings.GodModeSettingsStore>("godmode.json")
{
    /// <summary>
    /// godmode.json 的数据结构。
    /// </summary>
    public class GodModeSettingsStore
    {
        /// <summary>
        /// 单个自定义模式预设：记录名称、各功耗/温度限制与风扇曲线。
        /// 所有限制值均可为空，表示该机型不支持或用户未设置该项。
        /// </summary>
        public class Preset
        {
            /// <summary>
            /// 预设名称。
            /// </summary>
            public string Name { get; init; } = string.Empty;

            /// <summary>
            /// CPU 长时功耗限制（PL1）。
            /// </summary>
            public StepperValue? CPULongTermPowerLimit { get; init; }

            /// <summary>
            /// CPU 短时功耗限制（PL2）。
            /// </summary>
            public StepperValue? CPUShortTermPowerLimit { get; init; }

            /// <summary>
            /// CPU 峰值功耗限制。
            /// </summary>
            public StepperValue? CPUPeakPowerLimit { get; init; }

            /// <summary>
            /// CPU 交叉负载功耗限制。
            /// </summary>
            public StepperValue? CPUCrossLoadingPowerLimit { get; init; }

            /// <summary>
            /// CPU PL1 时间窗口（Tau）。
            /// </summary>
            public StepperValue? CPUPL1Tau { get; init; }

            /// <summary>
            /// APU 持续功耗限制（sPPT）。
            /// </summary>
            public StepperValue? APUsPPTPowerLimit { get; init; }

            /// <summary>
            /// CPU 温度上限。
            /// </summary>
            public StepperValue? CPUTemperatureLimit { get; init; }

            /// <summary>
            /// GPU 功耗提升值。
            /// </summary>
            public StepperValue? GPUPowerBoost { get; init; }

            /// <summary>
            /// GPU 可配置 TGP。
            /// </summary>
            public StepperValue? GPUConfigurableTGP { get; init; }

            /// <summary>
            /// GPU 温度上限。
            /// </summary>
            public StepperValue? GPUTemperatureLimit { get; init; }

            /// <summary>
            /// 接入电源时 GPU 总处理功耗相对基线的偏移。
            /// </summary>
            public StepperValue? GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline { get; init; }

            /// <summary>
            /// GPU 到 CPU 的动态加速设置。
            /// </summary>
            public StepperValue? GPUToCPUDynamicBoost { get; init; }

            /// <summary>
            /// 自定义风扇转速表。
            /// </summary>
            public FanTable? FanTable { get; init; }

            /// <summary>
            /// 是否开启风扇全速模式。
            /// </summary>
            public bool? FanFullSpeed { get; init; }

            /// <summary>
            /// 自定义模式取值范围的最小偏移量。
            /// </summary>
            public int? MinValueOffset { get; init; }

            /// <summary>
            /// 自定义模式取值范围的最大偏移量。
            /// </summary>
            public int? MaxValueOffset { get; init; }
        }

        /// <summary>
        /// 当前激活的预设 ID。
        /// </summary>
        public Guid ActivePresetId { get; set; }

        /// <summary>
        /// 全部预设，按 ID 索引。
        /// </summary>
        public Dictionary<Guid, Preset> Presets { get; set; } = [];
    }

    // ReSharper disable once StringLiteralTypo
}

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// LENOVO_DEFAULT_VALUE_IN_DIFFERENT_MODE_DATA 数据表封装：
    /// 记录各散热/性能模式下 CPU 与 GPU 功耗、温度限制的默认值，用于"恢复默认"操作。
    /// </summary>
    public static class LenovoDefaultValueInDifferentModeData
    {
        /// <summary>
        /// 单个模式下的默认功耗与温度限制数据。
        /// </summary>
        /// <param name="mode">散热/性能模式编号。</param>
        /// <param name="cpuLongTermPowerLimit">CPU 长时功耗墙默认值。</param>
        /// <param name="cpuShortTermPowerLimit">CPU 短时功耗墙默认值。</param>
        /// <param name="cpuPeakPowerLimit">CPU 峰值功耗默认值。</param>
        /// <param name="cpuCrossLoadingPowerLimit">CPU 交叉负载功耗默认值。</param>
        /// <param name="apUsPPTPowerLimit">APU sPPT 功耗默认值（AMD 平台）。</param>
        /// <param name="cpuTemperatureLimit">CPU 温度控制点默认值。</param>
        /// <param name="gpuPowerBoost">GPU 动态加速（PPAB）功耗默认值。</param>
        /// <param name="gpuConfigurableTGP">GPU 基础功耗（cTGP）默认值。</param>
        /// <param name="gpuTemperatureLimit">GPU 温度限制默认值。</param>
        public readonly struct Data(
            int mode,
            int cpuLongTermPowerLimit,
            int cpuShortTermPowerLimit,
            int cpuPeakPowerLimit,
            int cpuCrossLoadingPowerLimit,
            int apUsPPTPowerLimit,
            int cpuTemperatureLimit,
            int gpuPowerBoost,
            int gpuConfigurableTGP,
            int gpuTemperatureLimit)
        {
            /// <summary>散热/性能模式编号。</summary>
            public int Mode { get; } = mode;
            /// <summary>CPU 长时功耗墙默认值。</summary>
            public int CPULongTermPowerLimit { get; } = cpuLongTermPowerLimit;
            /// <summary>CPU 短时功耗墙默认值。</summary>
            public int CPUShortTermPowerLimit { get; } = cpuShortTermPowerLimit;
            /// <summary>CPU 峰值功耗默认值。</summary>
            public int CPUPeakPowerLimit { get; } = cpuPeakPowerLimit;
            /// <summary>CPU 交叉负载功耗默认值。</summary>
            public int CPUCrossLoadingPowerLimit { get; } = cpuCrossLoadingPowerLimit;
            /// <summary>APU sPPT 功耗默认值。</summary>
            public int APUsPPTPowerLimit { get; } = apUsPPTPowerLimit;
            /// <summary>CPU 温度控制点默认值。</summary>
            public int CPUTemperatureLimit { get; } = cpuTemperatureLimit;
            /// <summary>GPU 动态加速（PPAB）功耗默认值。</summary>
            public int GPUPowerBoost { get; } = gpuPowerBoost;
            /// <summary>GPU 基础功耗（cTGP）默认值。</summary>
            public int GPUConfigurableTGP { get; } = gpuConfigurableTGP;
            /// <summary>GPU 温度限制默认值。</summary>
            public int GPUTemperatureLimit { get; } = gpuTemperatureLimit;
        }

        /// <summary>
        /// 读取所有模式的默认值数据。
        /// </summary>
        /// <returns>每个模式一条默认值记录。</returns>
        public static Task<IEnumerable<Data>> ReadAsync() => WMI.ReadAsync("root\\WMI",
            $"SELECT * FROM LENOVO_DEFAULT_VALUE_IN_DIFFERENT_MODE_DATA ",
            pdc => new Data(Convert.ToInt32(pdc["mode"].Value),
                Convert.ToInt32(pdc["DefaultLongTermPowerlimit"].Value),
                Convert.ToInt32(pdc["DefaultShortTermPowerlimit"].Value),
                Convert.ToInt32(pdc["DefaultPeakPowerLimit"].Value),
                Convert.ToInt32(pdc["DefaultCpuCrossLoading"].Value),
                Convert.ToInt32(pdc["DefaultAPUsPPTPowerLimit"].Value),
                Convert.ToInt32(pdc["DefaultTemperatueControl"].Value),
                Convert.ToInt32(pdc["Default_PPAB_Powerlimit"].Value),
                Convert.ToInt32(pdc["Default_cTGP_Powerlimit"].Value),
                Convert.ToInt32(pdc["DefaultTemperatueLimit"].Value)));
    }
}

using System;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// LENOVO_CPU_METHOD 类的封装：CPU 功耗墙（PL1/PL2/Peak）与温度控制点的读写。
    /// 写操作会直接改变固件功耗策略，调用方需自行校验取值范围。
    /// </summary>
    public static class LenovoCpuMethod
    {
        /// <summary>
        /// 读取 CPU 长/短时功耗墙的默认值。
        /// </summary>
        /// <returns>元组：长时（PL1）、短时（PL2）默认功耗。</returns>
        public static Task<(int longTerm, int shortTerm)> CPUGetDefaultPowerLimitAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_CPU_METHOD",
            "CPU_Get_Default_PowerLimit",
            [],
            pdc =>
            {
                var longTerm = Convert.ToInt32(pdc["DefaultLongTermPowerlimit"].Value);
                var shortTerm = Convert.ToInt32(pdc["DefaultShortTermPowerlimit"].Value);
                return (longTerm, shortTerm);
            });

        /// <summary>
        /// 读取 CPU 长时功耗墙（PL1）的当前值及可调范围。
        /// </summary>
        /// <returns>元组：当前值、最小值、最大值、步进。</returns>
        public static Task<(int value, int min, int max, int step)> CPUGetLongTermPowerLimitAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_CPU_METHOD",
            "CPU_Get_LongTerm_PowerLimit",
            [],
            pdc =>
            {
                var value = Convert.ToInt32(pdc["CurrentLongTerm_PowerLimit"].Value);
                var min = Convert.ToInt32(pdc["MinLongTerm_PowerLimit"].Value);
                var max = Convert.ToInt32(pdc["MaxLongTerm_PowerLimit"].Value);
                var step = Convert.ToInt32(pdc["step"].Value);

                return (value, min, max, step);
            });

        /// <summary>
        /// 设置 CPU 长时功耗墙（PL1）。
        /// </summary>
        /// <param name="value">目标功耗值。</param>
        public static Task CPUSetLongTermPowerLimitAsync(int value) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_CPU_METHOD",
            "CPU_Set_LongTerm_PowerLimit",
            new() { { "value", value } });

        /// <summary>
        /// 读取 CPU 短时功耗墙（PL2）的当前值及可调范围。
        /// </summary>
        /// <returns>元组：当前值、最小值、最大值、步进。</returns>
        public static Task<(int value, int min, int max, int step)> CPUGetShortTermPowerLimitAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_CPU_METHOD",
            "CPU_Get_ShortTerm_PowerLimit",
            [],
            pdc =>
            {
                var value = Convert.ToInt32(pdc["CurrentShortTerm_PowerLimit"].Value);
                var min = Convert.ToInt32(pdc["MinShortTerm_PowerLimit"].Value);
                var max = Convert.ToInt32(pdc["MaxShortTerm_PowerLimit"].Value);
                var step = Convert.ToInt32(pdc["step"].Value);

                return (value, min, max, step);
            });

        /// <summary>
        /// 设置 CPU 短时功耗墙（PL2）。
        /// </summary>
        /// <param name="value">目标功耗值。</param>
        public static Task CPUSetShortTermPowerLimitAsync(int value) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_CPU_METHOD",
            "CPU_Set_ShortTerm_PowerLimit",
            new() { { "value", value } });

        /// <summary>
        /// 读取 CPU 峰值功耗（Peak）的当前值、可调范围与默认值。
        /// </summary>
        /// <returns>元组：当前值、最小值、最大值、步进、默认值。</returns>
        public static Task<(int value, int min, int max, int step, int defaultValue)> CPUGetPeakPowerLimitAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_CPU_METHOD",
            "CPU_Get_Peak_PowerLimit",
            [],
            pdc =>
            {
                var value = Convert.ToInt32(pdc["CurrentPeakPowerLimit"].Value);
                var min = Convert.ToInt32(pdc["MinPeakPowerLimit"].Value);
                var max = Convert.ToInt32(pdc["MaxPeakPowerLimit"].Value);
                var step = Convert.ToInt32(pdc["step"].Value);
                var defaultValue = Convert.ToInt32(pdc["DefaultPeakPowerLimit"].Value);

                return (value, min, max, step, defaultValue);
            });

        /// <summary>
        /// 设置 CPU 峰值功耗（Peak）。
        /// </summary>
        /// <param name="value">目标功耗值。</param>
        public static Task CPUSetPeakPowerLimitAsync(int value) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_CPU_METHOD",
            "CPU_Set_Peak_PowerLimit",
            new() { { "CurrentPeakPowerLimit", value } });

        /// <summary>
        /// 读取 CPU 交叉负载功耗限制（Cross Loading）的当前值、范围与默认值。
        /// </summary>
        /// <returns>元组：当前值、最小值、最大值、步进、默认值。</returns>
        public static Task<(int value, int min, int max, int step, int defaultValue)> CPUGetCrossLoadingPowerLimitAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_CPU_METHOD",
            "CPU_Get_Cross_Loading_PowerLimit",
            [],
            pdc =>
            {
                var value = Convert.ToInt32(pdc["CurrentCpuCrossLoading"].Value);
                var min = Convert.ToInt32(pdc["MinCpuCrossLoading"].Value);
                var max = Convert.ToInt32(pdc["MaxCpuCrossLoading"].Value);
                var step = Convert.ToInt32(pdc["step"].Value);
                var defaultValue = Convert.ToInt32(pdc["DefaultCpuCrossLoading"].Value);

                return (value, min, max, step, defaultValue);
            });

        /// <summary>
        /// 设置 CPU 交叉负载功耗限制。
        /// </summary>
        /// <param name="value">目标功耗值。</param>
        public static Task CPUSetCrossLoadingPowerLimitAsync(int value) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_CPU_METHOD",
            "CPU_Set_Cross_Loading_PowerLimit",
            new() { { "CurrentCpuCrossLoading", value } });

        /// <summary>
        /// 读取 APU 的 sPPT 功耗限制（AMD 平台）。
        /// </summary>
        /// <returns>元组：当前值、最小值、最大值、步进、默认值。</returns>
        public static Task<(int value, int min, int max, int step, int defaultValue)> GetAPUSPPTPowerLimitAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_CPU_METHOD",
            "Get_APU_sPPT_PowerLimit",
            [],
            pdc =>
            {
                var value = Convert.ToInt32(pdc["CurrenAPUsPPTPowerLimit"].Value);
                var min = Convert.ToInt32(pdc["MinAPUsPPTPowerLimit"].Value);
                var max = Convert.ToInt32(pdc["MaxAPUsPPTPowerLimit"].Value);
                var step = Convert.ToInt32(pdc["step"].Value);
                var defaultValue = Convert.ToInt32(pdc["DefaultAPUsPPTPowerLimit"].Value);

                return (value, min, max, step, defaultValue);
            });

        /// <summary>
        /// 设置 APU 的 sPPT 功耗限制（AMD 平台）。
        /// </summary>
        /// <param name="value">目标功耗值。</param>
        public static Task SetAPUSPPTPowerLimitAsync(int value) => WMI.CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_CPU_METHOD",
            "Set_APU_sPPT_PowerLimit",
            new() { { "CurrentAPUsPPTPowerLimit", value } });

        /// <summary>
        /// 读取 CPU 温度控制点（温控目标温度）的当前值、范围与默认值。
        /// </summary>
        /// <returns>元组：当前值、最小值、最大值、步进、默认值。</returns>
        public static Task<(int value, int min, int max, int step, int defaultValue)> CPUGetTemperatureControlAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_CPU_METHOD",
            "CPU_Get_Temperature_Control",
            [],
            pdc =>
            {
                // 固件字段名中 "Temperatue" 为原始拼写（缺字母 r），不可修正，否则属性查找失败
                var value = Convert.ToInt32(pdc["CurrentTemperatueControl"].Value);
                var min = Convert.ToInt32(pdc["MinTemperatueControl"].Value);
                var max = Convert.ToInt32(pdc["MaxTemperatueControl"].Value);
                var step = Convert.ToInt32(pdc["step"].Value);
                var defaultValue = Convert.ToInt32(pdc["DefaultTemperatueControl"].Value);

                return (value, min, max, step, defaultValue);
            });

        /// <summary>
        /// 设置 CPU 温度控制点。
        /// </summary>
        /// <param name="value">目标温度。</param>
        public static Task CPUSetTemperatureControlAsync(int value) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_CPU_METHOD",
            "CPU_Set_Temperature_Control",
            new() { { "CurrentTemperatureControl", value } });
    }
}

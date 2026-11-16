

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

using System;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// LENOVO_GPU_METHOD 类的封装：独立显卡功耗墙（cTGP、PPAB）与温度限制的读写。
    /// </summary>
    public static class LenovoGpuMethod
    {
        /// <summary>
        /// 读取 GPU 的 cTGP 与 PPAB 功耗墙默认值。
        /// </summary>
        /// <returns>元组：默认 cTGP 功耗、默认 PPAB（动态加速）功耗。</returns>
        public static Task<(int ctgp, int ppab)> GPUGetDefaultPPABcTGPPowerLimit() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GPU_METHOD",
            "GPU_Get_Default_PPAB_cTGP_PowerLimit",
            [],
            pdc =>
            {
                var ctgp = Convert.ToInt32(pdc["Default_cTGP_Powerlimit"].Value);
                var ppab = Convert.ToInt32(pdc["Default_PPAB_Powerlimit"].Value);
                return (ctgp, ppab);
            });

        /// <summary>
        /// 读取 GPU 基础功耗墙（cTGP）的当前值及可调范围。
        /// </summary>
        /// <returns>元组：当前值、最小值、最大值、步进。</returns>
        public static Task<(int value, int min, int max, int step)> GPUGetCTGPPowerLimitAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GPU_METHOD",
            "GPU_Get_cTGP_PowerLimit",
            [],
            pdc =>
            {
                var value = Convert.ToInt32(pdc["Current_cTGP_PowerLimit"].Value);
                var min = Convert.ToInt32(pdc["Min_cTGP_PowerLimit"].Value);
                var max = Convert.ToInt32(pdc["Max_cTGP_PowerLimit"].Value);
                var step = Convert.ToInt32(pdc["step"].Value);

                return (value, min, max, step);
            });

        /// <summary>
        /// 设置 GPU 基础功耗墙（cTGP）。
        /// </summary>
        /// <param name="value">目标功耗值。</param>
        public static Task GPUSetCTGPPowerLimitAsync(int value) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GPU_METHOD",
            "GPU_Set_cTGP_PowerLimit",
            new() { { "value", value } });

        /// <summary>
        /// 读取 GPU 动态加速功耗墙（PPAB）的当前值及可调范围。
        /// </summary>
        /// <returns>元组：当前值、最小值、最大值、步进。</returns>
        public static Task<(int value, int min, int max, int step)> GPUGetPPABPowerLimitAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GPU_METHOD",
            "GPU_Get_PPAB_PowerLimit",
            [],
            pdc =>
            {
                var value = Convert.ToInt32(pdc["CurrentPPAB_PowerLimit"].Value);
                var min = Convert.ToInt32(pdc["MinPPAB_PowerLimit"].Value);
                var max = Convert.ToInt32(pdc["MaxPPAB_PowerLimit"].Value);
                var step = Convert.ToInt32(pdc["step"].Value);

                return (value, min, max, step);
            });

        /// <summary>
        /// 设置 GPU 动态加速功耗墙（PPAB）。
        /// </summary>
        /// <param name="value">目标功耗值。</param>
        public static Task GPUSetPPABPowerLimitAsync(int value) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GPU_METHOD",
            "GPU_Set_PPAB_PowerLimit",
            new() { { "value", value } });

        /// <summary>
        /// 读取 GPU 温度限制的当前值、范围与默认值。
        /// </summary>
        /// <returns>元组：当前值、最小值、最大值、步进、默认值。</returns>
        public static Task<(int value, int min, int max, int step, int defaultValue)> GPUGetTemperatureLimitAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GPU_METHOD",
            "GPU_Get_Temperature_Limit",
            [],
            pdc =>
            {
                // 字段名同样保留了固件原始拼写 "Temperatue"
                var value = Convert.ToInt32(pdc["CurrentTemperatueLimit"].Value);
                var min = Convert.ToInt32(pdc["MinTemperatueLimit"].Value);
                var max = Convert.ToInt32(pdc["MaxTemperatueLimit"].Value);
                var step = Convert.ToInt32(pdc["step"].Value);
                var defaultValue = Convert.ToInt32(pdc["DefaultTemperatueLimit"].Value);

                return (value, min, max, step, defaultValue);
            });

        /// <summary>
        /// 设置 GPU 温度限制。
        /// </summary>
        /// <param name="value">目标温度。</param>
        public static Task GPUSetTemperatureLimitAsync(int value) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GPU_METHOD",
            "GPU_Set_Temperature_Limit",
            new() { { "CurrentTemperatureLimit", value } });
    }
}

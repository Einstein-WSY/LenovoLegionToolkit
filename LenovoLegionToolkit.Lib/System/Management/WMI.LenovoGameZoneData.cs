using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// LENOVO_GAMEZONE_DATA 类的封装。
    /// 该类由联想 WMI 驱动提供，位于 root\WMI 命名空间，除特别说明外所有方法均通过它调用固件功能。
    /// 各方法返回值通常为 0/1 表示关闭/开启或不支持/支持。
    /// </summary>
    public static partial class LenovoGameZoneData
    {
        // 从硬件 ID 字符串中提取 PCI 厂商号（VEN_xxxx）与设备号（DEV_xxxx）
        [GeneratedRegex("PCIVEN_([0-9A-F]{4})|DEV_([0-9A-F]{4})")]
        private static partial Regex DGPUHWIdRegex();

        /// <summary>
        /// 判断当前机型是否提供 LENOVO_GAMEZONE_DATA 的 WMI 接口。
        /// </summary>
        /// <returns>存在返回 true。</returns>
        public static Task<bool> ExistsAsync() => WMI.ExistsAsync("root\\WMI", $"SELECT * FROM LENOVO_GAMEZONE_DATA");

        /// <summary>
        /// 查询是否支持智能风扇模式。
        /// </summary>
        /// <returns>1 表示支持，0 表示不支持。</returns>
        public static Task<int> IsSupportSmartFanAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "IsSupportSmartFan",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        /// <summary>
        /// 获取当前智能风扇模式。
        /// </summary>
        /// <returns>模式枚举值（含义见 SmartFanMode）。</returns>
        public static Task<int> GetSmartFanModeAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "GetSmartFanMode",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        /// <summary>
        /// 设置智能风扇模式。
        /// </summary>
        /// <param name="data">目标模式值。</param>
        public static Task SetSmartFanModeAsync(int data) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "SetSmartFanMode",
            new() { { "Data", data } });

        /// <summary>
        /// 获取智能模式的子模式（平衡/性能等细分档位）。
        /// </summary>
        /// <returns>子模式值。</returns>
        public static Task<int> GetIntelligentSubModeAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "GetIntelligentSubMode",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        /// <summary>
        /// 设置智能模式的子模式。
        /// </summary>
        /// <param name="data">子模式值。</param>
        public static Task SetIntelligentSubModeAsync(int data) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "SetIntelligentSubMode",
            new() { { "Data", data } });

        /// <summary>
        /// 查询是否支持 G-Sync。
        /// </summary>
        /// <returns>1 表示支持。</returns>
        public static Task<int> IsSupportGSyncAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "IsSupportGSync",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        /// <summary>
        /// 获取 G-Sync 当前状态。
        /// </summary>
        /// <returns>1 表示已开启。</returns>
        public static Task<int> GetGSyncStatusAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "GetGSyncStatus",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        /// <summary>
        /// 设置 G-Sync 开关。
        /// </summary>
        /// <param name="data">1 开启，0 关闭。</param>
        public static Task SetGSyncStatusAsync(int data) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "SetGSyncStatus",
            new() { { "Data", data } });

        /// <summary>
        /// 查询是否支持纯核显模式（iGPU only）。
        /// </summary>
        /// <returns>1 表示支持。</returns>
        public static Task<int> IsSupportIGPUModeAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "IsSupportIGPUMode",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        /// <summary>
        /// 获取核显模式当前状态。
        /// </summary>
        /// <returns>模式值。</returns>
        public static Task<int> GetIGPUModeStatusAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "GetIGPUModeStatus",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        /// <summary>
        /// 设置核显模式（通常需重启生效）。
        /// </summary>
        /// <param name="mode">目标模式值。</param>
        public static Task SetIGPUModeStatusAsync(int mode) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "SetIGPUModeStatus",
            new() { { "mode", mode } });

        /// <summary>
        /// 通知固件独立显卡（DGPU）当前的使用状态，避免固件误判后强制切换显卡模式。
        /// </summary>
        /// <param name="status">显卡状态值。</param>
        public static Task NotifyDGPUStatusAsync(int status) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "NotifyDGPUStatus",
            new() { { "Status", status } });

        /// <summary>
        /// 获取独立显卡的硬件 ID（PCI 厂商号与设备号）。
        /// </summary>
        /// <returns>硬件 ID；固件返回格式不匹配时返回 <see cref="HardwareId.Empty"/>。</returns>
        public static Task<HardwareId> GetDGPUHWIdAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "GetDGPUHWId",
            [],
            pdc =>
            {
                var id = pdc["Data"].Value.ToString();

                if (id is null)
                    return HardwareId.Empty;

                try
                {
                    // 期望匹配到 VEN_xxxx 与 DEV_xxxx 两段
                    var matches = DGPUHWIdRegex().Matches(id);
                    if (matches.Count != 2)
                        return HardwareId.Empty;

                    var vendor = matches[0].Groups[1].Value;
                    var device = matches[1].Groups[2].Value;

                    return new HardwareId(vendor, device);
                }
                catch
                {
                    return HardwareId.Empty;
                }
            });

        /// <summary>
        /// 查询是否支持显卡超频（OC）。
        /// </summary>
        /// <returns>1 表示支持。</returns>
        public static Task<int> IsSupportGpuOCAsync() => CallAsync("ROOT\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "IsSupportGpuOC",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        /// <summary>
        /// 查询是否支持禁用触摸板。
        /// </summary>
        /// <returns>1 表示支持。</returns>
        public static Task<int> IsSupportDisableTPAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "IsSupportDisableTP",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        /// <summary>
        /// 获取触摸板状态。
        /// </summary>
        /// <returns>1 表示已启用。</returns>
        public static Task<int> GetTPStatusStatusAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "GetTPStatus",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        /// <summary>
        /// 设置触摸板开关。
        /// </summary>
        /// <param name="data">1 启用，0 禁用。</param>
        public static Task SetTPStatusAsync(int data) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "SetTPStatus",
            new() { { "Data", data } });

        /// <summary>
        /// 查询是否支持禁用 Win 键。
        /// </summary>
        /// <returns>1 表示支持。</returns>
        public static Task<int> IsSupportDisableWinKeyAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "IsSupportDisableWinKey",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        /// <summary>
        /// 获取 Win 键状态。
        /// </summary>
        /// <returns>1 表示可用。</returns>
        public static Task<int> GetWinKeyStatusAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "GetWinKeyStatus",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        /// <summary>
        /// 设置 Win 键开关。
        /// </summary>
        /// <param name="data">1 启用，0 禁用。</param>
        public static Task SetWinKeyStatusAsync(int data) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "SetWinKeyStatus",
            new() { { "Data", data } });

        /// <summary>
        /// 查询是否支持显卡超频档位（OD，Over Drive）。
        /// </summary>
        /// <returns>1 表示支持。</returns>
        public static Task<int> IsSupportODAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "IsSupportOD",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        /// <summary>
        /// 获取显卡超频档位状态。
        /// </summary>
        /// <returns>档位值。</returns>
        public static Task<int> GetODStatusAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "GetODStatus",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        /// <summary>
        /// 设置显卡超频档位。
        /// </summary>
        /// <param name="data">档位值。</param>
        public static Task SetODStatusAsync(int data) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "SetODStatus",
            new() { { "Data", data } });

        /// <summary>
        /// 设置灯效控制权归属（本程序或固件），避免双方同时控制灯效产生冲突。
        /// </summary>
        /// <param name="data">控制方标识。</param>
        public static Task SetLightControlOwnerAsync(int data) => CallAsync("ROOT\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "SetLightControlOwner",
            new() { { "Data", data } });

        /// <summary>
        /// 查询电源适配器是否满足超频供电要求。
        /// </summary>
        /// <returns>1 表示满足。</returns>
        public static Task<int> IsACFitForOCAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "IsACFitForOC",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        /// <summary>
        /// 获取当前充电模式（快充/标准/养护等）。
        /// </summary>
        /// <returns>充电模式值。</returns>
        public static Task<int> GetPowerChargeModeAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "GetPowerChargeMode",
            [],
            pdc => Convert.ToInt32(pdc["Data"].Value));

        /// <summary>
        /// 获取 CPU 当前频率（MHz）。
        /// </summary>
        /// <returns>CPU 频率。</returns>
        public static Task<int> GetCPUFrequencyAsync() => WMI.CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_DATA",
            "GetCPUFrequency",
            [],
            pdc =>
            {
                // 固件把两个核心/两种统计的频率打包进一个 32 位值：低 16 位与高 16 位各一个读数
                var value = Convert.ToInt32(pdc["Data"].Value);
                var low = value & 0xFFFF;
                var high = value >> 16;
                // 取较大者作为当前频率
                return Math.Max(low, high);
            });
    }
}

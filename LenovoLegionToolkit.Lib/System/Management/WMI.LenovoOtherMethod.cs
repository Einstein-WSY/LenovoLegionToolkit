using System;
using System.Threading.Tasks;

// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// LENOVO_OTHER_METHOD 类的封装：散热模式支持、功能能力（Feature/Capability）读写、
    /// 独立显卡状态与硬件 ID 等杂项固件能力。
    /// </summary>
    public static class LenovoOtherMethod
    {
        /// <summary>
        /// 查询固件支持的散热模式位掩码。
        /// </summary>
        /// <returns>位掩码，每一位对应一种散热模式。</returns>
        public static Task<int> GetSupportThermalModeAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "GetSupportThermalMode",
            [],
            pdc => Convert.ToInt32(pdc["mode"].Value));

        /// <summary>
        /// 查询固件要求的 Legion Zone 支持版本。
        /// </summary>
        /// <returns>版本号。</returns>
        public static Task<int> GetSupportLegionZoneVersionAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "Get_Support_LegionZone_Version",
            [],
            pdc => Convert.ToInt32(pdc["Version"].Value));

        /// <summary>
        /// 获取本机支持的功能特性集合。
        /// </summary>
        /// <returns>特性状态值。</returns>
        public static Task<int> GetLegionDeviceSupportFeatureAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "Get_Legion_Device_Support_Feature",
            [],
            pdc => Convert.ToInt32(pdc["Status"].Value));

        /// <summary>
        /// 获取设备当前生效的功能特性。
        /// </summary>
        /// <returns>特性标志位。</returns>
        public static Task<int> GetDeviceCurrentSupportFeatureAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "Get_Device_Current_Support_Feature",
            [],
            pdc => Convert.ToInt32(pdc["Flag"].Value));

        /// <summary>
        /// 设置指定功能 ID 的当前特性值。
        /// </summary>
        /// <param name="functionId">功能 ID。</param>
        /// <param name="value">目标值。</param>
        /// <returns>固件返回码（ret）。</returns>
        public static Task<int> SetDeviceCurrentSupportFeatureAsync(int functionId, int value) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "Set_Device_Current_Support_Feature",
            new()
            {
                { "FunctionID", functionId },
                { "value", value }
            },
            pdc => Convert.ToInt32(pdc["ret"].Value));

        /// <summary>
        /// 通知固件独立显卡的启用/禁用状态（常用于切换混合模式前后）。
        /// </summary>
        /// <param name="status">true 表示启用。</param>
        public static Task SetDGPUDeviceStatusAsync(bool status) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "Set_DGPU_Device_Status",
            new() { { "Status", status ? 1 : 0 } });

        /// <summary>
        /// 获取独立显卡的 DID/VID（设备号与厂商号）。
        /// </summary>
        /// <returns>硬件 ID；固件把两者打包在同一个 32 位整数中。</returns>
        public static Task<HardwareId> GetDGPUDeviceDIDVIDAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "Get_DGPU_Device_DIDVID",
            [],
            pdc =>
            {
                // 低 16 位为厂商号（VID），高 16 位为设备号（DID）
                var id = Convert.ToInt32(pdc["DGPU_ID"].Value);
                var vendorId = id & 0xFFFF;
                var deviceId = id >> 16;
                return new HardwareId($"{vendorId:X}", $"{deviceId:X}");
            });

        /// <summary>
        /// 读取指定能力 ID 的当前取值。
        /// </summary>
        /// <param name="id">能力 ID。</param>
        /// <returns>能力当前值。</returns>
        public static Task<int> GetFeatureValueAsync(CapabilityID id) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "GetFeatureValue",
            new() { { "IDs", (int)id } },
            pdc => Convert.ToInt32(pdc["Value"].Value));

        /// <summary>
        /// 设置指定能力 ID 的取值。
        /// </summary>
        /// <param name="id">能力 ID。</param>
        /// <param name="value">目标值。</param>
        public static Task SetFeatureValueAsync(CapabilityID id, int value) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "SetFeatureValue",
            new()
            {
                { "IDs", (int)id },
                { "value", value }
            });

        /// <summary>
        /// 以原始 ID 读取能力取值（用于枚举中未定义的扩展能力）。
        /// </summary>
        /// <param name="idRaw">原始能力 ID。</param>
        /// <returns>能力当前值。</returns>
        public static Task<int> GetFeatureValueAsync(uint idRaw) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "GetFeatureValue",
            new() { { "IDs", idRaw } },
            pdc => Convert.ToInt32(pdc["Value"].Value));

        /// <summary>
        /// 以原始 ID 设置能力取值。
        /// </summary>
        /// <param name="idRaw">原始能力 ID。</param>
        /// <param name="value">目标值。</param>
        public static Task SetFeatureValueAsync(uint idRaw, int value) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_OTHER_METHOD",
            "SetFeatureValue",
            new()
            {
                { "IDs", idRaw },
                { "value", value }
            });
    }
}

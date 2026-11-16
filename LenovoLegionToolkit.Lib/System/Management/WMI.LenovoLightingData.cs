using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// LENOVO_LIGHTING_DATA 数据表封装：记录机型支持的灯效设备及其控制接口类型。
    /// </summary>
    public static class LenovoLightingData
    {
        /// <summary>
        /// 判断指定的灯效设备（照明 ID、控制接口、灯效类型）是否受支持。
        /// </summary>
        /// <param name="lightingId">灯效设备 ID。</param>
        /// <param name="controlInterface">控制接口类型。</param>
        /// <param name="type">灯效类型。</param>
        /// <returns>受支持返回 true。</returns>
        public static Task<bool> ExistsAsync(int lightingId, int controlInterface, int type) =>
            WMI.ExistsAsync("root\\WMI", $"SELECT * FROM LENOVO_LIGHTING_DATA WHERE Lighting_ID = {lightingId} AND Control_Interface = {controlInterface} AND Lighting_Type = {type}");
    }
}

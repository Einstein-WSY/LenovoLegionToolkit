using System;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// LENOVO_LIGHTING_METHOD 类的封装：灯效（四分区键盘背光等）状态与亮度等级的读写。
    /// </summary>
    public static class LenovoLightingMethod
    {
        /// <summary>
        /// 获取指定灯效设备的当前状态。
        /// </summary>
        /// <param name="lightingId">灯效设备 ID。</param>
        /// <returns>元组：状态类型（如常亮/呼吸/关闭）、当前亮度等级。</returns>
        public static Task<(int stateType, int level)> GetLightingCurrentStatusAsync(int lightingId) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_LIGHTING_METHOD",
            "Get_Lighting_Current_Status",
            new() { { "Lighting_ID", lightingId } },
            pdc =>
            {
                var stateType = Convert.ToInt32(pdc["Current_State_Type"].Value);
                var level = Convert.ToInt32(pdc["Current_Brightness_Level"].Value);
                return (stateType, level);
            });

        /// <summary>
        /// 设置指定灯效设备的状态与亮度等级。
        /// </summary>
        /// <param name="lightingId">灯效设备 ID。</param>
        /// <param name="stateType">状态类型。</param>
        /// <param name="level">亮度等级。</param>
        public static Task SetLightingCurrentStatusAsync(int lightingId, int stateType, int level) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_LIGHTING_METHOD",
            "Set_Lighting_Current_Status",
            new()
            {
                { "Lighting_ID", lightingId },
                { "Current_State_Type", stateType },
                { "Current_Brightness_Level", level }
            });
    }
}

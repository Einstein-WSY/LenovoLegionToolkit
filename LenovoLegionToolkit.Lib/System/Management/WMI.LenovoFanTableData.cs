using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// LENOVO_FAN_TABLE_DATA 数据表封装：自定义风扇曲线表（温度-转速对应关系）。
    /// </summary>
    public static class LenovoFanTableData
    {
        /// <summary>
        /// 判断指定传感器与风扇组合的风扇表是否存在。
        /// </summary>
        /// <param name="sensorId">温度传感器 ID。</param>
        /// <param name="fanId">风扇 ID。</param>
        /// <returns>存在返回 true。</returns>
        public static Task<bool> ExistsAsync(int sensorId, int fanId) => WMI.ExistsAsync("root\\WMI", $"SELECT * FROM LENOVO_FAN_TABLE_DATA WHERE Sensor_ID = {sensorId} AND Fan_Id = {fanId}");

        /// <summary>
        /// 读取全部风扇曲线表项。
        /// </summary>
        /// <returns>每项包含：模式、风扇 ID、传感器 ID、风扇转速表、传感器温度表。</returns>
        public static Task<IEnumerable<(int mode, byte fanId, byte sensorId, ushort[] fanTableData, ushort[] sensorTableData)>> ReadAsync() => WMI.ReadAsync("root\\WMI",
            $"SELECT * FROM LENOVO_FAN_TABLE_DATA",
            pdc =>
            {
                // Mode 字段为较新固件才提供，缺失时用 -1 标识"未提供"
                var mode = pdc.Contains("Mode") ? Convert.ToInt32(pdc["Mode"].Value) : -1;
                var fanId = Convert.ToByte(pdc["Fan_Id"].Value);
                var sensorId = Convert.ToByte(pdc["Sensor_ID"].Value);
                // 表数据为 ushort 数组，固件未返回时降级为空数组
                var fanTableData = (ushort[]?)pdc["FanTable_Data"].Value ?? [];
                var sensorTableData = (ushort[]?)pdc["SensorTable_Data"].Value ?? [];
                return (mode, fanId, sensorId, fanTableData, sensorTableData);
            });
    }
}

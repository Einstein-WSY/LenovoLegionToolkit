using System;
using System.Linq;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// LENOVO_FAN_METHOD 类的封装：自定义风扇曲线下发、全速模式以及风扇/传感器实时读数。
    /// </summary>
    public static class LenovoFanMethod
    {
        /// <summary>
        /// 下发自定义风扇曲线表。
        /// </summary>
        /// <param name="fanTable">按固件协议打包好的字节数组（传感器温度与风扇转速成对排列）。</param>
        public static Task FanSetTableAsync(byte[] fanTable) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_FAN_METHOD",
            "Fan_Set_Table",
            new() { { "FanTable", fanTable } });

        /// <summary>
        /// 查询是否处于风扇全速模式。
        /// </summary>
        /// <returns>全速返回 true。</returns>
        public static Task<bool> FanGetFullSpeedAsync() => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_FAN_METHOD",
            "Fan_Get_FullSpeed",
            [],
            pdc => (bool)pdc["Status"].Value);

        /// <summary>
        /// 设置风扇全速模式。
        /// </summary>
        /// <param name="status">非 0 表示开启全速。</param>
        public static Task FanSetFullSpeedAsync(int status) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_FAN_METHOD",
            "Fan_Set_FullSpeed",
            new() { { "Status", status } });

        /// <summary>
        /// 读取指定温度传感器的当前温度。
        /// </summary>
        /// <param name="sensorId">传感器 ID。</param>
        /// <returns>温度值（摄氏度）。</returns>
        public static Task<int> FanGetCurrentSensorTemperatureAsync(int sensorId) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_FAN_METHOD",
            "Fan_GetCurrentSensorTemperature",
            new() { { "SensorID", sensorId } },
            pdc => Convert.ToInt32(pdc["CurrentSensorTemperature"].Value));

        /// <summary>
        /// 读取指定风扇的当前转速。
        /// </summary>
        /// <param name="fanId">风扇 ID。</param>
        /// <returns>转速（RPM）。</returns>
        public static Task<int> FanGetCurrentFanSpeedAsync(int fanId) => CallAsync("root\\WMI",
            $"SELECT * FROM LENOVO_FAN_METHOD",
            "Fan_GetCurrentFanSpeed",
            new() { { "FanID", fanId } },
            pdc => Convert.ToInt32(pdc["CurrentFanSpeed"].Value));

        /// <summary>
        /// 获取风扇当前允许的最大转速。
        /// </summary>
        /// <param name="sensorId">传感器 ID。</param>
        /// <param name="fanId">风扇 ID。</param>
        /// <returns>最大转速；查询结果为空时由 Max() 抛异常，调用方需保证组合有效。</returns>
        public static async Task<int> GetCurrentFanMaxSpeedAsync(int sensorId, int fanId)
        {
            var result = await ReadAsync("root\\WMI",
                $"SELECT * FROM LENOVO_FAN_TABLE_DATA WHERE Sensor_ID = {sensorId} AND Fan_Id = {fanId}",
                pdc => Convert.ToInt32(pdc["CurrentFanMaxSpeed"].Value)).ConfigureAwait(false);
            // 同一传感器/风扇可能对应多条记录（不同模式），取最大值作为上限
            return result.Max();
        }

        /// <summary>
        /// 获取风扇出厂默认最大转速。
        /// </summary>
        /// <param name="sensorId">传感器 ID。</param>
        /// <param name="fanID">风扇 ID。</param>
        /// <returns>默认最大转速。</returns>
        public static async Task<int> GetDefaultFanMaxSpeedAsync(int sensorId, int fanID)
        {
            var result = await ReadAsync("root\\WMI",
                $"SELECT * FROM LENOVO_FAN_TABLE_DATA WHERE Sensor_ID = {sensorId} AND Fan_Id = {fanID}",
                pdc => Convert.ToInt32(pdc["DefaultFanMaxSpeed"].Value)).ConfigureAwait(false);
            return result.Max();
        }
    }
}

using System;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// LENOVO_GAMEZONE_THERMAL_MODE_EVENT 事件封装：散热（性能）模式变化时由固件触发。
    /// </summary>
    public static class LenovoGameZoneThermalModeEvent
    {
        /// <summary>
        /// 订阅散热模式变化事件。
        /// </summary>
        /// <param name="handler">回调，参数为 mode 值。</param>
        /// <returns>可释放对象，释放后停止监听。</returns>
        public static IDisposable Listen(Action<int> handler) => WMI.Listen("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_THERMAL_MODE_EVENT",
            pdc =>
            {
                var value = Convert.ToInt32(pdc["mode"].Value);
                handler(value);
            });
    }
}

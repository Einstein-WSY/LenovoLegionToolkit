using System;

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// WmiMonitorBrightnessEvent 事件封装：系统屏幕亮度变化时由 WMI 触发（如用户按亮度键）。
    /// </summary>
    public static class WmiMonitorBrightnessEvent
    {
        /// <summary>
        /// 订阅屏幕亮度变化事件。
        /// </summary>
        /// <param name="handler">回调，参数为当前亮度百分比（0-100）。</param>
        /// <returns>可释放对象，释放后停止监听。</returns>
        public static IDisposable Listen(Action<byte> handler) => WMI.Listen("root\\WMI",
            $"SELECT * FROM WmiMonitorBrightnessEvent",
            pdc =>
            {
                var value = Convert.ToByte(pdc["Brightness"].Value);
                handler(value);
            });
    }
}
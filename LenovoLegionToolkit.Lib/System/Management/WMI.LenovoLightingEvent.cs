using System;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// LENOVO_LIGHTING_EVENT 事件封装：灯效按键事件（键盘上的灯效切换键）触发时通知。
    /// </summary>
    public static class LenovoLightingEvent
    {
        /// <summary>
        /// 订阅灯效按键事件。
        /// </summary>
        /// <param name="handler">回调，参数为 Key_ID（按键标识）。</param>
        /// <returns>可释放对象，释放后停止监听。</returns>
        public static IDisposable Listen(Action<int> handler) => WMI.Listen("root\\WMI",
            $"SELECT * FROM LENOVO_LIGHTING_EVENT",
            pdc =>
            {
                var value = Convert.ToInt32(pdc["Key_ID"].Value);
                handler(value);
            });
    }
}

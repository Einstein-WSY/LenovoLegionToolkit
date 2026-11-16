using System;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// LENOVO_UTILITY_EVENT 事件封装：固件工具事件（如 Fn 组合键按下）触发时通知。
    /// </summary>
    public static class LenovoUtilityEvent
    {
        /// <summary>
        /// 订阅固件工具事件。
        /// </summary>
        /// <param name="handler">回调，参数为 PressTypeDataVal（按键类型值）。</param>
        /// <returns>可释放对象，释放后停止监听。</returns>
        public static IDisposable Listen(Action<int> handler) => WMI.Listen("root\\WMI",
            $"SELECT * FROM LENOVO_UTILITY_EVENT",
            pdc =>
            {
                var value = Convert.ToInt32(pdc["PressTypeDataVal"].Value);
                handler(value);
            });
    }
}

using System;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// LENOVO_GAMEZONE_LIGHT_PROFILE_CHANGE_EVENT 事件封装：灯效配置切换时由固件触发。
    /// </summary>
    public static class LenovoGameZoneLightProfileChangeEvent
    {
        /// <summary>
        /// 订阅灯效配置变化事件。
        /// </summary>
        /// <param name="handler">回调，参数为 EventId 值。</param>
        /// <returns>可释放对象，释放后停止监听。</returns>
        public static IDisposable Listen(Action<int> handler) => WMI.Listen("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_LIGHT_PROFILE_CHANGE_EVENT",
            pdc =>
            {
                var value = Convert.ToInt32(pdc["EventId"].Value);
                handler(value);
            });
    }
}

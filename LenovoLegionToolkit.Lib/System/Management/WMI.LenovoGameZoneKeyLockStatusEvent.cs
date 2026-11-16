using System;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// LENOVO_GAMEZONE_KEYLOCK_STATUS_EVENT 事件封装：Fn 键锁定状态（如 FnLock）发生变化时由固件触发。
    /// </summary>
    public static class LenovoGameZoneKeyLockStatusEvent
    {
        /// <summary>
        /// 订阅按键锁定状态变化事件。
        /// </summary>
        /// <param name="handler">回调，参数为 KeyLockState 值。</param>
        /// <returns>可释放对象，释放后停止监听。</returns>
        public static IDisposable Listen(Action<int> handler) => WMI.Listen("root\\WMI",
            $"SELECT * FROM LENOVO_GAMEZONE_KEYLOCK_STATUS_EVENT",
            pdc =>
            {
                var value = Convert.ToInt32(pdc["KeyLockState"].Value);
                handler(value);
            });
    }
}

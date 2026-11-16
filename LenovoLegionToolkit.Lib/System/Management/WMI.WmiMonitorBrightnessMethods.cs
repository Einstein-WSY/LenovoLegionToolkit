using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// WmiMonitorBrightnessMethods 类封装：通过 WMI 设置屏幕亮度。
    /// 与 WmiMonitorBrightnessEvent 配合可监听/修改系统亮度。
    /// </summary>
    public static class WmiMonitorBrightnessMethods
    {
        /// <summary>
        /// 设置屏幕亮度。
        /// </summary>
        /// <param name="brightness">目标亮度百分比（0-100）。</param>
        /// <param name="timeout">过渡时间（秒），0 表示立即生效。</param>
        public static Task WmiSetBrightness(int brightness, int timeout) => CallAsync("root\\WMI",
            $"SELECT * FROM WmiMonitorBrightnessMethods",
            "WmiSetBrightness",
            new()
            {
                { "Brightness", brightness },
                { "Timeout", timeout }
            });
    }
}

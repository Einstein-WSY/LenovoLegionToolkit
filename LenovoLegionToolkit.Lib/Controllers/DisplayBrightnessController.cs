using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System.Management;

namespace LenovoLegionToolkit.Lib.Controllers;

/// <summary>
/// 屏幕亮度控制器：通过 WMI（WmiMonitorBrightnessMethods）设置内置显示器亮度。
/// </summary>
public class DisplayBrightnessController
{
    /// <summary>
    /// 设置屏幕亮度。
    /// </summary>
    /// <param name="brightness">亮度百分比（0-100）。</param>
    /// <returns>异步任务。</returns>
    public Task SetBrightnessAsync(int brightness) => WMI.WmiMonitorBrightnessMethods.WmiSetBrightness(brightness, 1); // 第二个参数为超时（秒）
}

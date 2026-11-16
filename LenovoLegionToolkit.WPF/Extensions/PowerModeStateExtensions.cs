using System.Windows.Media;
using LenovoLegionToolkit.Lib;

namespace LenovoLegionToolkit.WPF.Extensions;

/// <summary>
/// 提供电源模式状态到界面颜色的映射扩展方法。
/// </summary>
public static class PowerModeStateExtensions
{
    /// <summary>
    /// 获取表示该电源模式的强调色画刷。
    /// </summary>
    /// <param name="powerModeState">电源模式状态。</param>
    /// <returns>对应模式的主题色画刷，未知模式返回透明画刷。</returns>
    public static SolidColorBrush GetSolidColorBrush(this PowerModeState powerModeState) => new(powerModeState switch
    {
        PowerModeState.Quiet => Color.FromRgb(53, 123, 242),
        PowerModeState.Balance => Colors.White,
        PowerModeState.Performance => Color.FromRgb(212, 51, 51),
        PowerModeState.GodMode => Color.FromRgb(99, 52, 227),
        _ => Colors.Transparent,
    });
}

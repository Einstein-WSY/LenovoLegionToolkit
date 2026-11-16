using System.Windows.Media;
using LenovoLegionToolkit.Lib;

namespace LenovoLegionToolkit.WPF.Extensions;

/// <summary>
/// 提供 Lib 层颜色模型到 WPF 颜色类型转换的扩展方法。
/// </summary>
public static class RGBColorExtensions
{
    /// <summary>
    /// 将 <see cref="RGBColor"/> 转换为不透明度的 WPF <see cref="Color"/>。
    /// </summary>
    /// <param name="color">要转换的 RGB 颜色。</param>
    /// <returns>对应的 WPF 颜色。</returns>
    public static Color ToColor(this RGBColor color) => Color.FromRgb(color.R, color.G, color.B);
}

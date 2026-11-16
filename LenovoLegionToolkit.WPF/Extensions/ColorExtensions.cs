using System.Windows.Media;
using LenovoLegionToolkit.Lib;

namespace LenovoLegionToolkit.WPF.Extensions;

/// <summary>
/// 提供 WPF 颜色类型与 Lib 层颜色模型之间转换的扩展方法。
/// </summary>
public static class ColorExtensions
{
    /// <summary>
    /// 将 WPF 的 <see cref="Color"/> 转换为只包含 RGB 分量的 <see cref="RGBColor"/>。
    /// </summary>
    /// <param name="color">要转换的 WPF 颜色。</param>
    /// <returns>丢弃 Alpha 通道后的 RGB 颜色。</returns>
    public static RGBColor ToRGBColor(this Color color) => new(color.R, color.G, color.B);
}
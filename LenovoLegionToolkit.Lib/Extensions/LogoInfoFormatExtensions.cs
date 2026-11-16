using System.Collections.Generic;
using System.Drawing.Imaging;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供开机 Logo 格式（<see cref="BootLogoFormat"/>）相关的扩展方法。
/// </summary>
public static class LogoInfoFormatExtensions
{
    /// <summary>
    /// 根据位标志枚举解析出对应的 <see cref="ImageFormat"/> 集合。
    /// </summary>
    /// <param name="format">开机 Logo 支持的格式标志。</param>
    /// <returns>受支持的图片格式序列（BMP/JPEG/PNG）。</returns>
    public static IEnumerable<ImageFormat> ImageFormats(this BootLogoFormat format)
    {
        if (format.HasFlag(BootLogoFormat.Bmp))
            yield return ImageFormat.Bmp;
        if (format.HasFlag(BootLogoFormat.Jpeg))
            yield return ImageFormat.Jpeg;
        if (format.HasFlag(BootLogoFormat.Png))
            yield return ImageFormat.Png;
    }

    /// <summary>
    /// 根据位标志枚举生成文件选择对话框使用的扩展名过滤器（如 "*.bmp"）。
    /// </summary>
    /// <param name="format">开机 Logo 支持的格式标志。</param>
    /// <returns>形如 "*.png" 的过滤器序列。</returns>
    public static IEnumerable<string> ExtensionFilters(this BootLogoFormat format)
    {
        if (format.HasFlag(BootLogoFormat.Bmp))
            yield return "*.bmp";
        if (format.HasFlag(BootLogoFormat.Png))
            yield return "*.png";
        if (format.HasFlag(BootLogoFormat.Jpeg))
        {
            yield return "*.jpeg";
            yield return "*.jpg";
        }
    }
}

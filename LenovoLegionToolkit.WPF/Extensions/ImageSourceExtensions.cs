using System;
using System.Drawing;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LenovoLegionToolkit.WPF.Extensions;

/// <summary>
/// 提供图像资源加载相关的扩展方法。
/// </summary>
public static class ImageSourceExtensions
{
    /// <summary>
    /// 从可执行文件中提取关联的应用程序图标。
    /// </summary>
    /// <param name="exePath">可执行文件路径；为 <see langword="null"/> 时直接返回 <see langword="null"/>。</param>
    /// <returns>图标对应的 <see cref="ImageSource"/>；提取失败时返回 <see langword="null"/>。</returns>
    public static ImageSource? ApplicationIcon(string? exePath)
    {
        if (exePath is null)
            return null;

        try
        {
            var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon is null)
                return null;

            var imageSource = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            return imageSource;

        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 从当前程序集的嵌入资源中加载图像。
    /// </summary>
    /// <param name="name">资源相对路径。</param>
    /// <returns>解码后的图像源。</returns>
    public static ImageSource FromResource(string name)
    {
        var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
        // 使用 pack URI 访问程序集内的资源，避免依赖磁盘上的绝对路径
        var path = "pack://application:,,,/" + assemblyName + ";component/" + name;
        var uri = new Uri(path, UriKind.RelativeOrAbsolute);
        return BitmapFrame.Create(uri);
    }
}

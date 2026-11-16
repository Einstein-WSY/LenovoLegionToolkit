using System;
using System.Diagnostics;

namespace LenovoLegionToolkit.WPF.Extensions;

/// <summary>
/// 提供 URI 打开操作的扩展方法。
/// </summary>
public static class UriExtensions
{
    /// <summary>
    /// 使用系统默认程序打开该 URI。
    /// </summary>
    /// <param name="uri">要打开的 URI。</param>
    public static void Open(this Uri uri) => Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
}

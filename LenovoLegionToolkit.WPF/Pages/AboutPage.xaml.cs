using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Resources;

namespace LenovoLegionToolkit.WPF.Pages;

/// <summary>
/// “关于”页面：展示版本号、构建时间与版权信息，并提供打开应用数据/临时目录的入口。
/// </summary>
public partial class AboutPage
{
    /// <summary>
    /// 版本文本：取程序集版本，Beta 版本显示为 “BETA”，否则显示三段式版本号。
    /// </summary>
    private static string VersionText
    {
        get
        {
            var version = Assembly.GetEntryAssembly()?.GetName().Version;
            if (version is null)
                return string.Empty;
            if (version.IsBeta())
                return "BETA";
            return version.ToString(3);
        }
    }

    /// <summary>构建时间文本（取自程序集元数据中的构建时间）。</summary>
    private static string BuildText => Assembly.GetEntryAssembly()?.GetBuildDateTimeString() ?? string.Empty;

    /// <summary>
    /// 版权文本：读取入口程序集文件版本信息中的 LegalCopyright。
    /// </summary>
    private static string CopyrightText
    {
        get
        {
            var location = Assembly.GetEntryAssembly()?.Location;
            if (location is null)
                return string.Empty;
            return FileVersionInfo.GetVersionInfo(location).LegalCopyright ?? string.Empty;
        }
    }

    /// <summary>
    /// 初始化页面：加载 XAML 视图并把版本、构建时间、版权写入对应文本控件。
    /// </summary>
    public AboutPage()
    {
        InitializeComponent();

        _version.Text += $" {VersionText}";
        _build.Text += $" {BuildText}";
        _copyright.Text = CopyrightText;

        // 仅当界面语言不是英语时显示翻译致谢区块。
        _translationCredit.Visibility = Resource.Culture.Equals(new CultureInfo("en")) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// 打开应用数据目录；目录不存在时不做处理。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void OpenApplicationDataFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(Folders.AppData))
            return;

        Process.Start("explorer", Folders.AppData);
    }

    /// <summary>
    /// 打开应用临时目录；目录不存在时不做处理。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void OpenApplicationTempFolder_Click(object sender, RoutedEventArgs e)
    {
        if (!Directory.Exists(Folders.Temp))
            return;

        Process.Start("explorer", Folders.Temp);
    }
}

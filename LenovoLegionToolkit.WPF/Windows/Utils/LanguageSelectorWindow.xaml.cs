using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Humanizer;
using LenovoLegionToolkit.WPF.Extensions;

namespace LenovoLegionToolkit.WPF.Windows.Utils;

/// <summary>
/// 语言选择窗口，首次启动时让用户选择界面语言。
/// </summary>
public partial class LanguageSelectorWindow
{
    private readonly TaskCompletionSource<CultureInfo?> _taskCompletionSource = new();

    /// <summary>
    /// 等待用户选择结果的任务；返回 <see langword="null"/> 表示用户未确认（直接关闭）。
    /// </summary>
    public Task<CultureInfo?> ShouldContinue => _taskCompletionSource.Task;

    /// <summary>
    /// 初始化窗口，并按语言名称排序填充下拉框。
    /// </summary>
    /// <param name="languages">可选语言集合。</param>
    /// <param name="defaultLanguage">默认选中的语言。</param>
    public LanguageSelectorWindow(IEnumerable<CultureInfo> languages, CultureInfo defaultLanguage)
    {
        InitializeComponent();

        _languageComboBox.SetItems(languages.OrderBy(ci => ci.Name, StringComparer.InvariantCultureIgnoreCase),
            defaultLanguage,
            cc => cc.NativeName.Transform(cc, To.TitleCase));
    }

    /// <summary>
    /// 窗口关闭时若尚未有结果，则视为取消选择。
    /// </summary>
    private void LanguageSelectorWindow_OnClosed(object? sender, EventArgs e) => _taskCompletionSource.TrySetResult(null);

    /// <summary>
    /// 确认所选语言并结束等待。
    /// </summary>
    private void OK_Click(object sender, RoutedEventArgs e)
    {
        _languageComboBox.TryGetSelectedItem(out CultureInfo? cultureInfo);
        _taskCompletionSource.TrySetResult(cultureInfo);
        Close();
    }
}

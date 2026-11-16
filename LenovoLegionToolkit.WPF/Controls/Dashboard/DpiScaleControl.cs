using System;
using System.Threading.Tasks;
using System.Windows;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;
using Wpf.Ui.Common;
using DpiScale = LenovoLegionToolkit.Lib.DpiScale;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “显示缩放（DPI）”仪表盘卡片控件，以下拉框列出可用缩放比例并切换；
/// 通过显示配置监听器感知显示器配置变化后自动刷新。
/// </summary>
public class DpiScaleControl : AbstractComboBoxFeatureCardControl<DpiScale>
{
    /// <summary>显示配置监听器，用于感知 DPI 缩放等显示设置的变化。</summary>
    private readonly DisplayConfigurationListener _listener = IoCContainer.Resolve<DisplayConfigurationListener>();

    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题、副标题，并订阅显示配置变化事件。
    /// </summary>
    public DpiScaleControl()
    {
        Icon = SymbolRegular.TextFontSize24;
        Title = Resource.DpiScaleControl_Title;
        Subtitle = Resource.DpiScaleControl_Message;

        _listener.Changed += Listener_Changed;
    }

    /// <summary>
    /// 刷新控件状态：可用缩放比例少于两个时隐藏卡片，避免无意义的选择。
    /// </summary>
    /// <returns>异步刷新任务。</returns>
    protected override async Task OnRefreshAsync()
    {
        await base.OnRefreshAsync();

        // 只有一种缩放比例可选时无需展示下拉框，直接折叠卡片。
        Visibility = ItemsCount < 2 ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// 生成下拉项显示文本，并强制从左到右排版以正确显示 “125%” 形式的百分比。
    /// </summary>
    /// <param name="value">DPI 缩放枚举值。</param>
    /// <returns>用于显示的本地化文本。</returns>
    protected override string ComboBoxItemDisplayName(DpiScale value)
    {
        var str = base.ComboBoxItemDisplayName(value);
        // 数字+百分号在 RTL 语言环境下会错位，这里强制按 LTR 显示。
        return LocalizationHelper.ForceLeftToRight(str);
    }

    /// <summary>
    /// 显示配置变化回调：控件已加载时切回 UI 线程重新读取当前缩放比例。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数（无附加数据）。</param>
    private void Listener_Changed(object? sender, EventArgs e) => Dispatcher.Invoke(async () =>
    {
        if (IsLoaded)
            await RefreshAsync();
    });
}

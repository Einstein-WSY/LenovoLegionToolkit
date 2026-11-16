using System;
using System.Threading.Tasks;
using System.Windows;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “屏幕刷新率”仪表盘卡片控件，以下拉框列出可用刷新率并切换；
/// 通过显示配置监听器感知显示器配置变化后自动刷新。
/// </summary>
public class RefreshRateControl : AbstractComboBoxFeatureCardControl<RefreshRate>
{
    /// <summary>显示配置监听器，用于感知分辨率/刷新率等显示设置的变化。</summary>
    private readonly DisplayConfigurationListener _listener = IoCContainer.Resolve<DisplayConfigurationListener>();

    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题、副标题，并订阅显示配置变化事件。
    /// </summary>
    public RefreshRateControl()
    {
        Icon = SymbolRegular.DesktopPulse24;
        Title = Resource.RefreshRateControl_Title;
        Subtitle = Resource.RefreshRateControl_Message;

        _listener.Changed += Listener_Changed;
    }

    /// <summary>
    /// 刷新控件状态：可用刷新率少于两个时隐藏卡片，避免无意义的选择。
    /// </summary>
    /// <returns>异步刷新任务。</returns>
    protected override async Task OnRefreshAsync()
    {
        await base.OnRefreshAsync();

        // 只有一种刷新率可选时没有切换价值，直接折叠卡片。
        Visibility = ItemsCount < 2 ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>
    /// 生成下拉项显示文本，并强制从左到右排版以正确显示诸如 “60 Hz” 的数值。
    /// </summary>
    /// <param name="value">刷新率枚举值。</param>
    /// <returns>用于显示的本地化文本。</returns>
    protected override string ComboBoxItemDisplayName(RefreshRate value)
    {
        var str = base.ComboBoxItemDisplayName(value);
        // 数字+单位在 RTL 语言环境下会错位，这里强制按 LTR 显示。
        return LocalizationHelper.ForceLeftToRight(str);
    }

    /// <summary>
    /// 显示配置变化回调：控件已加载时切回 UI 线程重新读取当前刷新率。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数（无附加数据）。</param>
    private void Listener_Changed(object? sender, EventArgs e) => Dispatcher.Invoke(async () =>
    {
        if (IsLoaded)
            await RefreshAsync();
    });
}

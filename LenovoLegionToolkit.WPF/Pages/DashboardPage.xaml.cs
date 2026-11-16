using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Controls.Dashboard;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Settings;
using LenovoLegionToolkit.WPF.Windows.Dashboard;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Pages;

/// <summary>
/// 仪表盘页面：读取用户保存的分组配置，动态创建各分组卡片控件并按窗口宽度排布为单列或双列，
/// 底部提供“自定义仪表盘”入口。
/// </summary>
public partial class DashboardPage
{
    /// <summary>仪表盘设置，保存分组配置与传感器卡片显示开关。</summary>
    private readonly DashboardSettings _dashboardSettings = IoCContainer.Resolve<DashboardSettings>();

    /// <summary>当前已创建的分组控件，供布局重排时设置行列位置。</summary>
    private readonly List<DashboardGroupControl> _dashboardGroupControls = [];

    /// <summary>
    /// 初始化页面并加载对应的 XAML 视图。
    /// </summary>
    public DashboardPage() => InitializeComponent();

    /// <summary>
    /// 页面初始化完成回调：触发首次加载。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数。</param>
    private async void DashboardPage_Initialized(object? sender, EventArgs e)
    {
        await RefreshAsync();
    }

    /// <summary>
    /// 重建仪表盘内容：按配置创建传感器卡片与各分组卡片，等待全部初始化完成后结束加载状态。
    /// </summary>
    /// <returns>异步刷新任务。</returns>
    private async Task RefreshAsync()
    {
        _loader.IsLoading = true;

        // 至少展示 1 秒加载动画，避免内容闪现造成的界面突兀。
        var initializedTasks = new List<Task> { Task.Delay(TimeSpan.FromSeconds(1)) };

        ScrollHost?.ScrollToTop();

        _sensors.Visibility = _dashboardSettings.Store.ShowSensors ? Visibility.Visible : Visibility.Collapsed;

        // 重建前清空旧控件与网格定义，防止重复叠加。
        _dashboardGroupControls.Clear();
        _content.ColumnDefinitions.Clear();
        _content.RowDefinitions.Clear();
        _content.Children.Clear();

        var groups = _dashboardSettings.Store.Groups ?? DashboardGroup.DefaultGroups;

        if (Log.Instance.IsTraceEnabled)
        {
            Log.Instance.Trace($"Groups:");
            foreach (var group in groups)
                Log.Instance.Trace($" - {group}");
        }

        // 固定两列等宽，宽屏时每组占一列，窄屏时折叠为单列。
        _content.ColumnDefinitions.Add(new ColumnDefinition { Width = new(1, GridUnitType.Star) });
        _content.ColumnDefinitions.Add(new ColumnDefinition { Width = new(1, GridUnitType.Star) });

        foreach (var group in groups)
        {
            _content.RowDefinitions.Add(new RowDefinition { Height = new(1, GridUnitType.Auto) });

            var control = new DashboardGroupControl(group);
            _content.Children.Add(control);
            _dashboardGroupControls.Add(control);
            initializedTasks.Add(control.InitializedTask);
        }

        _content.RowDefinitions.Add(new RowDefinition { Height = new(1, GridUnitType.Auto) });

        // 底部居中的“自定义仪表盘”链接，打开编辑窗口，应用后重新加载本页。
        var editDashboardHyperlink = new Hyperlink
        {
            Icon = SymbolRegular.Edit24,
            Content = Resource.DashboardPage_Customize,
            Margin = new(0, 16, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        editDashboardHyperlink.Click += (_, _) =>
        {
            var window = new EditDashboardWindow { Owner = Window.GetWindow(this) };
            window.Apply += async (_, _) => await RefreshAsync();
            window.ShowDialog();
        };

        Grid.SetRow(editDashboardHyperlink, groups.Length);
        Grid.SetColumn(editDashboardHyperlink, 0);
        Grid.SetColumnSpan(editDashboardHyperlink, 2);

        _content.Children.Add(editDashboardHyperlink);

        LayoutGroups(ActualWidth);

        await Task.WhenAll(initializedTasks);

        _loader.IsLoading = false;
    }

    /// <summary>
    /// 尺寸变化回调：宽度改变时重新排布分组（双列或单列）。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">尺寸变化参数。</param>
    private void DashboardPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged)
            return;

        LayoutGroups(e.NewSize.Width);
    }

    /// <summary>
    /// 按可用宽度选择布局：大于 1000px 使用双列，否则使用单列。
    /// </summary>
    /// <param name="width">当前可用宽度。</param>
    private void LayoutGroups(double width)
    {
        if (width > 1000)
            Expand();
        else
            Collapse();
    }

    /// <summary>
    /// 双列布局：恢复第二列宽度，分组按 0/1 交替放在两列中。
    /// </summary>
    private void Expand()
    {
        var lastColumn = _content.ColumnDefinitions.LastOrDefault();
        if (lastColumn is not null)
            lastColumn.Width = new(1, GridUnitType.Star);

        // 偶数索引放左列、奇数索引放右列，同一行相邻两个分组并排。
        for (var index = 0; index < _dashboardGroupControls.Count; index++)
        {
            var control = _dashboardGroupControls[index];
            Grid.SetRow(control, index - (index % 2));
            Grid.SetColumn(control, index % 2);
        }
    }

    /// <summary>
    /// 单列布局：把第二列宽度置零，所有分组自上而下依次排布。
    /// </summary>
    private void Collapse()
    {
        var lastColumn = _content.ColumnDefinitions.LastOrDefault();
        if (lastColumn is not null)
            lastColumn.Width = new(0, GridUnitType.Pixel);

        for (var index = 0; index < _dashboardGroupControls.Count; index++)
        {
            var control = _dashboardGroupControls[index];
            Grid.SetRow(control, index);
            Grid.SetColumn(control, 0);
        }
    }
}

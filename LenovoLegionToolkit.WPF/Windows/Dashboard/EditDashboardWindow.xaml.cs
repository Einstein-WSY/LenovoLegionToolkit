using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.WPF.Controls.Dashboard.Edit;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Settings;
using LenovoLegionToolkit.WPF.Utils;

namespace LenovoLegionToolkit.WPF.Windows.Dashboard;

/// <summary>
/// 编辑仪表盘窗口，用于增删仪表盘分组、调整顺序并切换传感器显示。
/// </summary>
public partial class EditDashboardWindow
{
    private readonly DashboardSettings _dashboardSettings = IoCContainer.Resolve<DashboardSettings>();

    private readonly DashboardGroup[] _groups;

    /// <summary>仪表盘配置被应用后触发，通知主界面刷新。</summary>
    public event EventHandler? Apply;

    /// <summary>
    /// 初始化窗口，读取已保存的分组配置；未配置时使用默认分组。
    /// </summary>
    public EditDashboardWindow()
    {
        _groups = _dashboardSettings.Store.Groups ?? DashboardGroup.DefaultGroups;

        InitializeComponent();

        IsVisibleChanged += EditDashboardWindow_IsVisibleChanged;
    }

    private async void EditDashboardWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            await RefreshAsync();
    }

    /// <summary>
    /// 重建分组列表，并通过最短 500ms 的加载动画避免界面闪烁。
    /// </summary>
    private async Task RefreshAsync()
    {
        _loader.IsLoading = true;
        _infoBar.Visibility = Visibility.Hidden;
        _applyRevertStackPanel.Visibility = Visibility.Hidden;

        var loadingTask = Task.Delay(TimeSpan.FromMilliseconds(500));

        var groups = _groups;

        _groupsScrollViewer.ScrollToTop();
        _groupsStackPanel.Children.Clear();

        _sensorsSwitch.IsChecked = _dashboardSettings.Store.ShowSensors;

        foreach (var group in groups)
            _groupsStackPanel.Children.Add(CreateGroupControl(group));

        GroupsChanged();

        await loadingTask;

        _applyRevertStackPanel.Visibility = Visibility.Visible;
        _infoBar.Visibility = Visibility.Visible;
        _loader.IsLoading = false;
    }

    /// <summary>
    /// 通过输入对话框创建自定义分组。
    /// </summary>
    private async void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var result = await MessageBoxHelper.ShowInputAsync(this,
            Resource.EditDashboardWindow_CreateGroup_Title,
            Resource.EditDashboardWindow_CreateGroup_Message,
            primaryButton: Resource.OK,
            secondaryButton: Resource.Cancel);

        if (string.IsNullOrEmpty(result))
            return;

        _groupsStackPanel.Children.Add(CreateGroupControl(new(DashboardGroupType.Custom, result)));
    }

    /// <summary>
    /// 将仪表盘恢复为默认分组配置并应用。
    /// </summary>
    private void DefaultButton_Click(object sender, RoutedEventArgs e)
    {
        _dashboardSettings.Store.ShowSensors = true;
        _dashboardSettings.Store.Groups = null;
        _dashboardSettings.SynchronizeStore();

        Close();

        Apply?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 收集当前分组与传感器开关状态并持久化，随后通知外部应用变更。
    /// </summary>
    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        _dashboardSettings.Store.ShowSensors = _sensorsSwitch.IsChecked ?? true;
        _dashboardSettings.Store.Groups = _groupsStackPanel.Children
            .OfType<EditDashboardGroupControl>()
            .Select(c => c.GetDashboardGroup())
            .ToArray();
        _dashboardSettings.SynchronizeStore();

        Close();

        Apply?.Invoke(this, EventArgs.Empty);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// 汇总当前所有分组中已放置的仪表盘项目。
    /// </summary>
    private IEnumerable<DashboardItem> GetAllItems() =>
        _groupsStackPanel.Children
            .OfType<EditDashboardGroupControl>()
            .SelectMany(c => c.GetItems());

    /// <summary>
    /// 创建分组控件，并连接其上移、下移、删除与内容变更事件。
    /// </summary>
    /// <param name="dashboardGroup">分组数据。</param>
    /// <returns>分组编辑控件。</returns>
    private EditDashboardGroupControl CreateGroupControl(DashboardGroup dashboardGroup)
    {
        var control = new EditDashboardGroupControl(dashboardGroup, GetAllItems);
        control.MoveUp += (_, _) => MoveGroupUp(control);
        control.MoveDown += (_, _) => MoveGroupDown(control);
        control.Delete += (_, _) => DeleteGroup(control);
        control.Changed += (_, _) => GroupsChanged();
        return control;
    }

    /// <summary>
    /// 分组内容变化时刷新各分组的"添加"按钮可用状态。
    /// </summary>
    private void GroupsChanged()
    {
        _groupsStackPanel.Children.OfType<EditDashboardGroupControl>().ForEach(c => c.RefreshAdd());
    }

    /// <summary>
    /// 将指定分组在列表中上移一位。
    /// </summary>
    private void MoveGroupUp(UIElement control)
    {
        var index = _groupsStackPanel.Children.IndexOf(control);
        index--;

        if (index < 0)
            return;

        _groupsStackPanel.Children.Remove(control);
        _groupsStackPanel.Children.Insert(index, control);
    }

    /// <summary>
    /// 将指定分组在列表中下移一位。
    /// </summary>
    private void MoveGroupDown(UIElement control)
    {
        var index = _groupsStackPanel.Children.IndexOf(control);
        index++;

        if (index >= _groupsStackPanel.Children.Count)
            return;

        _groupsStackPanel.Children.Remove(control);
        _groupsStackPanel.Children.Insert(index, control);
    }

    /// <summary>
    /// 从列表中移除指定分组控件。
    /// </summary>
    private void DeleteGroup(UIElement control)
    {
        _groupsStackPanel.Children.Remove(control);
    }
}

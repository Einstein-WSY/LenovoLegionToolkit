using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using LenovoLegionToolkit.WPF.Controls;
using LenovoLegionToolkit.WPF.Extensions;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;
using CardControl = LenovoLegionToolkit.WPF.Controls.Custom.CardControl;

namespace LenovoLegionToolkit.WPF.Windows.Dashboard;

/// <summary>
/// "添加仪表盘项目"窗口，列出尚未添加到仪表盘的项目供用户选择。
/// </summary>
public partial class AddDashboardItemWindow
{
    private readonly Func<IEnumerable<DashboardItem>> _existingItems;
    private readonly Action<DashboardItem> _addDashboardItem;

    /// <summary>
    /// 初始化窗口。
    /// </summary>
    /// <param name="existingItems">获取当前仪表盘已有项目的委托，用于过滤候选项。</param>
    /// <param name="addDashboardItem">用户选中项目后的回调。</param>
    public AddDashboardItemWindow(Func<IEnumerable<DashboardItem>> existingItems, Action<DashboardItem> addDashboardItem)
    {
        _existingItems = existingItems;
        _addDashboardItem = addDashboardItem;

        InitializeComponent();

        IsVisibleChanged += AddAutomationStepWindow_IsVisibleChanged;
    }

    private async void AddAutomationStepWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            await RefreshAsync();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// 重建候选列表，仅展示尚未添加到仪表盘的项目。
    /// </summary>
    private Task RefreshAsync()
    {
        _content.Children.Clear();

        var allItems = Enum.GetValues<DashboardItem>();
        var existingItems = _existingItems().ToArray();

        foreach (var item in allItems.Except(existingItems))
            _content.Children.Add(CreateCardControl(item));

        return Task.CompletedTask;
    }

    /// <summary>
    /// 将仪表盘项目包装为可点击卡片，点击后触发回调并关闭窗口。
    /// </summary>
    /// <param name="item">仪表盘项目。</param>
    /// <returns>用于列表展示的卡片控件。</returns>
    private CardControl CreateCardControl(DashboardItem item)
    {
        var control = new CardControl
        {
            Icon = item.GetIcon(),
            Header = new CardHeaderControl
            {
                Title = item.GetTitle(),
                Accessory = new SymbolIcon { Symbol = SymbolRegular.ChevronRight24 }
            },
            Margin = new(0, 8, 0, 0),
        };

        control.Click += (_, _) =>
        {
            _addDashboardItem(item);
            Close();
        };

        return control;
    }
}

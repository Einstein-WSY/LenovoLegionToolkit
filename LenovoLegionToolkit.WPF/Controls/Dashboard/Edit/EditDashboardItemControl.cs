using System;
using System.Windows.Automation;
using System.Windows.Controls;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;
using Button = Wpf.Ui.Controls.Button;
using CardControl = LenovoLegionToolkit.WPF.Controls.Custom.CardControl;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard.Edit;

/// <summary>
/// 仪表盘编辑模式下的单个卡片项控件，展示卡片标题与图标，并提供上移、下移、删除按钮。
/// </summary>
public class EditDashboardItemControl : UserControl
{
    /// <summary>该控件对应的仪表盘卡片项。</summary>
    public DashboardItem DashboardItem { get; }

    /// <summary>外层卡片容器，用于承载图标与头部。</summary>
    private readonly CardControl _cardControl = new()
    {
        Margin = new(0, 0, 0, 8)
    };

    /// <summary>卡片头部，用于显示标题与右侧操作按钮。</summary>
    private readonly CardHeaderControl _cardHeaderControl = new();

    /// <summary>横向排列操作按钮的容器。</summary>
    private readonly StackPanel _stackPanel = new()
    {
        Orientation = Orientation.Horizontal,
    };

    /// <summary>上移按钮。</summary>
    private readonly Button _moveUpButton = new()
    {
        Icon = SymbolRegular.ArrowUp24,
        ToolTip = Resource.MoveUp,
        MinWidth = 34,
        Height = 34,
        Margin = new(8, 0, 0, 0),
    };

    /// <summary>下移按钮。</summary>
    private readonly Button _moveDownButton = new()
    {
        Icon = SymbolRegular.ArrowDown24,
        ToolTip = Resource.MoveDown,
        MinWidth = 34,
        Height = 34,
        Margin = new(8, 0, 0, 0),
    };

    /// <summary>删除按钮。</summary>
    private readonly Button _deleteButton = new()
    {
        Icon = SymbolRegular.Dismiss24,
        ToolTip = Resource.Delete,
        MinWidth = 34,
        Height = 34,
        Margin = new(8, 0, 0, 0),
    };

    /// <summary>请求将本项上移时触发。</summary>
    public event EventHandler? MoveUp;
    /// <summary>请求将本项下移时触发。</summary>
    public event EventHandler? MoveDown;
    /// <summary>请求删除本项时触发。</summary>
    public event EventHandler? Delete;

    /// <summary>
    /// 使用指定的卡片项初始化控件，构建头部与操作按钮，并将按钮点击转发为对应事件。
    /// </summary>
    /// <param name="dashboardItem">要呈现的仪表盘卡片项。</param>
    public EditDashboardItemControl(DashboardItem dashboardItem)
    {
        DashboardItem = dashboardItem;

        _moveUpButton.Click += (_, _) => MoveUp?.Invoke(this, EventArgs.Empty);
        _moveDownButton.Click += (_, _) => MoveDown?.Invoke(this, EventArgs.Empty);
        _deleteButton.Click += (_, _) => Delete?.Invoke(this, EventArgs.Empty);

        _stackPanel.Children.Add(_moveUpButton);
        _stackPanel.Children.Add(_moveDownButton);
        _stackPanel.Children.Add(_deleteButton);

        _cardHeaderControl.Title = DashboardItem.GetTitle();
        _cardHeaderControl.Accessory = _stackPanel;
        _cardControl.Icon = DashboardItem.GetIcon();
        _cardControl.Header = _cardHeaderControl;

        AutomationProperties.SetName(_moveUpButton, _cardHeaderControl.Title);
        AutomationProperties.SetName(_moveDownButton, _cardHeaderControl.Title);
        AutomationProperties.SetName(_deleteButton, _cardHeaderControl.Title);

        Content = _cardControl;
    }
}

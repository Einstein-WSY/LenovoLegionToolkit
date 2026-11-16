using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;
using LenovoLegionToolkit.WPF.Windows.Dashboard;
using Wpf.Ui.Common;
using Button = Wpf.Ui.Controls.Button;
using CardExpander = LenovoLegionToolkit.WPF.Controls.Custom.CardExpander;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard.Edit;

/// <summary>
/// 仪表盘编辑模式下的分组控件：以可展开卡片呈现分组名与其卡片项列表，
/// 支持重命名分组、调整分组顺序、删除分组以及增删/排序分组内的卡片项。
/// </summary>
public class EditDashboardGroupControl : UserControl
{
    /// <summary>可展开的分组卡片容器。</summary>
    private readonly CardExpander _cardExpander = new()
    {
        Margin = new(0, 0, 0, 8)
    };

    /// <summary>卡片头部，用于显示分组名称与操作按钮。</summary>
    private readonly CardHeaderControl _cardHeaderControl = new();

    /// <summary>卡片内容区容器（卡片项列表 + 添加按钮）。</summary>
    private readonly StackPanel _stackPanel = new();

    /// <summary>分组内卡片项控件的纵向容器。</summary>
    private readonly StackPanel _itemsStackPanel = new();

    /// <summary>放置编辑/上移/下移/删除按钮的横向容器。</summary>
    private readonly StackPanel _buttonsStackPanel = new()
    {
        Margin = new(0, 0, 16, 0),
        Orientation = Orientation.Horizontal
    };

    /// <summary>重命名分组按钮。</summary>
    private readonly Button _editButton = new()
    {
        Icon = SymbolRegular.Edit24,
        ToolTip = Resource.Edit,
        MinWidth = 34,
        Height = 34,
        Margin = new(8, 0, 0, 0),
    };

    /// <summary>上移分组按钮。</summary>
    private readonly Button _moveUpButton = new()
    {
        Icon = SymbolRegular.ArrowUp24,
        ToolTip = Resource.MoveUp,
        MinWidth = 34,
        Height = 34,
        Margin = new(8, 0, 0, 0),
    };

    /// <summary>下移分组按钮。</summary>
    private readonly Button _moveDownButton = new()
    {
        Icon = SymbolRegular.ArrowDown24,
        ToolTip = Resource.MoveDown,
        MinWidth = 34,
        Height = 34,
        Margin = new(8, 0, 0, 0),
    };

    /// <summary>删除分组按钮。</summary>
    private readonly Button _deleteButton = new()
    {
        Icon = SymbolRegular.Dismiss24,
        ToolTip = Resource.Delete,
        MinWidth = 34,
        Height = 34,
        Margin = new(8, 0, 0, 0),
    };

    /// <summary>向分组内添加卡片项的按钮。</summary>
    private readonly Button _addItemButton = new()
    {
        MinWidth = 120,
        HorizontalAlignment = HorizontalAlignment.Right,
        Appearance = ControlAppearance.Primary,
        Content = Resource.Add,
        Margin = new(0, 8, 0, 0),
    };

    /// <summary>请求将本分组上移时触发。</summary>
    public event EventHandler? MoveUp;
    /// <summary>请求将本分组下移时触发。</summary>
    public event EventHandler? MoveDown;
    /// <summary>请求删除本分组时触发。</summary>
    public event EventHandler? Delete;
    /// <summary>分组内容（名称或卡片项）发生变更时触发，用于标记待保存状态。</summary>
    public event EventHandler? Changed;

    /// <summary>当前分组类型，重命名后若为自定义分组会保持自定义类型。</summary>
    private DashboardGroupType _dashboardGroupType;
    /// <summary>获取当前仪表盘已存在的全部卡片项，用于添加窗口去重。</summary>
    private readonly Func<IEnumerable<DashboardItem>> _getExistingItems;

    /// <summary>
    /// 使用分组定义与已存在项查询委托初始化控件，构建分组头部、卡片项列表与操作按钮。
    /// </summary>
    /// <param name="dashboardGroup">要编辑的仪表盘分组。</param>
    /// <param name="getExistingItems">返回当前已存在的全部卡片项的委托。</param>
    public EditDashboardGroupControl(DashboardGroup dashboardGroup, Func<IEnumerable<DashboardItem>> getExistingItems)
    {
        _dashboardGroupType = dashboardGroup.Type;

        _getExistingItems = getExistingItems;

        _editButton.Click += async (_, _) => await EditNameAsync();
        _moveUpButton.Click += (_, _) => MoveUp?.Invoke(this, EventArgs.Empty);
        _moveDownButton.Click += (_, _) => MoveDown?.Invoke(this, EventArgs.Empty);
        _deleteButton.Click += (_, _) => Delete?.Invoke(this, EventArgs.Empty);
        _addItemButton.Click += (_, _) => ShowAddItemWindow();

        _buttonsStackPanel.Children.Add(_editButton);
        _buttonsStackPanel.Children.Add(_moveUpButton);
        _buttonsStackPanel.Children.Add(_moveDownButton);
        _buttonsStackPanel.Children.Add(_deleteButton);

        foreach (var item in dashboardGroup.Items)
            _itemsStackPanel.Children.Add(CreateGroupControl(item));

        _stackPanel.Children.Add(_itemsStackPanel);
        _stackPanel.Children.Add(_addItemButton);

        _cardHeaderControl.Title = dashboardGroup.GetName();
        _cardHeaderControl.Accessory = _buttonsStackPanel;
        _cardExpander.Header = _cardHeaderControl;
        _cardExpander.Content = _stackPanel;

        AutomationProperties.SetName(_cardExpander, _cardHeaderControl.Title);
        AutomationProperties.SetName(_editButton, _cardHeaderControl.Title);
        AutomationProperties.SetName(_moveUpButton, _cardHeaderControl.Title);
        AutomationProperties.SetName(_moveDownButton, _cardHeaderControl.Title);
        AutomationProperties.SetName(_deleteButton, _cardHeaderControl.Title);

        Content = _cardExpander;
    }

    /// <summary>
    /// 依据当前界面状态构造分组对象（类型、标题与卡片项顺序）。
    /// </summary>
    /// <returns>反映当前编辑结果的新分组。</returns>
    public DashboardGroup GetDashboardGroup()
    {
        var items = _itemsStackPanel.Children
            .OfType<EditDashboardItemControl>()
            .Select(c => c.DashboardItem)
            .ToArray();
        return new(_dashboardGroupType, _cardHeaderControl.Title, items);
    }

    /// <summary>
    /// 按界面中的排列顺序枚举分组内的卡片项。
    /// </summary>
    /// <returns>当前分组包含的卡片项序列。</returns>
    public IEnumerable<DashboardItem> GetItems() =>
        _itemsStackPanel.Children
            .OfType<EditDashboardItemControl>()
            .Select(c => c.DashboardItem);

    /// <summary>
    /// 弹出输入框重命名分组；自定义分组以当前名称作为初始值，内置分组初始为空。
    /// </summary>
    /// <returns>异步操作任务。</returns>
    private async Task EditNameAsync()
    {
        // 内置分组名称由本地化资源提供，重命名时不应预填，避免用户误改资源文案。
        var text = _dashboardGroupType == DashboardGroupType.Custom ? _cardHeaderControl.Title : null;

        var result = await MessageBoxHelper.ShowInputAsync(this,
            Resource.EditDashboardGroupControl_EditGroup_Title,
            Resource.EditDashboardGroupControl_EditGroup_Message,
            text,
            primaryButton: Resource.OK,
            secondaryButton: Resource.Cancel);

        if (string.IsNullOrEmpty(result))
            return;

        // 重命名后分组转为自定义类型，以便持久化用户名称。
        _dashboardGroupType = DashboardGroupType.Custom;
        _cardHeaderControl.Title = result;
    }

    /// <summary>
    /// 打开“添加卡片项”窗口，并以其回调把选中项加入当前分组。
    /// </summary>
    private void ShowAddItemWindow()
    {
        var window = new AddDashboardItemWindow(_getExistingItems, AddItem) { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    /// <summary>
    /// 将新卡片项添加到列表末尾并通知内容已变更。
    /// </summary>
    /// <param name="dashboardItem">要添加的卡片项。</param>
    private void AddItem(DashboardItem dashboardItem)
    {
        _itemsStackPanel.Children.Add(CreateGroupControl(dashboardItem));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 创建单个卡片项控件，并把其上移/下移/删除事件连接到本分组的处理逻辑。
    /// </summary>
    /// <param name="dashboardItem">对应的卡片项。</param>
    /// <returns>配置完成的卡片项控件。</returns>
    private EditDashboardItemControl CreateGroupControl(DashboardItem dashboardItem)
    {
        var control = new EditDashboardItemControl(dashboardItem);
        control.MoveUp += (_, _) => MoveItemUp(control);
        control.MoveDown += (_, _) => MoveItemDown(control);
        control.Delete += (_, _) => DeleteItem(control);
        return control;
    }

    /// <summary>
    /// 将指定卡片项上移一位，已位于首位时不做处理。
    /// </summary>
    /// <param name="control">要移动的卡片项控件。</param>
    private void MoveItemUp(UIElement control)
    {
        var index = _itemsStackPanel.Children.IndexOf(control);
        index--;

        // 已在首位，无法继续上移。
        if (index < 0)
            return;

        // 先移除再插入，避免索引偏移。
        _itemsStackPanel.Children.Remove(control);
        _itemsStackPanel.Children.Insert(index, control);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 将指定卡片项下移一位，已位于末位时不做处理。
    /// </summary>
    /// <param name="control">要移动的卡片项控件。</param>
    private void MoveItemDown(UIElement control)
    {
        var index = _itemsStackPanel.Children.IndexOf(control);
        index++;

        // 已在末位，无法继续下移。
        if (index >= _itemsStackPanel.Children.Count)
            return;

        // 先移除再插入，避免索引偏移。
        _itemsStackPanel.Children.Remove(control);
        _itemsStackPanel.Children.Insert(index, control);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 从列表中移除指定卡片项并通知内容已变更。
    /// </summary>
    /// <param name="control">要删除的卡片项控件。</param>
    private void DeleteItem(UIElement control)
    {
        _itemsStackPanel.Children.Remove(control);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 刷新“添加”按钮可用性：仅当仍有未被使用的卡片项时可用。
    /// </summary>
    public void RefreshAdd() => _addItemButton.IsEnabled = Enum.GetValues<DashboardItem>().Except(_getExistingItems()).Any();
}

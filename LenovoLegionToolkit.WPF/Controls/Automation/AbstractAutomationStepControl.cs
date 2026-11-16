using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;
using Button = Wpf.Ui.Controls.Button;
using CardControl = LenovoLegionToolkit.WPF.Controls.Custom.CardControl;

namespace LenovoLegionToolkit.WPF.Controls.Automation;

/// <summary>
/// 强类型自动化步骤控件基类，将 <see cref="AutomationStep"/> 暴露为具体步骤类型 <typeparamref name="T"/>。
/// </summary>
/// <typeparam name="T">该控件所编辑的具体自动化步骤类型。</typeparam>
/// <param name="automationStep">要编辑的自动化步骤实例。</param>
public abstract class AbstractAutomationStepControl<T>(T automationStep) : AbstractAutomationStepControl(automationStep)
    where T : IAutomationStep
{
    /// <summary>
    /// 获取强类型化后的自动化步骤实例。
    /// </summary>
    protected new T AutomationStep => (T)base.AutomationStep;
}

/// <summary>
/// 自动化步骤编辑控件的非泛型基类：以卡片形式展示步骤标题、副标题与自定义编辑区域，并提供删除按钮。
/// </summary>
public abstract class AbstractAutomationStepControl : UserControl
{
    /// <summary>
    /// 当前控件所编辑的自动化步骤。
    /// </summary>
    protected IAutomationStep AutomationStep { get; }

    private readonly CardControl _cardControl = new()
    {
        Margin = new(0, 0, 0, 8),
    };

    private readonly CardHeaderControl _cardHeaderControl = new();

    private readonly StackPanel _stackPanel = new()
    {
        Orientation = Orientation.Horizontal,
    };

    private readonly Button _deleteButton = new()
    {
        Icon = SymbolRegular.Dismiss24,
        ToolTip = Resource.AbstractAutomationStepControl_Delete,
        MinWidth = 34,
        Height = 34,
        Margin = new(8, 0, 0, 0),
    };

    /// <summary>
    /// 卡片图标。
    /// </summary>
    public SymbolRegular Icon
    {
        get => _cardControl.Icon;
        set => _cardControl.Icon = value;
    }

    /// <summary>
    /// 卡片标题。
    /// </summary>
    public string Title
    {
        get => _cardHeaderControl.Title;
        set => _cardHeaderControl.Title = value;
    }

    /// <summary>
    /// 卡片副标题（通常为步骤行为说明）。
    /// </summary>
    public string Subtitle
    {
        get => _cardHeaderControl.Subtitle;
        set => _cardHeaderControl.Subtitle = value;
    }

    /// <summary>
    /// 标题的垂直对齐方式。
    /// </summary>
    public VerticalAlignment TitleVerticalAlignment
    {
        get => _cardHeaderControl.TitleVerticalAlignment;
        set => _cardHeaderControl.TitleVerticalAlignment = value;
    }

    /// <summary>
    /// 副标题的垂直对齐方式。
    /// </summary>
    public VerticalAlignment SubtitleVerticalAlignment
    {
        get => _cardHeaderControl.SubtitleVerticalAlignment;
        set => _cardHeaderControl.SubtitleVerticalAlignment = value;
    }

    /// <summary>
    /// 当用户修改了步骤参数时触发，用于通知上层管线保存更改。
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// 当用户点击删除按钮时触发，用于通知上层管线移除该步骤。
    /// </summary>
    public event EventHandler? Delete;

    /// <summary>
    /// 初始化控件并订阅加载完成事件。
    /// </summary>
    /// <param name="automationStep">要编辑的自动化步骤实例。</param>
    protected AbstractAutomationStepControl(IAutomationStep automationStep)
    {
        AutomationStep = automationStep;

        InitializeComponent();

        Loaded += RefreshingControl_Loaded;
    }

    /// <summary>
    /// 手动构建卡片界面：绑定删除按钮、插入子类提供的自定义控件并组装卡片结构。
    /// </summary>
    private void InitializeComponent()
    {
        _deleteButton.Click += (_, _) => Delete?.Invoke(this, EventArgs.Empty);

        var control = GetCustomControl();
        if (control is not null)
            _stackPanel.Children.Add(control);
        _stackPanel.Children.Add(_deleteButton);

        _cardHeaderControl.Accessory = _stackPanel;
        _cardControl.Header = _cardHeaderControl;

        Content = _cardControl;
    }

    /// <summary>
    /// 控件加载完成后同步步骤状态到界面，并通知子类加载结束。
    /// </summary>
    private async void RefreshingControl_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
        OnFinishedLoading();
    }

    /// <summary>
    /// 根据当前界面状态创建新的自动化步骤实例（用于序列化回写）。
    /// </summary>
    /// <returns>新的自动化步骤实例。</returns>
    public abstract IAutomationStep CreateAutomationStep();

    /// <summary>
    /// 获取子类自定义的编辑控件，返回 null 表示该步骤无参数需要编辑。
    /// </summary>
    protected abstract UIElement? GetCustomControl();

    /// <summary>
    /// 加载完成后的收尾处理（例如显示下拉框）。
    /// </summary>
    protected abstract void OnFinishedLoading();

    /// <summary>
    /// 将步骤当前状态同步到界面控件。
    /// </summary>
    protected abstract Task RefreshAsync();

    /// <summary>
    /// 触发 <see cref="Changed"/> 事件，通知上层步骤参数已变化。
    /// </summary>
    protected void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}

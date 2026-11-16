using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “显示通知”步骤的编辑器：使用文本框编辑要显示的通知文本。
/// </summary>
public class NotificationAutomationStepControl : AbstractAutomationStepControl<NotificationAutomationStep>
{
    // 通知文本输入框。
    private readonly TextBox _scriptPath = new()
    {
        PlaceholderText = Resource.NotificationAutomationStepControl_NotificationText,
        Width = 300
    };

    /// <summary>
    /// 编辑区容器。
    /// </summary>
    private readonly StackPanel _stackPanel = new();

    /// <summary>
    /// 初始化编辑器并设置图标与标题，同时订阅尺寸变化以自适应输入框宽度。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public NotificationAutomationStepControl(NotificationAutomationStep step) : base(step)
    {
        Icon = SymbolRegular.Rocket24;
        Title = Resource.NotificationAutomationStepControl_Title;

        SizeChanged += RunAutomationStepControl_SizeChanged;
    }

    /// <summary>
    /// 宽度变化时将输入框宽度调整为卡片宽度的三分之一。
    /// </summary>
    private void RunAutomationStepControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged)
            return;

        var newWidth = e.NewSize.Width / 3;
        _scriptPath.Width = newWidth;
    }

    /// <summary>
    /// 以文本框内容创建通知步骤。
    /// </summary>
    /// <returns>新的步骤实例。</returns>
    public override IAutomationStep CreateAutomationStep() => new NotificationAutomationStep(_scriptPath.Text);

    /// <summary>
    /// 提供通知文本框，并在文本变化时通知上层。
    /// </summary>
    /// <returns>自定义编辑控件。</returns>
    protected override UIElement GetCustomControl()
    {
        _scriptPath.TextChanged += (_, _) =>
        {
            if (_scriptPath.Text != AutomationStep.Text)
                RaiseChanged();
        };

        _stackPanel.Children.Add(_scriptPath);

        return _stackPanel;
    }

    /// <summary>
    /// 无加载后处理。
    /// </summary>
    protected override void OnFinishedLoading() { }

    /// <summary>
    /// 将步骤中的通知文本同步到文本框。
    /// </summary>
    protected override Task RefreshAsync()
    {
        _scriptPath.Text = AutomationStep.Text ?? string.Empty;
        return Task.CompletedTask;
    }
}

using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “运行程序”步骤的编辑器：编辑可执行文件路径、启动参数，以及静默运行、等待结束两个选项。
/// </summary>
public class RunAutomationStepControl : AbstractAutomationStepControl<RunAutomationStep>
{
    // 可执行文件路径输入框。
    private readonly TextBox _scriptPath = new()
    {
        PlaceholderText = Resource.RunAutomationStepControl_ExePath,
        Width = 300,
        Margin = new(0, 0, 0, 8),
    };

    // 启动参数输入框。
    private readonly TextBox _scriptArguments = new()
    {
        PlaceholderText = Resource.RunAutomationStepControl_ExeArguments,
        Width = 300,
        Margin = new(0, 0, 0, 8),
    };

    // “静默运行”复选框（不显示窗口）。
    private readonly CheckBox _checkBoxProcessRunSilently = new()
    {
        Content = Resource.RunAutomationStepControl_ProcessRunSilently,
        ToolTip = Resource.RunAutomationStepControl_ProcessRunSilently_Description
    };

    // “等待进程结束”复选框（阻塞后续步骤直到进程退出）。
    private readonly CheckBox _checkBoxProcessWaitUntilFinished = new()
    {
        Content = Resource.RunAutomationStepControl_ProcessWaitUntilFinished,
        ToolTip = Resource.RunAutomationStepControl_ProcessWaitUntilFinished_Description
    };

    /// <summary>
    /// 编辑区容器（垂直排列）。
    /// </summary>
    private readonly StackPanel _stackPanel = new();

    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本，同时为各控件设置无障碍名称并订阅尺寸变化。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public RunAutomationStepControl(RunAutomationStep step) : base(step)
    {
        Icon = SymbolRegular.WindowConsole20;
        Title = Resource.RunAutomationStepControl_Title;
        Subtitle = Resource.RunAutomationStepControl_Message;
        TitleVerticalAlignment = VerticalAlignment.Bottom;
        SubtitleVerticalAlignment = VerticalAlignment.Top;

        AutomationProperties.SetName(_scriptPath, Resource.RunAutomationStepControl_ExePath);
        AutomationProperties.SetName(_scriptArguments, Resource.RunAutomationStepControl_ExeArguments);
        AutomationProperties.SetName(_checkBoxProcessRunSilently, Resource.RunAutomationStepControl_ProcessRunSilently);
        AutomationProperties.SetName(_checkBoxProcessWaitUntilFinished, Resource.RunAutomationStepControl_ProcessWaitUntilFinished);

        SizeChanged += RunAutomationStepControl_SizeChanged;
    }

    /// <summary>
    /// 宽度变化时将两个输入框宽度调整为卡片宽度的三分之一。
    /// </summary>
    private void RunAutomationStepControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged)
            return;

        var newWidth = e.NewSize.Width / 3;
        _scriptPath.Width = newWidth;
        _scriptArguments.Width = newWidth;
    }

    /// <summary>
    /// 根据界面控件的当前值创建步骤；复选框未确定时默认按 true（静默运行、等待结束）处理。
    /// </summary>
    /// <returns>新的步骤实例。</returns>
    public override IAutomationStep CreateAutomationStep()
    {
        return new RunAutomationStep(_scriptPath.Text,
            _scriptArguments.Text,
            _checkBoxProcessRunSilently.IsChecked ?? true,
            _checkBoxProcessWaitUntilFinished.IsChecked ?? true);
    }

    /// <summary>
    /// 构建编辑区，并在文本/勾选状态变化时通知上层；仅当值与步骤当前值不同才触发以免重复通知。
    /// </summary>
    /// <returns>自定义编辑控件。</returns>
    protected override UIElement GetCustomControl()
    {
        _scriptPath.TextChanged += (_, _) =>
        {
            if (_scriptPath.Text != AutomationStep.ScriptPath)
                RaiseChanged();
        };
        _scriptArguments.TextChanged += (_, _) =>
        {
            if (_scriptArguments.Text != AutomationStep.ScriptArguments)
                RaiseChanged();
        };
        _checkBoxProcessRunSilently.Checked += (_, _) =>
        {
            if (_checkBoxProcessRunSilently.IsChecked != AutomationStep.RunSilently)
                RaiseChanged();
        };
        _checkBoxProcessWaitUntilFinished.Checked += (_, _) =>
        {
            if (_checkBoxProcessWaitUntilFinished.IsChecked != AutomationStep.WaitUntilFinished)
                RaiseChanged();
        };
        _checkBoxProcessRunSilently.Unchecked += (_, _) =>
        {
            if (_checkBoxProcessRunSilently.IsChecked != AutomationStep.RunSilently)
                RaiseChanged();
        };
        _checkBoxProcessWaitUntilFinished.Unchecked += (_, _) =>
        {
            if (_checkBoxProcessWaitUntilFinished.IsChecked != AutomationStep.WaitUntilFinished)
                RaiseChanged();
        };

        _stackPanel.Children.Add(_scriptPath);
        _stackPanel.Children.Add(_scriptArguments);
        _stackPanel.Children.Add(_checkBoxProcessRunSilently);
        _stackPanel.Children.Add(_checkBoxProcessWaitUntilFinished);

        return _stackPanel;
    }

    /// <summary>
    /// 无加载后处理。
    /// </summary>
    protected override void OnFinishedLoading() { }

    /// <summary>
    /// 将步骤中的路径、参数与选项同步到界面控件。
    /// </summary>
    protected override Task RefreshAsync()
    {
        _scriptPath.Text = AutomationStep.ScriptPath ?? string.Empty;
        _scriptArguments.Text = AutomationStep.ScriptArguments ?? string.Empty;
        _checkBoxProcessRunSilently.IsChecked = AutomationStep.RunSilently;
        _checkBoxProcessWaitUntilFinished.IsChecked = AutomationStep.WaitUntilFinished;
        return Task.CompletedTask;
    }
}

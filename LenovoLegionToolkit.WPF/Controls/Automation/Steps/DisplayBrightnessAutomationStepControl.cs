using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;
using NumberBox = Wpf.Ui.Controls.NumberBox;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “显示亮度”步骤的编辑器：使用数字框编辑 0-100 的亮度百分比参数。
/// </summary>
public class DisplayBrightnessAutomationStepControl : AbstractAutomationStepControl<DisplayBrightnessAutomationStep>
{
    // 亮度输入框；取值范围 0-100，步进 5。
    private readonly NumberBox _brightness = new()
    {
        Width = 150,
        ClearButtonEnabled = false,
        MaxDecimalPlaces = 0,
        Minimum = 0,
        Maximum = 100,
        SmallChange = 5,
        LargeChange = 5
    };

    /// <summary>
    /// 编辑器布局容器。
    /// </summary>
    private readonly Grid _grid = new();

    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public DisplayBrightnessAutomationStepControl(DisplayBrightnessAutomationStep step) : base(step)
    {
        Icon = SymbolRegular.BrightnessHigh48;
        Title = Resource.DisplayBrightnessAutomationStepControl_Title;
        Subtitle = Resource.DisplayBrightnessAutomationStepControl_Message;
    }

    /// <summary>
    /// 以数字框当前值创建步骤；空值按 0 处理。
    /// </summary>
    /// <returns>新的步骤实例。</returns>
    public override IAutomationStep CreateAutomationStep() => new DisplayBrightnessAutomationStep((int?)_brightness.Value ?? 0);

    /// <summary>
    /// 提供亮度数字框，并在数值变化时通知上层。
    /// </summary>
    /// <returns>自定义编辑控件。</returns>
    protected override UIElement GetCustomControl()
    {
        _brightness.TextChanged += (_, _) =>
        {
            if ((int?)_brightness.Value != AutomationStep.Brightness)
                RaiseChanged();
        };
        _grid.Children.Add(_brightness);
        return _grid;
    }

    /// <summary>
    /// 无加载后处理。
    /// </summary>
    protected override void OnFinishedLoading() { }

    /// <summary>
    /// 将步骤中的亮度同步到数字框。
    /// </summary>
    protected override Task RefreshAsync()
    {
        _brightness.Value = AutomationStep.Brightness;
        return Task.CompletedTask;
    }
}

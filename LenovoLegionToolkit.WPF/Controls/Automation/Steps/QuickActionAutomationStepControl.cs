using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation;
using LenovoLegionToolkit.Lib.Automation.Pipeline;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “快捷操作”步骤的编辑器：以下拉框选择要触发的手动管线（无触发器管线）。
/// </summary>
public class QuickActionAutomationStepControl : AbstractAutomationStepControl<QuickActionAutomationStep>
{
    // 自动化处理器，用于枚举现有管线。
    private readonly AutomationProcessor _processor = IoCContainer.Resolve<AutomationProcessor>();

    // 管线选择下拉框。
    private readonly ComboBox _comboBox = new()
    {
        MinWidth = 150
    };

    /// <summary>
    /// 编辑区容器。
    /// </summary>
    private readonly StackPanel _stackPanel = new();

    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public QuickActionAutomationStepControl(QuickActionAutomationStep step) : base(step)
    {
        Icon = SymbolRegular.Play24;
        Title = Resource.QuickActionAutomationStepControl_Title;
        Subtitle = Resource.QuickActionAutomationStepControl_Message;
    }

    /// <summary>
    /// 根据选中的管线创建步骤；未选择时管线 ID 为 null。
    /// </summary>
    /// <returns>新的步骤实例。</returns>
    public override IAutomationStep CreateAutomationStep()
    {
        return _comboBox.TryGetSelectedItem(out AutomationPipeline? pipeline)
            ? new QuickActionAutomationStep(pipeline?.Id)
            : new QuickActionAutomationStep(null);
    }

    /// <summary>
    /// 提供管线下拉框，并在选择变化时通知上层。
    /// </summary>
    /// <returns>自定义编辑控件。</returns>
    protected override UIElement GetCustomControl()
    {
        _comboBox.SelectionChanged += (_, _) => { RaiseChanged(); };
        _stackPanel.Children.Add(_comboBox);
        return _stackPanel;
    }

    /// <summary>
    /// 无加载后处理。
    /// </summary>
    protected override void OnFinishedLoading() { }

    /// <summary>
    /// 仅列出无触发器的手动管线作为可选数据源，并定位到当前已选管线。
    /// </summary>
    protected override async Task RefreshAsync()
    {
        var pipelines = await _processor.GetPipelinesAsync();
        var filteredPipelines = pipelines.Where(p => p.Trigger is null).ToArray();
        var selectedPipeline = filteredPipelines.FirstOrDefault(p => p.Id == AutomationStep.PipelineId);

        _comboBox.SetItems(filteredPipelines, selectedPipeline, p => p?.Name ?? "");
    }
}

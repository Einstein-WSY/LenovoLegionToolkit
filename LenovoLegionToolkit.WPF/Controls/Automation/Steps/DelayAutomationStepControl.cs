using LenovoLegionToolkit.Lib.Automation;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “延迟”步骤的编辑器：通过下拉框选择预定义的 <see cref="Delay"/> 时长。
/// </summary>
public class DelayAutomationStepControl : AbstractComboBoxAutomationStepCardControl<Delay>
{
    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public DelayAutomationStepControl(IAutomationStep<Delay> step) : base(step)
    {
        Icon = SymbolRegular.Clock24;
        Title = Resource.DelayAutomationStepControl_Title;
        Subtitle = Resource.DelayAutomationStepControl_Message;
    }
}

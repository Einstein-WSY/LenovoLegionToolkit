using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “开盖即开机”步骤的编辑器：通过下拉框选择 <see cref="FlipToStartState"/> 参数。
/// </summary>
public class FlipToStartAutomationStepControl : AbstractComboBoxAutomationStepCardControl<FlipToStartState>
{
    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public FlipToStartAutomationStepControl(IAutomationStep<FlipToStartState> step) : base(step)
    {
        Icon = SymbolRegular.Power24;
        Title = Resource.FlipToStartAutomationStepControl_Title;
        Subtitle = Resource.FlipToStartAutomationStepControl_Message;
    }
}

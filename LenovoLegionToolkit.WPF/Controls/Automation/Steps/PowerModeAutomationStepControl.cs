using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “电源模式”步骤的编辑器：通过下拉框选择 <see cref="PowerModeState"/> 参数。
/// </summary>
public class PowerModeAutomationStepControl : AbstractComboBoxAutomationStepCardControl<PowerModeState>
{
    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public PowerModeAutomationStepControl(IAutomationStep<PowerModeState> step) : base(step)
    {
        Icon = SymbolRegular.Gauge24;
        Title = Resource.PowerModeAutomationStepControl_Title;
        Subtitle = Resource.PowerModeAutomationStepControl_Message;
    }
}

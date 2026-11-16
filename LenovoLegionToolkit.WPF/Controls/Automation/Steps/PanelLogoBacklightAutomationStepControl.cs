using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “顶盖 Logo 背光”步骤的编辑器：通过下拉框选择 <see cref="PanelLogoBacklightState"/> 参数。
/// </summary>
public class PanelLogoBacklightAutomationStepControl : AbstractComboBoxAutomationStepCardControl<PanelLogoBacklightState>
{
    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public PanelLogoBacklightAutomationStepControl(IAutomationStep<PanelLogoBacklightState> step) : base(step)
    {
        Icon = SymbolRegular.LightbulbCircle24;
        Title = Resource.PanelLogoBacklightAutomationStepControl_Title;
        Subtitle = Resource.PanelLogoBacklightAutomationStepControl_Message;
    }
}

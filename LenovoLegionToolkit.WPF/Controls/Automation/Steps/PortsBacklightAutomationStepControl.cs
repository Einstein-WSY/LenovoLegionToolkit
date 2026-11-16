using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “接口背光”步骤的编辑器：通过下拉框选择 <see cref="PortsBacklightState"/> 参数。
/// </summary>
public class PortsBacklightAutomationStepControl : AbstractComboBoxAutomationStepCardControl<PortsBacklightState>
{
    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public PortsBacklightAutomationStepControl(IAutomationStep<PortsBacklightState> step) : base(step)
    {
        Icon = SymbolRegular.UsbPlug24;
        Title = Resource.PortsBacklightAutomationStepControl_Title;
        Subtitle = Resource.PortsBacklightAutomationStepControl_Message;
    }
}

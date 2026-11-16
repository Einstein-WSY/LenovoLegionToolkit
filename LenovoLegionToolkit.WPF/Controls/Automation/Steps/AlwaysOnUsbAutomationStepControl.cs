using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “始终开启 USB”步骤的编辑器：通过下拉框选择 <see cref="AlwaysOnUSBState"/> 参数。
/// </summary>
public class AlwaysOnUsbAutomationStepControl : AbstractComboBoxAutomationStepCardControl<AlwaysOnUSBState>
{
    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public AlwaysOnUsbAutomationStepControl(IAutomationStep<AlwaysOnUSBState> step) : base(step)
    {
        Icon = SymbolRegular.UsbStick24;
        Title = Resource.AlwaysOnUsbAutomationStepControl_Title;
        Subtitle = Resource.AlwaysOnUsbAutomationStepControl_Message;
    }
}

using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “电池充电”步骤的编辑器：通过下拉框选择 <see cref="BatteryState"/> 参数。
/// </summary>
public class BatteryAutomationStepControl : AbstractComboBoxAutomationStepCardControl<BatteryState>
{
    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public BatteryAutomationStepControl(IAutomationStep<BatteryState> step) : base(step)
    {
        Icon = SymbolRegular.BatteryCharge24;
        Title = Resource.BatteryAutomationStepControl_Title;
        Subtitle = Resource.BatteryAutomationStepControl_Message;
    }
}

using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “触摸板锁定”步骤的编辑器：通过下拉框选择 <see cref="TouchpadLockState"/> 参数。
/// </summary>
public class TouchpadLockAutomationStepControl : AbstractComboBoxAutomationStepCardControl<TouchpadLockState>
{
    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public TouchpadLockAutomationStepControl(IAutomationStep<TouchpadLockState> step) : base(step)
    {
        Icon = SymbolRegular.Tablet24;
        Title = Resource.TouchpadLockAutomationStepControl_Title;
        Subtitle = Resource.TouchpadLockAutomationStepControl_Message;
    }
}

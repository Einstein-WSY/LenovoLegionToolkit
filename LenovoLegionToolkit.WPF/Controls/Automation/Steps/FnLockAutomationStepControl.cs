using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “Fn 锁定”步骤的编辑器：通过下拉框选择 <see cref="FnLockState"/> 参数。
/// </summary>
public class FnLockAutomationStepControl : AbstractComboBoxAutomationStepCardControl<FnLockState>
{
    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public FnLockAutomationStepControl(IAutomationStep<FnLockState> step) : base(step)
    {
        Icon = SymbolRegular.Keyboard24;
        Title = Resource.FnLockAutomationStepControl_Title;
        Subtitle = Resource.FnLockAutomationStepControl_Message;
    }
}

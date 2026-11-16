using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “白色键盘背光”步骤的编辑器：通过下拉框选择 <see cref="WhiteKeyboardBacklightState"/> 参数。
/// </summary>
public class WhiteKeyboardBacklightAutomationStepControl : AbstractComboBoxAutomationStepCardControl<WhiteKeyboardBacklightState>
{
    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public WhiteKeyboardBacklightAutomationStepControl(IAutomationStep<WhiteKeyboardBacklightState> step) : base(step)
    {
        Icon = SymbolRegular.Keyboard24;
        Title = Resource.WhiteKeyboardBacklightAutomationStepControl_Title;
        Subtitle = Resource.WhiteKeyboardBacklightAutomationStepControl_Message;
    }
}

using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “RGB 键盘背光”步骤的编辑器：通过下拉框选择 <see cref="RGBKeyboardBacklightPreset"/> 预设。
/// </summary>
public class RGBKeyboardBacklightAutomationStepControl : AbstractComboBoxAutomationStepCardControl<RGBKeyboardBacklightPreset>
{
    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public RGBKeyboardBacklightAutomationStepControl(IAutomationStep<RGBKeyboardBacklightPreset> step) : base(step)
    {
        Icon = SymbolRegular.Keyboard24;
        Title = Resource.RGBKeyboardBacklightAutomationStepControl_Title;
        Subtitle = Resource.RGBKeyboardBacklightAutomationStepControl_Message;
    }
}

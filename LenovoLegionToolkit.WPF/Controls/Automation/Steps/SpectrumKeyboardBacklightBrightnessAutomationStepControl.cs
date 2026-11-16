using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “Spectrum 键盘背光亮度”步骤的编辑器：通过下拉框选择亮度级别（int）。
/// </summary>
public class SpectrumKeyboardBacklightBrightnessAutomationStepControl : AbstractComboBoxAutomationStepCardControl<int>
{
    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public SpectrumKeyboardBacklightBrightnessAutomationStepControl(IAutomationStep<int> step) : base(step)
    {
        Icon = SymbolRegular.BrightnessHigh24;
        Title = Resource.SpectrumKeyboardBacklightBrightnessAutomationStepControl_Title;
        Subtitle = Resource.SpectrumKeyboardBacklightBrightnessAutomationStepControl_Message;
    }

    /// <summary>
    /// 生成下拉项显示文本：亮度为 0 时显示"关闭"，其余沿用基类的数字显示。
    /// </summary>
    /// <param name="value">亮度值。</param>
    /// <returns>显示文本。</returns>
    protected override string ComboBoxItemDisplayName(int value)
    {
        return value == 0
            ? Resource.SpectrumKeyboardBacklightBrightnessAutomationStepControl_Off
            : base.ComboBoxItemDisplayName(value);
    }
}

using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “麦克风”步骤的编辑器：通过下拉框选择 <see cref="MicrophoneState"/> 参数。
/// </summary>
public class MicrophoneAutomationStepControl : AbstractComboBoxAutomationStepCardControl<MicrophoneState>
{
    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public MicrophoneAutomationStepControl(IAutomationStep<MicrophoneState> step) : base(step)
    {
        Icon = SymbolRegular.Mic24;
        Title = Resource.MicrophoneAutomationStepControl_Title;
        Subtitle = Resource.MicrophoneAutomationStepControl_Message;
    }
}

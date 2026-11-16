using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “屏幕超频（Over Drive）”步骤的编辑器：通过下拉框选择 <see cref="OverDriveState"/> 参数。
/// </summary>
public class OverDriveAutomationStepControl : AbstractComboBoxAutomationStepCardControl<OverDriveState>
{
    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public OverDriveAutomationStepControl(IAutomationStep<OverDriveState> step) : base(step)
    {
        Icon = SymbolRegular.TopSpeed24;
        Title = Resource.OverDriveAutomationStepControl_Title;
        Subtitle = Resource.OverDriveAutomationStepControl_Message;
    }
}

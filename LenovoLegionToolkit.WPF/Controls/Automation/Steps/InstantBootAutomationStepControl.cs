using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “快速启动”步骤的编辑器：通过下拉框选择 <see cref="InstantBootState"/> 参数。
/// </summary>
internal class InstantBootAutomationStepControl : AbstractComboBoxAutomationStepCardControl<InstantBootState>
{
    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public InstantBootAutomationStepControl(IAutomationStep<InstantBootState> step) : base(step)
    {
        Icon = SymbolRegular.PlugDisconnected24;
        Title = Resource.InstantBootAutomationStepControl_Title;
        Subtitle = Resource.InstantBootAutomationStepControl_Message;
    }
}

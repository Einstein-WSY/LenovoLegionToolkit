using System.Threading.Tasks;
using System.Windows;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “关闭显示器”步骤的编辑器：该步骤无参数，仅展示卡片与删除按钮。
/// </summary>
public class TurnOffMonitorsAutomationStepControl : AbstractAutomationStepControl
{
    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="automationStep">要编辑的步骤实例。</param>
    public TurnOffMonitorsAutomationStepControl(TurnOffMonitorsAutomationStep automationStep) : base(automationStep)
    {
        Icon = SymbolRegular.Desktop24;
        Title = Resource.TurnOffMonitorsAutomationStepControl_Title;
        Subtitle = Resource.TurnOffMonitorsAutomationStepControl_Message;
    }

    /// <summary>
    /// 创建步骤实例（无参数）。
    /// </summary>
    /// <returns>新的步骤实例。</returns>
    public override IAutomationStep CreateAutomationStep() => new TurnOffMonitorsAutomationStep();

    /// <summary>
    /// 无自定义编辑控件。
    /// </summary>
    protected override UIElement? GetCustomControl() => null;

    /// <summary>
    /// 无加载后处理。
    /// </summary>
    protected override void OnFinishedLoading() { }

    /// <summary>
    /// 无状态需要同步。
    /// </summary>
    protected override Task RefreshAsync() => Task.CompletedTask;
}

using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

namespace LenovoLegionToolkit.WPF.Windows.Automation.TabItemContent;

/// <summary>
/// 自动化管道触发器选项卡内容的通用接口，用于从界面控件中取回编辑后的触发器。
/// </summary>
/// <typeparam name="T">触发器类型。</typeparam>
public interface IAutomationPipelineTriggerTabItemContent<out T> where T : IAutomationPipelineTrigger
{
    /// <summary>
    /// 根据当前界面状态生成触发器实例。
    /// </summary>
    /// <returns>编辑后的触发器。</returns>
    T GetTrigger();
}

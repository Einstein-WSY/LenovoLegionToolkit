using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：系统从睡眠/休眠恢复时执行。
/// 触发时机为电源状态事件中的“恢复”，且必须不是由电源适配器插拔引起——
/// 否则“插上电源同时唤醒”会被误判为单纯的恢复。
/// </summary>
public class OnResumeAutomationPipelineTrigger : IOnResumeAutomationPipelineTrigger
{
    /// <summary>界面显示名称：当从睡眠恢复时。</summary>
    [JsonIgnore]
    public string DisplayName => Resource.OnResumeAutomationPipelineTrigger_DisplayName;

    /// <summary>
    /// 判断事件是否触发：事件为 Resume，且本次变化与适配器插拔无关。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>系统恢复且非适配器插拔引起时返回 true。</returns>
    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        return Task.FromResult(automationEvent is PowerStateAutomationEvent { PowerStateEvent: PowerStateEvent.Resume, PowerAdapterStateChanged: false });
    }

    /// <summary>
    /// “恢复”属于瞬时事件而非持续状态，无法通过查询状态判断，因此固定返回 false。
    /// </summary>
    /// <returns>始终返回 false。</returns>
    public Task<bool> IsMatchingState() => Task.FromResult(false);

    /// <summary>向执行环境写入“本次由系统恢复触发”。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment) => environment.Resume = true;

    /// <summary>创建深拷贝。该触发器无可变配置。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new OnResumeAutomationPipelineTrigger();

    /// <summary>同类触发器彼此等价，只比较类型。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>类型相同时返回 true。</returns>
    public override bool Equals(object? obj) => obj is OnResumeAutomationPipelineTrigger;

    /// <summary>基于显示名称生成哈希值。</summary>
    /// <returns>哈希值。</returns>
    public override int GetHashCode() => HashCode.Combine(DisplayName);
}

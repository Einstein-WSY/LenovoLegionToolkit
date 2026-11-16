using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：应用启动时执行。
/// 触发时机为启动阶段由 AutomationProcessor 主动派发的启动事件。
/// </summary>
public class OnStartupAutomationPipelineTrigger : IOnStartupAutomationPipelineTrigger
{
    /// <summary>界面显示名称：当应用启动时。</summary>
    [JsonIgnore]
    public string DisplayName => Resource.OnStartupAutomationPipelineTrigger_DisplayName;

    /// <summary>
    /// 判断事件是否触发：事件类型为启动事件即可。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>事件为启动事件时返回 true。</returns>
    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        return Task.FromResult(automationEvent is StartupAutomationEvent);
    }

    /// <summary>
    /// “启动”属于瞬时事件而非持续状态，无法通过查询状态判断，因此固定返回 false。
    /// </summary>
    /// <returns>始终返回 false。</returns>
    public Task<bool> IsMatchingState() => Task.FromResult(false);

    /// <summary>向执行环境写入“本次由应用启动触发”。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment) => environment.Startup = true;

    /// <summary>创建深拷贝。该触发器无可变配置。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new OnStartupAutomationPipelineTrigger();

    /// <summary>同类触发器彼此等价，只比较类型。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>类型相同时返回 true。</returns>
    public override bool Equals(object? obj) => obj is OnStartupAutomationPipelineTrigger;

    /// <summary>基于显示名称生成哈希值。</summary>
    /// <returns>哈希值。</returns>
    public override int GetHashCode() => HashCode.Combine(DisplayName);
}

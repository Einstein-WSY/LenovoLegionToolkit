using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;
using LenovoLegionToolkit.Lib.System;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：外接显示器断开时执行。
/// 触发时机为系统上报外接显示器断开消息。
/// </summary>
public class ExternalDisplayDisconnectedAutomationPipelineTrigger : INativeWindowsMessagePipelineTrigger, IDisallowDuplicatesAutomationPipelineTrigger
{
    /// <summary>界面显示名称：当外接显示器断开时。</summary>
    [JsonIgnore] public string DisplayName => Resource.ExternalDisplayDisconnectedAutomationPipelineTrigger_DisplayName;

    /// <summary>
    /// 判断事件是否触发：仅接受外接显示器断开消息。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>消息为外接显示器断开时返回 true。</returns>
    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        var result = automationEvent is NativeWindowsMessageEvent { Message: NativeWindowsMessage.ExternalMonitorDisconnected };
        return Task.FromResult(result);
    }

    /// <summary>
    /// 查询当前状态：系统中不存在任何外接显示器才视为满足。
    /// </summary>
    /// <returns>没有外接显示器时返回 true。</returns>
    public Task<bool> IsMatchingState()
    {
        var result = ExternalDisplays.Get().Length < 1;
        return Task.FromResult(result);
    }

    /// <summary>向执行环境写入“未连接外接显示器”。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment) => environment.ExternalDisplayConnected = false;

    /// <summary>创建深拷贝。该触发器无可变配置。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new ExternalDisplayDisconnectedAutomationPipelineTrigger();

    /// <summary>同类触发器彼此等价，只比较类型。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>类型相同时返回 true。</returns>
    public override bool Equals(object? obj) => obj is ExternalDisplayDisconnectedAutomationPipelineTrigger;

    /// <summary>基于显示名称生成哈希值。</summary>
    /// <returns>哈希值。</returns>
    public override int GetHashCode() => HashCode.Combine(DisplayName);
}

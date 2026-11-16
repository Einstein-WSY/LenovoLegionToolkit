using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;
using LenovoLegionToolkit.Lib.Listeners;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：笔记本上盖打开时执行。
/// 触发时机为系统上报开盖消息。
/// </summary>
public class LidOpenedAutomationPipelineTrigger : INativeWindowsMessagePipelineTrigger, IDisallowDuplicatesAutomationPipelineTrigger
{
    /// <summary>界面显示名称：当打开盖子时。</summary>
    [JsonIgnore]
    public string DisplayName => Resource.LidOpenedAutomationPipelineTrigger_DisplayName;

    /// <summary>
    /// 判断事件是否触发：仅接受开盖的原生消息。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>消息为开盖时返回 true。</returns>
    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        var result = automationEvent is NativeWindowsMessageEvent { Message: NativeWindowsMessage.LidOpened };
        return Task.FromResult(result);
    }

    /// <summary>
    /// 查询当前状态：监听器缓存的上盖状态为“已打开”。
    /// </summary>
    /// <returns>上盖当前处于打开状态时返回 true。</returns>
    public Task<bool> IsMatchingState()
    {
        var listener = IoCContainer.Resolve<NativeWindowsMessageListener>();
        var result = listener.IsLidOpen;
        return Task.FromResult(result);
    }

    /// <summary>向执行环境写入“上盖已打开”。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment) => environment.LidOpen = true;

    /// <summary>创建深拷贝。该触发器无可变配置。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new LidOpenedAutomationPipelineTrigger();

    /// <summary>同类触发器彼此等价，只比较类型。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>类型相同时返回 true。</returns>
    public override bool Equals(object? obj) => obj is LidOpenedAutomationPipelineTrigger;

    /// <summary>基于显示名称生成哈希值。</summary>
    /// <returns>哈希值。</returns>
    public override int GetHashCode() => HashCode.Combine(DisplayName);
}

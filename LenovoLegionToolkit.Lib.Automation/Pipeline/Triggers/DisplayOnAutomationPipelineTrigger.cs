using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;
using LenovoLegionToolkit.Lib.Listeners;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：显示器点亮（唤醒屏幕）时执行。
/// 触发时机为系统上报显示器开启消息。
/// </summary>
public class DisplayOnAutomationPipelineTrigger : INativeWindowsMessagePipelineTrigger, IDisallowDuplicatesAutomationPipelineTrigger
{
    /// <summary>界面显示名称：当显示器开启时。</summary>
    [JsonIgnore]
    public string DisplayName => Resource.DisplayOnAutomationPipelineTrigger_DisplayName;

    /// <summary>
    /// 判断事件是否触发：仅接受显示器开启的原生消息。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>消息为显示器开启时返回 true。</returns>
    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        var result = automationEvent is NativeWindowsMessageEvent { Message: NativeWindowsMessage.MonitorOn };
        return Task.FromResult(result);
    }

    /// <summary>
    /// 查询当前状态：从容器取出原生消息监听器并读取其缓存的显示器状态。
    /// </summary>
    /// <returns>显示器当前处于点亮状态时返回 true。</returns>
    public Task<bool> IsMatchingState()
    {
        var listener = IoCContainer.Resolve<NativeWindowsMessageListener>();
        var result = listener.IsMonitorOn;
        return Task.FromResult(result);
    }

    /// <summary>向执行环境写入“显示器已点亮”。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment) => environment.DisplayOn = true;

    /// <summary>创建深拷贝。该触发器无可变配置。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new DisplayOnAutomationPipelineTrigger();

    /// <summary>同类触发器彼此等价，只比较类型。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>类型相同时返回 true。</returns>
    public override bool Equals(object? obj) => obj is DisplayOnAutomationPipelineTrigger;

    /// <summary>基于显示名称生成哈希值。</summary>
    /// <returns>哈希值。</returns>
    public override int GetHashCode() => HashCode.Combine(DisplayName);
}

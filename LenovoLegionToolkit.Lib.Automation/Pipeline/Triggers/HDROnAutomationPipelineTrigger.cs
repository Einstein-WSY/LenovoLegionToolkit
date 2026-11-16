using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;
using LenovoLegionToolkit.Lib.Listeners;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：HDR 开启时执行。
/// 触发时机为显示配置监听器报告 HDR 状态切换为开启。
/// </summary>
public class HDROnAutomationPipelineTrigger : IHDRPipelineTrigger
{
    /// <summary>界面显示名称：当 HDR 开启时。</summary>
    [JsonIgnore]
    public string DisplayName => Resource.HDROnAutomationPipelineTrigger_DisplayName;

    /// <summary>
    /// 判断事件是否触发：事件必须是 HDR 事件且状态为开启。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>HDR 已开启时返回 true。</returns>
    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        var result = automationEvent is HDRAutomationEvent { IsHDROn: true };
        return Task.FromResult(result);
    }

    /// <summary>
    /// 查询当前状态：监听器的 HDR 状态为可确定值且为开启。
    /// </summary>
    /// <returns>HDR 当前为开启状态时返回 true。</returns>
    public Task<bool> IsMatchingState()
    {
        var listener = IoCContainer.Resolve<DisplayConfigurationListener>();
        var result = listener.IsHDROn;
        // 状态未知（null）时按“不满足”处理，避免在无法判断时误触发。
        return Task.FromResult(result.HasValue && result.Value);
    }

    /// <summary>向执行环境写入“HDR 已开启”。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment) => environment.HDROn = true;

    /// <summary>创建深拷贝。该触发器无可变配置。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new HDROnAutomationPipelineTrigger();

    /// <summary>同类触发器彼此等价，只比较类型。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>类型相同时返回 true。</returns>
    public override bool Equals(object? obj) => obj is HDROnAutomationPipelineTrigger;

    /// <summary>基于显示名称生成哈希值。</summary>
    /// <returns>哈希值。</returns>
    public override int GetHashCode() => HashCode.Combine(DisplayName);
}

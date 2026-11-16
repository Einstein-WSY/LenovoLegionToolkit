using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;
using LenovoLegionToolkit.Lib.Listeners;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：Windows 会话锁定时执行。
/// 触发时机为会话锁定/解锁事件中的“已锁定”。
/// </summary>
public class SessionLockAutomationPipelineTrigger : ISessionLockPipelineTrigger
{
    /// <summary>界面显示名称：当会话锁定时。</summary>
    [JsonIgnore]
    public string DisplayName => Resource.SessionLockAutomationPipelineTrigger_DisplayName;

    /// <summary>
    /// 判断事件是否触发：事件必须是会话状态变化且处于锁定状态。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>会话锁定时返回 true。</returns>
    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        var result = automationEvent is SessionLockUnlockAutomationEvent { Locked: true };
        return Task.FromResult(result);
    }

    /// <summary>
    /// 查询当前状态：监听器的锁定状态为可确定值且为已锁定。
    /// </summary>
    /// <returns>会话当前处于锁定状态时返回 true。</returns>
    public Task<bool> IsMatchingState()
    {
        var listener = IoCContainer.Resolve<SessionLockUnlockListener>();
        var result = listener.IsLocked;
        // 状态未知（null）时按“不满足”处理。
        return Task.FromResult(result.HasValue && result.Value);
    }

    /// <summary>向执行环境写入“会话已锁定”。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment) => environment.SessionLocked = true;

    /// <summary>创建深拷贝。该触发器无可变配置。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new SessionLockAutomationPipelineTrigger();

    /// <summary>同类触发器彼此等价，只比较类型。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>类型相同时返回 true。</returns>
    public override bool Equals(object? obj) => obj is SessionLockAutomationPipelineTrigger;

    /// <summary>基于显示名称生成哈希值。</summary>
    /// <returns>哈希值。</returns>
    public override int GetHashCode() => HashCode.Combine(DisplayName);
}

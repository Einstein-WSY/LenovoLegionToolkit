using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.AutoListeners;
using LenovoLegionToolkit.Lib.Automation.Resources;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：用户空闲达到指定时长后执行。
/// 空闲监听器会按固定节拍上报累计空闲时长，本触发器要求时长与配置完全一致才触发，
/// 因此触发点精确落在“刚好达到阈值”的那一刻，避免同一段空闲重复触发。
/// </summary>
/// <param name="inactivityTimeSpan">空闲时长阈值；为 <see cref="TimeSpan.Zero"/> 表示“用户恢复活跃”。</param>
[method: JsonConstructor]
public class UserInactivityAutomationPipelineTrigger(TimeSpan inactivityTimeSpan) : IUserInactivityPipelineTrigger
{
    /// <summary>
    /// 界面显示名称：阈值为 0 时表示“用户活跃”，与普通空闲阈值使用不同文案。
    /// </summary>
    public string DisplayName => InactivityTimeSpan == TimeSpan.Zero
        ? Resource.UserInactivityAutomationPipelineTrigger_DisplayName_Zero
        : Resource.UserInactivityAutomationPipelineTrigger_DisplayName;

    /// <summary>空闲时长阈值。</summary>
    public TimeSpan InactivityTimeSpan { get; } = inactivityTimeSpan;

    /// <summary>
    /// 判断事件是否触发：事件中的累计空闲时长与阈值完全相等。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>空闲时长恰好达到阈值时返回 true。</returns>
    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        if (automationEvent is not UserInactivityAutomationEvent e)
            return Task.FromResult(false);

        var result = InactivityTimeSpan == e.InactivityTimeSpan;
        return Task.FromResult(result);
    }

    /// <summary>
    /// 查询当前状态：监听器当前统计的空闲时长与阈值一致。
    /// </summary>
    /// <returns>当前空闲时长等于阈值时返回 true。</returns>
    public Task<bool> IsMatchingState()
    {
        var listener = IoCContainer.Resolve<UserInactivityAutoListener>();
        var result = InactivityTimeSpan == listener.InactivityTimeSpan;
        return Task.FromResult(result);
    }

    /// <summary>
    /// 向执行环境写入用户是否活跃：阈值为 0 时代表用户刚刚恢复操作。
    /// </summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment) => environment.UserActive = InactivityTimeSpan == TimeSpan.Zero;

    /// <summary>创建深拷贝，保留空闲阈值。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new UserInactivityAutomationPipelineTrigger(InactivityTimeSpan);

    /// <summary>以新的空闲阈值创建副本（供界面编辑后重建触发器）。</summary>
    /// <param name="timeSpan">新的空闲时长阈值。</param>
    /// <returns>新的触发器实例。</returns>
    public IUserInactivityPipelineTrigger DeepCopy(TimeSpan timeSpan) => new UserInactivityAutomationPipelineTrigger(timeSpan);

    /// <summary>按空闲阈值比较是否等价。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>类型相同且阈值一致时返回 true。</returns>
    public override bool Equals(object? obj) => obj is UserInactivityAutomationPipelineTrigger t && InactivityTimeSpan == t.InactivityTimeSpan;

    /// <summary>基于空闲阈值生成哈希值。</summary>
    /// <returns>哈希值。</returns>
    public override int GetHashCode() => InactivityTimeSpan.GetHashCode();

    /// <summary>输出便于日志排查的文本表示。</summary>
    /// <returns>包含空闲阈值的字符串。</returns>
    public override string ToString() => $"{nameof(InactivityTimeSpan)}: {InactivityTimeSpan}";
}

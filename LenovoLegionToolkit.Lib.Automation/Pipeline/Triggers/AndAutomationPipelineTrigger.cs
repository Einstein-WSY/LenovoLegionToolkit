using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 复合触发器：所有子条件同时满足时才执行。
/// 匹配策略是“事件命中任一子触发器 + 其余子触发器的实时状态也成立”，
/// 这样既保留了事件驱动的触发时机，又能校验其他条件当前是否仍然成立。
/// </summary>
/// <param name="triggers">组成“与”条件的子触发器。</param>
[method: JsonConstructor]
public class AndAutomationPipelineTrigger(IAutomationPipelineTrigger[] triggers) : ICompositeAutomationPipelineTrigger
{
    /// <summary>界面显示名称：逐行拼接各子触发器的名称。</summary>
    [JsonIgnore]
    public string DisplayName => string.Join(Environment.NewLine, Triggers.Select(t => t.DisplayName));

    /// <summary>组成“与”条件的子触发器。</summary>
    public IAutomationPipelineTrigger[] Triggers { get; } = triggers;

    /// <summary>
    /// 判断事件是否触发全部“与”条件：事件需命中其中一个子触发器，
    /// 其余子触发器则以当前状态校验。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>全部条件成立时返回 true。</returns>
    public async Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        foreach (var trigger in Triggers)
        {
            // 该子触发器与事件无关，继续尝试其它子触发器作为“事件来源”。
            if (!await trigger.IsMatchingEvent(automationEvent).ConfigureAwait(false))
                continue;

            // 事件源已确定，剩余条件必须同时成立；任一不满足即整体不匹配。
            foreach (var otherTrigger in Triggers.Where(t => !ReferenceEquals(t, trigger)))
            {
                if (!await otherTrigger.IsMatchingState().ConfigureAwait(false))
                    return false;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// 复合触发器不作为其它复合条件的“状态”使用，因此固定返回 false，
    /// 避免出现复合条件互相嵌套时的语义歧义。
    /// </summary>
    /// <returns>始终返回 false。</returns>
    public Task<bool> IsMatchingState() => Task.FromResult(false);

    /// <summary>把所有子触发器的状态写入执行环境。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment)
    {
        foreach (var trigger in Triggers)
            trigger.UpdateEnvironment(environment);
    }

    /// <summary>
    /// 创建深拷贝。子触发器数组按引用复用，因为触发器本身在设计上视为不可变配置。
    /// </summary>
    /// <returns>新的复合触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new AndAutomationPipelineTrigger(Triggers);
}

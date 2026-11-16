using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：按固定周期重复执行。
/// 依赖时间监听器按分钟派发的时间事件，当“当天已过分钟数”能被周期整除时触发，
/// 因此实际触发点是与零点相距周期整数倍的时刻。
/// </summary>
/// <param name="period">重复周期。</param>
[method: JsonConstructor]
public class PeriodicAutomationPipelineTrigger(TimeSpan period) : IPeriodicAutomationPipelineTrigger
{
    /// <summary>界面显示名称：按周期执行。</summary>
    public string DisplayName => Resource.PeriodicActionPipelineTrigger_DisplayName;

    /// <summary>重复周期。</summary>
    public TimeSpan Period { get; } = period;

    /// <summary>
    /// 判断事件是否触发：只处理时间事件（作为周期节拍），其它事件一律忽略。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>到达周期触发点时返回 true。</returns>
    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        return automationEvent is not TimeAutomationEvent ? Task.FromResult(false) : IsMatching();
    }

    /// <summary>
    /// 查询当前状态：此刻是否正好落在周期的整数倍上。
    /// </summary>
    /// <returns>当前处于触发点时返回 true。</returns>
    public Task<bool> IsMatchingState() => IsMatching();

    /// <summary>创建深拷贝，保留周期配置。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new PeriodicAutomationPipelineTrigger(Period);

    /// <summary>以新的周期创建副本（供界面编辑后重建触发器）。</summary>
    /// <param name="period">新的重复周期。</param>
    /// <returns>新的触发器实例。</returns>
    public IPeriodicAutomationPipelineTrigger DeepCopy(TimeSpan period) => new PeriodicAutomationPipelineTrigger(period);

    /// <summary>
    /// 以“当天零点起的分钟数”对周期分钟数取余，余数为 0 即到达触发点。
    /// 周期应大于 0；为 0 时余数无意义，结果恒为不匹配。
    /// </summary>
    /// <returns>到达触发点时返回 true。</returns>
    private Task<bool> IsMatching()
    {
        var currentDayMinutes = (int)DateTime.Now.TimeOfDay.TotalMinutes;
        var isPeriod = currentDayMinutes % Period.TotalMinutes == 0;

        return Task.FromResult(isPeriod);
    }

    /// <summary>向执行环境写入周期长度。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment) => environment.Period = Period;
}

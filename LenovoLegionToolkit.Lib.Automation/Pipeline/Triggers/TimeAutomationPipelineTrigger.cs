using System;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：到达指定时刻、日出或日落时执行。
/// 时间监听器按分钟派发时间事件，本触发器据此比对配置的固定时刻、日出/日落时刻，
/// 并按需校验当天是否为允许的星期。
/// </summary>
/// <param name="isSunrise">是否在日出时刻触发。</param>
/// <param name="isSunset">是否在日落时刻触发。</param>
/// <param name="time">固定触发时刻；为 null 表示不使用固定时刻。</param>
/// <param name="days">限定生效的星期；为 null 或空表示每天生效。</param>
[method: JsonConstructor]
public class TimeAutomationPipelineTrigger(bool isSunrise, bool isSunset, Time? time, DayOfWeek[]? days)
    : ITimeAutomationPipelineTrigger
{
    /// <summary>是否在日出时刻触发。</summary>
    public bool IsSunrise { get; } = isSunrise;

    /// <summary>是否在日落时刻触发。</summary>
    public bool IsSunset { get; } = isSunset;

    /// <summary>固定触发时刻；为 null 表示只使用日出/日落条件。</summary>
    public Time? Time { get; } = time;

    /// <summary>限定生效的星期；为空表示每天生效。</summary>
    public DayOfWeek[] Days { get; } = days ?? [];

    /// <summary>界面显示名称：当到达指定时间时。</summary>
    [JsonIgnore]
    public string DisplayName => Resource.TimeAutomationPipelineTrigger_DisplayName;

    // 日出/日落时间依赖地理位置，由全局定位服务计算，需按需拉取。
    private readonly SunriseSunset _sunriseSunset = IoCContainer.Resolve<SunriseSunset>();

    /// <summary>
    /// 判断事件是否触发：只处理时间事件，并用事件携带的时刻与星期进行比对。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>时刻（及星期）匹配时返回 true。</returns>
    public async Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        if (automationEvent is not TimeAutomationEvent e)
            return false;

        return await IsMatching(e.Time, e.Day).ConfigureAwait(false);
    }

    /// <summary>
    /// 查询当前状态：以当前时间（UTC）构造时刻与星期进行比对。
    /// </summary>
    /// <returns>当前时刻匹配时返回 true。</returns>
    public async Task<bool> IsMatchingState()
    {
        var now = DateTime.UtcNow;
        var time = new Time(now.Hour, now.Minute);
        var day = now.DayOfWeek;

        return await IsMatching(time, day).ConfigureAwait(false);
    }

    /// <summary>向执行环境写入日出/日落标志、时刻与星期配置。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment)
    {
        environment.IsSunset = IsSunset;
        environment.IsSunrise = IsSunrise;
        environment.Time = Time;
        environment.Days = Days;
    }

    /// <summary>
    /// 匹配逻辑：先判断固定时刻（若配置了星期则还需当天命中）；
    /// 未命中时再判断是否为当天的日出或日落时刻。
    /// </summary>
    /// <param name="time">待比对的时间点。</param>
    /// <param name="dayOfWeek">待比对的星期。</param>
    /// <returns>任意条件成立时返回 true。</returns>
    private async Task<bool> IsMatching(Time time, DayOfWeek dayOfWeek)
    {
        if (Time == time && (Days.IsEmpty() || Days.Contains(dayOfWeek)))
            return true;

        // 只有启用了日出/日落条件时才需要计算太阳时刻，避免不必要的定位与计算开销。
        var (sunrise, sunset) = await _sunriseSunset.GetSunriseSunsetAsync().ConfigureAwait(false);

        if (IsSunrise && sunrise == time)
            return true;

        if (IsSunset && sunset == time)
            return true;

        return false;
    }
    /// <summary>创建深拷贝，保留全部时间条件。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new TimeAutomationPipelineTrigger(IsSunrise, IsSunset, Time, Days);

    /// <summary>以新的时间条件创建副本（供界面编辑后重建触发器）。</summary>
    /// <param name="isSunrise">是否在日出时触发。</param>
    /// <param name="isSunset">是否在日落时触发。</param>
    /// <param name="time">固定触发时刻。</param>
    /// <param name="days">限定生效的星期。</param>
    /// <returns>新的触发器实例。</returns>
    public ITimeAutomationPipelineTrigger DeepCopy(bool isSunrise, bool isSunset, Time? time, DayOfWeek[] days) => new TimeAutomationPipelineTrigger(isSunrise, isSunset, time, days);

    /// <summary>
    /// 比较时间条件是否等价。<see cref="Days"/> 按元素逐一比较。
    /// </summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>全部条件一致时返回 true。</returns>
    public override bool Equals(object? obj)
    {
        return obj is TimeAutomationPipelineTrigger t &&
               IsSunrise == t.IsSunrise &&
               IsSunset == t.IsSunset &&
               Time == t.Time &&
               Days.SequenceEqual(t.Days);
    }

    /// <summary>基于全部时间条件生成哈希值，Days 逐元素累加。</summary>
    /// <returns>哈希值。</returns>
    public override int GetHashCode()
    {
        var hc = new HashCode();
        hc.Add(IsSunrise);
        hc.Add(IsSunset);
        hc.Add(Time);
        Days.ForEach(d => hc.Add(d));
        return hc.ToHashCode();
    }

    /// <summary>输出便于日志排查的文本表示。</summary>
    /// <returns>包含各时间条件的字符串。</returns>
    public override string ToString() =>
        $"{nameof(IsSunrise)}: {IsSunrise}," +
        $" {nameof(IsSunset)}: {IsSunset}," +
        $" {nameof(Time)}: {Time}," +
        $" {nameof(Days)}: {Days}";
}

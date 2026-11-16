using System;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 流水线触发器接口。触发器同时承担三件事：
/// 判断某个事件是否满足触发条件、判断当前系统状态是否满足条件、把自身代表的状态写入执行环境。
/// </summary>
public interface IAutomationPipelineTrigger
{
    /// <summary>界面显示名称（已本地化，不参与序列化）。</summary>
    [JsonIgnore]
    string DisplayName { get; }

    /// <summary>
    /// 判断事件是否满足触发条件。仅在事件发生时调用。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>满足条件返回 true。</returns>
    Task<bool> IsMatchingEvent(IAutomationEvent automationEvent);

    /// <summary>
    /// 判断当前系统状态是否满足条件。用于复合触发器校验其余子条件的实时状态。
    /// </summary>
    /// <returns>当前状态满足条件返回 true。</returns>
    Task<bool> IsMatchingState();

    /// <summary>
    /// 把触发器代表的状态写入执行环境，供后续步骤以环境变量形式读取。
    /// </summary>
    /// <param name="environment">流水线执行环境。</param>
    void UpdateEnvironment(AutomationEnvironment environment);

    /// <summary>
    /// 创建触发器的深拷贝，用于对外返回时避免共享可变状态。
    /// </summary>
    /// <returns>新的触发器实例。</returns>
    IAutomationPipelineTrigger DeepCopy();
}

/// <summary>
/// 标记接口：同一条流水线中不允许重复添加同类触发器（例如两个“电源接入”触发器没有意义）。
/// </summary>
public interface IDisallowDuplicatesAutomationPipelineTrigger : IAutomationPipelineTrigger;

/// <summary>
/// 复合触发器：由多个子触发器按特定逻辑组合（当前为“与”关系）。
/// </summary>
public interface ICompositeAutomationPipelineTrigger : IAutomationPipelineTrigger
{
    /// <summary>组成复合条件的子触发器。</summary>
    public IAutomationPipelineTrigger[] Triggers { get; }
}

/// <summary>HDR 开关类触发器（开/关互斥，不允许同时添加）。</summary>
public interface IHDRPipelineTrigger : IDisallowDuplicatesAutomationPipelineTrigger;

/// <summary>基于原生 Windows 消息的触发器（合盖、显示器开关、设备插拔等）。</summary>
public interface INativeWindowsMessagePipelineTrigger : IAutomationPipelineTrigger;

/// <summary>
/// 设备连接类触发器：需要记录关注的设备实例 ID。
/// </summary>
public interface IDeviceAutomationPipelineTrigger : INativeWindowsMessagePipelineTrigger
{
    /// <summary>关注的设备实例 ID 列表。</summary>
    string[] InstanceIds { get; }

    /// <summary>
    /// 使用新的设备 ID 集合创建副本，供界面编辑配置后生成新触发器。
    /// </summary>
    /// <param name="instanceIds">新的设备实例 ID 列表。</param>
    /// <returns>新的触发器实例。</returns>
    IDeviceAutomationPipelineTrigger DeepCopy(string[] instanceIds);
}

/// <summary>应用启动时触发。</summary>
public interface IOnStartupAutomationPipelineTrigger : IDisallowDuplicatesAutomationPipelineTrigger;

/// <summary>系统从睡眠/休眠恢复时触发。</summary>
public interface IOnResumeAutomationPipelineTrigger : IDisallowDuplicatesAutomationPipelineTrigger;

/// <summary>电源状态变化类触发器（适配器接入/断开等）。</summary>
public interface IPowerStateAutomationPipelineTrigger : IDisallowDuplicatesAutomationPipelineTrigger;

/// <summary>
/// 性能模式切换触发器：需要记录关注的性能模式。
/// </summary>
public interface IPowerModeAutomationPipelineTrigger : IAutomationPipelineTrigger
{
    /// <summary>关注的性能模式。</summary>
    PowerModeState PowerModeState { get; }

    /// <summary>
    /// 使用新的性能模式创建副本。
    /// </summary>
    /// <param name="powerModeState">新的性能模式。</param>
    /// <returns>新的触发器实例。</returns>
    IPowerModeAutomationPipelineTrigger DeepCopy(PowerModeState powerModeState);
}

/// <summary>
/// 自定义模式预设切换触发器：需要记录关注的预设 ID。
/// </summary>
public interface IGodModePresetChangedAutomationPipelineTrigger : IAutomationPipelineTrigger
{
    /// <summary>关注的预设 ID。</summary>
    Guid PresetId { get; }

    /// <summary>
    /// 使用新的预设 ID 创建副本。
    /// </summary>
    /// <param name="powerModeState">新的预设 ID（参数名沿用历史命名）。</param>
    /// <returns>新的触发器实例。</returns>
    IGodModePresetChangedAutomationPipelineTrigger DeepCopy(Guid powerModeState);
}

/// <summary>游戏开始/停止类触发器。</summary>
public interface IGameAutomationPipelineTrigger : IDisallowDuplicatesAutomationPipelineTrigger;

/// <summary>
/// 进程启动/停止类触发器：需要记录关注的进程集合。
/// </summary>
public interface IProcessesAutomationPipelineTrigger : IAutomationPipelineTrigger
{
    /// <summary>关注的进程集合。</summary>
    ProcessInfo[] Processes { get; }

    /// <summary>
    /// 使用新的进程集合创建副本。
    /// </summary>
    /// <param name="processes">新的进程集合。</param>
    /// <returns>新的触发器实例。</returns>
    IProcessesAutomationPipelineTrigger DeepCopy(ProcessInfo[] processes);
}

/// <summary>会话锁定时触发。</summary>
public interface ISessionLockPipelineTrigger : IDisallowDuplicatesAutomationPipelineTrigger;

/// <summary>会话解锁时触发。</summary>
public interface ISessionUnlockPipelineTrigger : IDisallowDuplicatesAutomationPipelineTrigger;

/// <summary>
/// 时间类触发器：支持固定时刻、日出、日落以及限定星期。
/// </summary>
public interface ITimeAutomationPipelineTrigger : IAutomationPipelineTrigger
{
    /// <summary>是否在日出时刻触发。</summary>
    bool IsSunrise { get; }
    /// <summary>是否在日落时刻触发。</summary>
    bool IsSunset { get; }
    /// <summary>固定触发时刻；为 null 表示只使用日出/日落条件。</summary>
    Time? Time { get; }
    /// <summary>限定生效的星期；为空表示每天生效。</summary>
    DayOfWeek[] Days { get; }

    /// <summary>
    /// 使用新的时间条件创建副本。
    /// </summary>
    /// <param name="isSunrise">是否在日出时触发。</param>
    /// <param name="isSunset">是否在日落时触发。</param>
    /// <param name="time">固定触发时刻。</param>
    /// <param name="day">限定生效的星期。</param>
    /// <returns>新的触发器实例。</returns>
    ITimeAutomationPipelineTrigger DeepCopy(bool isSunrise, bool isSunset, Time? time, DayOfWeek[] day);
}

/// <summary>
/// 用户空闲触发器：需要记录空闲时长阈值。
/// </summary>
public interface IUserInactivityPipelineTrigger : IAutomationPipelineTrigger
{
    /// <summary>空闲时长阈值。</summary>
    TimeSpan InactivityTimeSpan { get; }

    /// <summary>
    /// 使用新的空闲时长创建副本。
    /// </summary>
    /// <param name="timeSpan">新的空闲时长阈值。</param>
    /// <returns>新的触发器实例。</returns>
    IUserInactivityPipelineTrigger DeepCopy(TimeSpan timeSpan);
}

/// <summary>
/// Wi-Fi 连接触发器：需要记录关注的 SSID 列表。
/// </summary>
public interface IWiFiConnectedPipelineTrigger : IAutomationPipelineTrigger
{
    /// <summary>关注的 SSID 列表；为空表示任意网络。</summary>
    string[] Ssids { get; }

    /// <summary>
    /// 使用新的 SSID 列表创建副本。
    /// </summary>
    /// <param name="ssids">新的 SSID 列表。</param>
    /// <returns>新的触发器实例。</returns>
    IWiFiConnectedPipelineTrigger DeepCopy(string[] ssids);
}

/// <summary>Wi-Fi 断开时触发。</summary>
public interface IWiFiDisconnectedPipelineTrigger : IDisallowDuplicatesAutomationPipelineTrigger;

/// <summary>
/// 周期触发器：按固定时间间隔重复触发。
/// </summary>
public interface IPeriodicAutomationPipelineTrigger : IAutomationPipelineTrigger
{
    /// <summary>触发周期。</summary>
    public TimeSpan Period { get; }

    /// <summary>
    /// 使用新的周期创建副本。
    /// </summary>
    /// <param name="period">新的触发周期。</param>
    /// <returns>新的触发器实例。</returns>
    IPeriodicAutomationPipelineTrigger DeepCopy(TimeSpan period);
}

using System;

namespace LenovoLegionToolkit.Lib.Automation;

/// <summary>
/// 自动化事件标记接口。各监听器把系统变更包装成具体事件，由流水线触发器判断是否匹配。
/// </summary>
public interface IAutomationEvent;

/// <summary>
/// HDR 状态变化事件。
/// </summary>
/// <param name="isHDROn">变化后的 HDR 状态；为 null 表示无法确定当前状态。</param>
public readonly struct HDRAutomationEvent(bool? isHDROn) : IAutomationEvent
{
    /// <summary>变化后的 HDR 状态；null 表示未知。</summary>
    public bool? IsHDROn { get; } = isHDROn;
}

/// <summary>
/// 原生 Windows 消息事件（锁屏、合盖、显示器开关、设备插拔等）。
/// </summary>
/// <param name="message">消息类型。</param>
/// <param name="data">消息附带的负载，例如设备实例 ID。</param>
public readonly struct NativeWindowsMessageEvent(NativeWindowsMessage message, object? data) : IAutomationEvent
{
    /// <summary>消息类型。</summary>
    public NativeWindowsMessage Message { get; } = message;
    /// <summary>消息负载，内容随消息类型而定。</summary>
    public object? Data { get; } = data;
}

/// <summary>
/// 应用启动事件，无附加数据。
/// </summary>
public struct StartupAutomationEvent : IAutomationEvent;

/// <summary>
/// 电源状态变化事件（交流电接入/断开、睡眠/恢复等）。
/// </summary>
/// <param name="powerStateEvent">具体的电源状态类型。</param>
/// <param name="powerAdapterStateChanged">本次变化是否由电源适配器插拔引起。</param>
public readonly struct PowerStateAutomationEvent(PowerStateEvent powerStateEvent, bool powerAdapterStateChanged)
    : IAutomationEvent
{
    /// <summary>具体的电源状态类型。</summary>
    public PowerStateEvent PowerStateEvent { get; } = powerStateEvent;
    /// <summary>是否由电源适配器插拔引起；用于区分“恢复”与“插上电源”等场景。</summary>
    public bool PowerAdapterStateChanged { get; } = powerAdapterStateChanged;
}

/// <summary>
/// 性能模式切换事件。
/// </summary>
/// <param name="powerModeState">切换后的性能模式。</param>
public readonly struct PowerModeAutomationEvent(PowerModeState powerModeState) : IAutomationEvent
{
    /// <summary>切换后的性能模式。</summary>
    public PowerModeState PowerModeState { get; } = powerModeState;
}

/// <summary>
/// 自定义模式（God Mode）预设切换事件。
/// </summary>
/// <param name="id">被激活的预设 ID。</param>
public readonly struct CustomModePresetAutomationEvent(Guid id) : IAutomationEvent
{
    /// <summary>被激活的预设 ID。</summary>
    public Guid Id { get; } = id;
}

/// <summary>
/// 游戏运行状态变化事件。
/// </summary>
/// <param name="running">是否有游戏正在运行。</param>
public readonly struct GameAutomationEvent(bool running) : IAutomationEvent
{
    /// <summary>是否有游戏正在运行。</summary>
    public bool Running { get; } = running;
}

/// <summary>
/// 进程启动/停止事件。
/// </summary>
/// <param name="type">事件类型（启动或停止）。</param>
/// <param name="processInfo">相关进程信息。</param>
public readonly struct ProcessAutomationEvent(ProcessEventInfoType type, ProcessInfo processInfo) : IAutomationEvent
{
    /// <summary>事件类型（启动或停止）。</summary>
    public ProcessEventInfoType Type { get; } = type;

    /// <summary>相关进程信息。</summary>
    public ProcessInfo ProcessInfo { get; } = processInfo;
}

/// <summary>
/// 会话锁定/解锁事件。
/// </summary>
/// <param name="locked">会话是否已锁定。</param>
public readonly struct SessionLockUnlockAutomationEvent(bool locked) : IAutomationEvent
{
    /// <summary>会话是否已锁定。</summary>
    public bool Locked { get; } = locked;
}

/// <summary>
/// 时间到达事件（用于定时与周期触发）。
/// </summary>
/// <param name="time">触发时刻。</param>
/// <param name="day">触发当天的星期。</param>
public readonly struct TimeAutomationEvent(Time time, DayOfWeek day) : IAutomationEvent
{
    /// <summary>触发时刻。</summary>
    public Time Time { get; } = time;
    /// <summary>触发当天的星期。</summary>
    public DayOfWeek Day { get; } = day;
}

/// <summary>
/// 用户空闲状态事件。
/// </summary>
/// <param name="inactivityTimeSpan">用户已持续未操作的时间长度。</param>
public readonly struct UserInactivityAutomationEvent(TimeSpan inactivityTimeSpan)
    : IAutomationEvent
{
    /// <summary>用户已持续未操作的时间长度。</summary>
    public TimeSpan InactivityTimeSpan { get; } = inactivityTimeSpan;
}

/// <summary>
/// Wi-Fi 连接状态变化事件。
/// </summary>
/// <param name="isConnected">是否已连接。</param>
/// <param name="ssid">当前连接的 SSID；未连接时为 null。</param>
public readonly struct WiFiAutomationEvent(bool isConnected, string? ssid) : IAutomationEvent
{
    /// <summary>是否已连接 Wi-Fi。</summary>
    public bool IsConnected { get; } = isConnected;
    /// <summary>当前连接的 SSID；未连接时为 null。</summary>
    public string? Ssid { get; } = ssid;
}

using System;
using System.Collections.Generic;
using System.Linq;

namespace LenovoLegionToolkit.Lib.Automation;

/// <summary>
/// 流水线执行环境。
/// 触发器匹配成功后会把自身代表的当前状态写入此对象，随后该对象被序列化为键值字典，
/// 供后续步骤（例如运行脚本）以环境变量的形式读取。
/// </summary>
public class AutomationEnvironment
{
    // 下面这些常量即对外暴露的“环境变量名”。布尔值统一写成 "TRUE"/"FALSE" 字符串，
    // 便于外部脚本用字符串比较的方式判断，而不依赖数字约定。
    private const string AC_ADAPTER_CONNECTED = "LLT_IS_AC_ADAPTER_CONNECTED";
    private const string LOW_POWER_AC_ADAPTER = "LLT_IS_AC_ADAPTER_LOW_POWER";
    private const string DISPLAY_ON = "LLT_IS_DISPLAY_ON";
    private const string EXTERNAL_DISPLAY_CONNECTED = "LLT_IS_EXTERNAL_DISPLAY_CONNECTED";
    private const string GAME_RUNNING = "LLT_IS_GAME_RUNNING";
    private const string HDR_ON = "LLT_IS_HDR_ON";
    private const string LID_OPEN = "LLT_IS_LID_OPEN";
    private const string STARTUP = "LLT_STARTUP";
    private const string RESUME = "LLT_RESUME";
    private const string POWER_MODE = "LLT_POWER_MODE";
    private const string POWER_MODE_NAME = "LLT_POWER_MODE_NAME";
    private const string PROCESSES_STARTED = "LLT_PROCESSES_STARTED";
    private const string PROCESSES = "LLT_PROCESSES";
    private const string DEVICE_CONNECTED = "LLT_DEVICE_CONNECTED";
    private const string DEVICE_INSTANCE_IDS = "LLT_DEVICE_INSTANCE_IDS";
    private const string IS_SUNSET = "LLT_IS_SUNSET";
    private const string IS_SUNRISE = "LLT_IS_SUNRISE";
    private const string TIME = "LLT_TIME";
    private const string DAYS = "LLT_DAYS";
    private const string PERIOD = "LLT_PERIOD";
    private const string USER_ACTIVE = "LLT_IS_USER_ACTIVE";
    private const string WIFI_CONNECTED = "LLT_WIFI_CONNECTED";
    private const string WIFI_SSID = "LLT_WIFI_SSID";
    private const string SESSION_LOCKED = "LLT_SESSION_LOCKED";

    private const string VALUE_TRUE = "TRUE";
    private const string VALUE_FALSE = "FALSE";

    /// <summary>电源适配器是否已连接（环境变量 <c>LLT_IS_AC_ADAPTER_CONNECTED</c>）。</summary>
    public bool AcAdapterConnected { set => _dictionary[AC_ADAPTER_CONNECTED] = value ? VALUE_TRUE : VALUE_FALSE; }

    /// <summary>当前连接的电源适配器是否为低功率适配器（环境变量 <c>LLT_IS_AC_ADAPTER_LOW_POWER</c>）。</summary>
    public bool LowPowerAcAdapter { set => _dictionary[LOW_POWER_AC_ADAPTER] = value ? VALUE_TRUE : VALUE_FALSE; }

    /// <summary>内屏是否处于点亮状态（环境变量 <c>LLT_IS_DISPLAY_ON</c>）。</summary>
    public bool DisplayOn { set => _dictionary[DISPLAY_ON] = value ? VALUE_TRUE : VALUE_FALSE; }

    /// <summary>是否连接了外接显示器（环境变量 <c>LLT_IS_EXTERNAL_DISPLAY_CONNECTED</c>）。</summary>
    public bool ExternalDisplayConnected { set => _dictionary[EXTERNAL_DISPLAY_CONNECTED] = value ? VALUE_TRUE : VALUE_FALSE; }

    /// <summary>是否有游戏进程正在运行（环境变量 <c>LLT_IS_GAME_RUNNING</c>）。</summary>
    public bool GameRunning { set => _dictionary[GAME_RUNNING] = value ? VALUE_TRUE : VALUE_FALSE; }

    /// <summary>HDR 是否已开启（环境变量 <c>LLT_IS_HDR_ON</c>）。</summary>
    public bool HDROn { set => _dictionary[HDR_ON] = value ? VALUE_TRUE : VALUE_FALSE; }

    /// <summary>笔记本上盖是否处于打开状态（环境变量 <c>LLT_IS_LID_OPEN</c>）。</summary>
    public bool LidOpen { set => _dictionary[LID_OPEN] = value ? VALUE_TRUE : VALUE_FALSE; }

    /// <summary>本次执行是否由开机启动触发（环境变量 <c>LLT_STARTUP</c>）。</summary>
    public bool Startup { set => _dictionary[STARTUP] = value ? VALUE_TRUE : VALUE_FALSE; }

    /// <summary>本次执行是否由系统从睡眠/休眠恢复触发（环境变量 <c>LLT_RESUME</c>）。</summary>
    public bool Resume { set => _dictionary[RESUME] = value ? VALUE_TRUE : VALUE_FALSE; }

    /// <summary>
    /// 当前性能模式。除了写入数值形式的 <c>LLT_POWER_MODE</c>（1/2/3/255）外，
    /// 还会写入可读名称 <c>LLT_POWER_MODE_NAME</c>（QUIET/BALANCE/PERFORMANCE/CUSTOM），
    /// 方便脚本按名称判断而无需记忆数值含义。
    /// </summary>
    public PowerModeState PowerMode
    {
        set
        {
            _dictionary[POWER_MODE] = value switch
            {
                PowerModeState.Quiet => "1",
                PowerModeState.Balance => "2",
                PowerModeState.Performance => "3",
                PowerModeState.GodMode => "255",
                _ => string.Empty
            };
            _dictionary[POWER_MODE_NAME] = value switch
            {
                PowerModeState.Quiet => "QUIET",
                PowerModeState.Balance => "BALANCE",
                PowerModeState.Performance => "PERFORMANCE",
                PowerModeState.GodMode => "CUSTOM",
                _ => string.Empty
            };
        }
    }

    /// <summary>关注的进程此前是否处于运行状态（环境变量 <c>LLT_PROCESSES_STARTED</c>）。</summary>
    public bool ProcessesStarted { set => _dictionary[PROCESSES_STARTED] = value ? VALUE_TRUE : VALUE_FALSE; }

    /// <summary>关注的进程名列表，以逗号分隔（环境变量 <c>LLT_PROCESSES</c>）。</summary>
    public ProcessInfo[] Processes { set => _dictionary[PROCESSES] = string.Join(",", value.Select(p => p.Name)); }

    /// <summary>关注的设备是否已连接（环境变量 <c>LLT_DEVICE_CONNECTED</c>）。</summary>
    public bool DeviceConnected { set => _dictionary[DEVICE_CONNECTED] = value ? VALUE_TRUE : VALUE_FALSE; }

    /// <summary>关注的设备实例 ID 列表，以逗号分隔（环境变量 <c>LLT_DEVICE_INSTANCE_IDS</c>）。</summary>
    public string[] DeviceInstanceIds { set => _dictionary[DEVICE_INSTANCE_IDS] = string.Join(",", value); }

    /// <summary>当前时间是否命中日落时刻（环境变量 <c>LLT_IS_SUNSET</c>）。</summary>
    public bool IsSunset { set => _dictionary[IS_SUNSET] = value ? VALUE_TRUE : VALUE_FALSE; }

    /// <summary>当前时间是否命中日出时刻（环境变量 <c>LLT_IS_SUNRISE</c>）。</summary>
    public bool IsSunrise { set => _dictionary[IS_SUNRISE] = value ? VALUE_TRUE : VALUE_FALSE; }

    /// <summary>
    /// 触发器设定的时间点，格式为 <c>H:m</c>（小时:分钟）。
    /// 为 null 时写入 null 表示“无固定时间”，区别于空字符串。
    /// </summary>
    public Time? Time { set => _dictionary[TIME] = value is null ? null : $"{value.Value.Hour}:{value.Value.Minute}"; }

    /// <summary>
    /// 触发器生效的星期集合，以逗号分隔的英文大写名称（如 <c>MONDAY,FRIDAY</c>）。
    /// 空数组表示不限星期，因此写入 null 而非空字符串。
    /// </summary>
    public DayOfWeek[] Days { set => _dictionary[DAYS] = value.Length < 1 ? null : string.Join(",", value.Select(v => v.ToString().ToUpperInvariant())); }

    /// <summary>周期触发的间隔，以“秒”为单位的整数文本写入（环境变量 <c>LLT_PERIOD</c>）。</summary>
    public TimeSpan Period { set => _dictionary[PERIOD] = $"{(int)value.TotalSeconds}"; }

    /// <summary>用户当前是否处于活跃状态（环境变量 <c>LLT_IS_USER_ACTIVE</c>）。</summary>
    public bool UserActive { set => _dictionary[USER_ACTIVE] = value ? VALUE_TRUE : VALUE_FALSE; }

    /// <summary>Wi-Fi 是否已连接（环境变量 <c>LLT_WIFI_CONNECTED</c>）。</summary>
    public bool WiFiConnected { set => _dictionary[WIFI_CONNECTED] = value ? VALUE_TRUE : VALUE_FALSE; }

    /// <summary>当前连接的 Wi-Fi SSID；未连接时为 null（环境变量 <c>LLT_WIFI_SSID</c>）。</summary>
    public string? WiFiSsid { set => _dictionary[WIFI_SSID] = value; }

    /// <summary>当前会话是否已锁定（环境变量 <c>LLT_SESSION_LOCKED</c>）。</summary>
    public bool SessionLocked { set => _dictionary[SESSION_LOCKED] = value ? VALUE_TRUE : VALUE_FALSE; }

    /// <summary>
    /// 获取环境变量字典的副本。返回新实例而非内部字段，避免调用方直接篡改运行期状态。
    /// </summary>
    public Dictionary<string, string?> Dictionary => new(_dictionary);

    // 实际的键值存储；值允许为 null，用于表达“未设置/不适用”（例如未连接 Wi-Fi 时的 SSID）。
    private readonly Dictionary<string, string?> _dictionary = [];
}

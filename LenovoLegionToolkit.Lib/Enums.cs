using System;
using System.ComponentModel.DataAnnotations;
using LenovoLegionToolkit.Lib.Resources;

namespace LenovoLegionToolkit.Lib;

/// <summary>
/// USB 常开供电模式：关闭 / 仅睡眠时供电 / 始终供电。
/// </summary>
public enum AlwaysOnUSBState
{
    [Display(ResourceType = typeof(Resource), Name = "AlwaysOnUSBState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "AlwaysOnUSBState_OnWhenSleeping")]
    OnWhenSleeping,
    [Display(ResourceType = typeof(Resource), Name = "AlwaysOnUSBState_OnAlways")]
    OnAlways
}

/// <summary>
/// 开机自启动状态（启用 / 延迟启用 / 禁用）。
/// </summary>
public enum AutorunState
{
    [Display(ResourceType = typeof(Resource), Name = "AutorunState_Enabled")]
    Enabled,
    [Display(ResourceType = typeof(Resource), Name = "AutorunState_EnabledDelayed")]
    EnabledDelayed,
    [Display(ResourceType = typeof(Resource), Name = "AutorunState_Disabled")]
    Disabled
}

/// <summary>
/// 夜间电池充电限制开关（开启后夜间只充到较低电量以保护电池）。
/// </summary>
public enum BatteryNightChargeState
{
    [Display(ResourceType = typeof(Resource), Name = "BatteryNightChargeState_On")]
    On,
    [Display(ResourceType = typeof(Resource), Name = "BatteryNightChargeState_Off")]
    Off
}

/// <summary>
/// 电池充电模式：养护模式（限充）/ 普通 / 快充。
/// </summary>
public enum BatteryState
{
    [Display(ResourceType = typeof(Resource), Name = "BatteryState_Conservation")]
    Conservation,
    [Display(ResourceType = typeof(Resource), Name = "BatteryState_Normal")]
    Normal,
    [Display(ResourceType = typeof(Resource), Name = "BatteryState_RapidCharge")]
    RapidCharge
}

/// <summary>
/// 机型能力 ID。
/// 编码规则为固件约定的 32 位标识：高 16 位表示能力分组/功能域，低 16 位（常为 0xFF00）区分具体子项。
/// 这些数值直接来自联想 WMI 能力查询接口，不可随意改动。
/// </summary>
public enum CapabilityID
{
    IGPUMode = 0x00010000, // 集显模式切换能力
    FlipToStart = 0x00030000, // 开盖即开机
    NvidiaGPUDynamicDisplaySwitching = 0x00040000, // NVIDIA 动态显示切换（DDS）
    AMDSmartShiftMode = 0x00050001, // AMD SmartShift 模式
    AMDSkinTemperatureTracking = 0x00050002, // AMD 表面温度跟踪
    SupportedPowerModes = 0x00070000, // 支持的电源模式
    LegionZoneSupportVersion = 0x00090000, // Legion Zone 支持版本
    GodModeFnQSwitchable = 0x00100000, // Fn+Q 可切换到自定义模式
    OverDrive = 0x001A0000, // 屏幕超频（OverDrive）
    AIChip = 0x000E0000, // 搭载 AI 芯片
    IGPUModeChangeStatus = 0x000F0000, // 集显模式切换状态
    // 以下为自定义模式可调参数（分组 0x01 = CPU，0x02 = GPU），数值部分表示参数编号
    CPUShortTermPowerLimit = 0x0101FF00,
    CPULongTermPowerLimit = 0x0102FF00,
    CPUPeakPowerLimit = 0x0103FF00,
    CPUTemperatureLimit = 0x0104FF00,
    APUsPPTPowerLimit = 0x0105FF00,
    CPUCrossLoadingPowerLimit = 0x0106FF00,
    CPUPL1Tau = 0x0107FF00,
    GPUPowerBoost = 0x0201FF00,
    GPUConfigurableTGP = 0x0202FF00,
    GPUTemperatureLimit = 0x0203FF00,
    GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline = 0x0204FF00,
    GPUToCPUDynamicBoost = 0x020BFF00,
    GPUStatus = 0x02070000, // 可查询 GPU 状态
    GPUDidVid = 0x02090000, // 可查询 GPU 设备/厂商 ID
    InstantBootAc = 0x03010001, // 通电即开机（AC 适配器）
    InstantBootUsbPowerDelivery = 0x03010002, // 通电即开机（USB-PD）
    FanFullSpeed = 0x04020000, // 风扇全速控制
    CpuCurrentFanSpeed = 0x04030001, // 可读取 CPU 风扇转速
    GpuCurrentFanSpeed = 0x04030002, // 可读取 GPU 风扇转速
    CpuCurrentTemperature = 0x05040000, // 可读取 CPU 温度
    GpuCurrentTemperature = 0x05050000 // 可读取 GPU 温度
}

/// <summary>
/// 驱动键（Driver Key）位标志，对应设备驱动中不同功能快捷键的位掩码。
/// </summary>
[Flags]
public enum DriverKey
{
    FnF10 = 32, // 2^5
    FnF4 = 256, // 2^8
    FnF8 = 8192, // 2^13
    FnSpace = 4096, // 2^12
}

/// <summary>
/// 风扇曲线表的归属对象。
/// </summary>
public enum FanTableType
{
    Unknown, // 未知
    CPU, // CPU 风扇
    CPUSensor, // CPU 传感器触发的风扇表
    GPU, // GPU 风扇
    GPU2 // 第二颗 GPU 风扇
}

/// <summary>
/// 开盖即开机开关状态。
/// </summary>
public enum FlipToStartState
{
    [Display(ResourceType = typeof(Resource), Name = "FlipToStartState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "FlipToStartState_On")]
    On
}

/// <summary>
/// Fn 锁状态（Fn 功能键是否锁定）。
/// </summary>
public enum FnLockState
{
    [Display(ResourceType = typeof(Resource), Name = "FnLockState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "FnLockState_On")]
    On
}

/// <summary>
/// 独立显卡的电源/工作状态。
/// </summary>
public enum GPUState
{
    Unknown, // 未知
    NvidiaGpuNotFound, // 未找到 NVIDIA 显卡
    MonitorConnected, // 显示器直接连接到独显
    Active, // 独显正在工作
    Inactive, // 独显已上电但空闲
    PoweredOff // 独显已断电
}

/// <summary>
/// G-Sync 开关状态。
/// </summary>
public enum GSyncState
{
    Off,
    On
}

/// <summary>
/// HDR 开关状态。
/// </summary>
public enum HDRState
{
    [Display(ResourceType = typeof(Resource), Name = "HDRState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "HDRState_On")]
    On
}

/// <summary>
/// 混合模式（显卡工作模式）状态。
/// </summary>
public enum HybridModeState
{
    [Display(ResourceType = typeof(Resource), Name = "HybridModeState_On")]
    On,
    [Display(ResourceType = typeof(Resource), Name = "HybridModeState_OnIGPUOnly")]
    OnIGPUOnly,
    [Display(ResourceType = typeof(Resource), Name = "HybridModeState_OnAuto")]
    OnAuto,
    [Display(ResourceType = typeof(Resource), Name = "HybridModeState_Off")]
    Off
}

/// <summary>
/// 集显模式状态：默认（混合）/ 仅集显 / 自动。
/// </summary>
public enum IGPUModeState
{
    Default,
    IGPUOnly,
    Auto
}

/// <summary>
/// 通电即开机（Instant Boot）的触发源。
/// </summary>
public enum InstantBootState
{
    [Display(ResourceType = typeof(Resource), Name = "InstantBootState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "InstantBootState_AcAdapter")]
    AcAdapter,
    [Display(ResourceType = typeof(Resource), Name = "InstantBootState_UsbPowerDelivery")]
    UsbPowerDelivery,
    [Display(ResourceType = typeof(Resource), Name = "InstantBootState_AcAdapterAndUsbPowerDelivery")]
    AcAdapterAndUsbPowerDelivery
}

/// <summary>
/// 键盘物理布局类型（ANSI / ISO / JIS），影响按键编码映射。
/// </summary>
public enum KeyboardLayout
{
    Ansi,
    Iso,
    Jis
}

/// <summary>
/// 需要解析路径的系统已知文件夹。
/// </summary>
public enum KnownFolder
{
    Contacts,
    Downloads,
    Favorites,
    Links,
    SavedGames,
    SavedSearches
}

/// <summary>
/// 灯光变化事件来源区域；数值与底层通知参数保持一致。
/// </summary>
public enum LightingChangeState
{
    Panel = 0, // 面板（A 面 Logo）灯光
    Ports = 1, // 接口（机身灯条）灯光
}

/// <summary>
/// 麦克风开关状态。
/// </summary>
public enum MicrophoneState
{
    [Display(ResourceType = typeof(Resource), Name = "MicrophoneState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "MicrophoneState_On")]
    On
}

/// <summary>
/// 修饰键位标志，可组合（如 Ctrl+Shift）。
/// </summary>
[Flags]
public enum ModifierKey
{
    None = 0,
    [Display(ResourceType = typeof(Resource), Name = "ModifierKey_Shift")]
    Shift = 1,
    [Display(ResourceType = typeof(Resource), Name = "ModifierKey_Ctrl")]
    Ctrl = 2,
    [Display(ResourceType = typeof(Resource), Name = "ModifierKey_Alt")]
    Alt = 4
}

/// <summary>
/// 需要监听的 Windows 原生窗口消息事件类型（由消息监听器翻译后发出）。
/// </summary>
public enum NativeWindowsMessage
{
    LidOpened, // 笔记本上盖打开
    LidClosed, // 笔记本上盖合上
    MonitorOn, // 显示器点亮
    MonitorOff, // 显示器关闭
    DeviceConnected, // 设备接入
    DeviceDisconnected, // 设备移除
    MonitorConnected, // 显示器接入
    MonitorDisconnected, // 显示器断开
    ExternalMonitorConnected, // 外接显示器接入
    ExternalMonitorDisconnected, // 外接显示器断开
    OnDisplayDeviceArrival, // 显示设备到达（WM_DISPLAYCHANGE 类）
    BatterySaverEnabled // 系统节电模式开启
}

/// <summary>
/// 系统通知停留时长。
/// </summary>
public enum NotificationDuration
{
    [Display(ResourceType = typeof(Resource), Name = "NotificationDuration_Short")]
    Short,
    [Display(ResourceType = typeof(Resource), Name = "NotificationDuration_Normal")]
    Normal,
    [Display(ResourceType = typeof(Resource), Name = "NotificationDuration_Long")]
    Long
}

/// <summary>
/// 系统通知类型，决定通知的文字与图标。
/// </summary>
public enum NotificationType
{
    ACAdapterConnected, // 电源适配器已连接
    ACAdapterConnectedLowWattage, // 连接了功率不足的适配器
    ACAdapterDisconnected, // 电源适配器已断开
    AutomationNotification, // 自动化规则触发
    CameraOn,
    CameraOff,
    CapsLockOn,
    CapsLockOff,
    FnLockOn,
    FnLockOff,
    MicrophoneOff,
    MicrophoneOn,
    NumLockOn,
    NumLockOff,
    PanelLogoLightingOn,
    PanelLogoLightingOff,
    PortLightingOn,
    PortLightingOff,
    PowerModeQuiet, // 已切换到安静模式
    PowerModeBalance, // 已切换到均衡模式
    PowerModePerformance, // 已切换到性能模式
    PowerModeGodMode, // 已切换到自定义模式
    RefreshRate, // 刷新率已改变
    RGBKeyboardBacklightChanged,
    RGBKeyboardBacklightOff,
    SmartKeyDoublePress, // 智能键双击
    SmartKeySinglePress, // 智能键单击
    SpectrumBacklightChanged,
    SpectrumBacklightOff,
    SpectrumBacklightPresetChanged,
    TouchpadOn,
    TouchpadOff,
    UpdateAvailable, // 有新版本可用
    WhiteKeyboardBacklightChanged,
    WhiteKeyboardBacklightOff
}

/// <summary>
/// 系统通知在屏幕上的出现位置。
/// </summary>
public enum NotificationPosition
{
    [Display(ResourceType = typeof(Resource), Name = "NotificationPosition_BottomRight")]
    BottomRight,
    [Display(ResourceType = typeof(Resource), Name = "NotificationPosition_BottomCenter")]
    BottomCenter,
    [Display(ResourceType = typeof(Resource), Name = "NotificationPosition_BottomLeft")]
    BottomLeft,
    [Display(ResourceType = typeof(Resource), Name = "NotificationPosition_CenterLeft")]
    CenterLeft,
    [Display(ResourceType = typeof(Resource), Name = "NotificationPosition_TopLeft")]
    TopLeft,
    [Display(ResourceType = typeof(Resource), Name = "NotificationPosition_TopCenter")]
    TopCenter,
    [Display(ResourceType = typeof(Resource), Name = "NotificationPosition_TopRight")]
    TopRight,
    [Display(ResourceType = typeof(Resource), Name = "NotificationPosition_CenterRight")]
    CenterRight,
    [Display(ResourceType = typeof(Resource), Name = "NotificationPosition_Center")]
    Center
}

/// <summary>
/// 单档白色键盘背光状态（开/关）。
/// </summary>
public enum OneLevelWhiteKeyboardBacklightState
{
    [Display(ResourceType = typeof(Resource), Name = "OneLevelWhiteKeyboardBacklightState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "OneLevelWhiteKeyboardBacklightState_On")]
    On
}

/// <summary>
/// Windows 操作系统版本枚举。
/// </summary>
public enum OS
{
    [Display(Name = "Windows 11")]
    Windows11,
    [Display(Name = "Windows 10")]
    Windows10,
    [Display(Name = "Windows 8")]
    Windows8,
    [Display(Name = "Windows 7")]
    Windows7
}

/// <summary>
/// 屏幕 OverDrive（响应时间加速）开关状态。
/// </summary>
public enum OverDriveState
{
    [Display(ResourceType = typeof(Resource), Name = "OverdriveState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "OverdriveState_On")]
    On
}

/// <summary>
/// 面板（顶盖 Logo）背光状态。
/// </summary>
public enum PanelLogoBacklightState
{
    [Display(ResourceType = typeof(Resource), Name = "PanelLogoBacklightState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "PanelLogoBacklightState_On")]
    On
}

/// <summary>
/// 接口（机身灯条）背光状态。
/// </summary>
public enum PortsBacklightState
{
    [Display(ResourceType = typeof(Resource), Name = "PortsBacklightState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "PortsBacklightState_On")]
    On
}

/// <summary>
/// 电源适配器连接状态；LowWattage 表示已连接但功率不足以支撑性能模式。
/// </summary>
public enum PowerAdapterStatus
{
    Connected,
    ConnectedLowWattage,
    Disconnected
}

/// <summary>
/// 电源模式与 Windows 电源管理项的联动方式。
/// </summary>
public enum PowerModeMappingMode
{
    [Display(ResourceType = typeof(Resource), Name = "PowerModeMappingMode_Disabled")]
    Disabled,
    [Display(ResourceType = typeof(Resource), Name = "PowerModeMappingMode_WindowsPowerMode")]
    WindowsPowerMode,
    [Display(ResourceType = typeof(Resource), Name = "PowerModeMappingMode_WindowsPowerPlan")]
    WindowsPowerPlan,
}

/// <summary>
/// 电源模式状态。前三个对应硬件档位，GodMode 使用 254 这一特殊值表示"自定义"。
/// </summary>
public enum PowerModeState
{
    [Display(ResourceType = typeof(Resource), Name = "PowerModeState_Quiet")]
    Quiet,
    [Display(ResourceType = typeof(Resource), Name = "PowerModeState_Balance")]
    Balance,
    [Display(ResourceType = typeof(Resource), Name = "PowerModeState_Performance")]
    Performance,
    [Display(ResourceType = typeof(Resource), Name = "PowerModeState_GodMode")]
    GodMode = 254 // 固件约定的自定义模式标识，避免与其他档位冲突
}

/// <summary>
/// 系统电源状态事件（来自 PBT_APMxxx 电源广播）。
/// </summary>
public enum PowerStateEvent
{
    Unknown = -1,
    StatusChange, // 电源状态变化（如插拔适配器）
    Suspend, // 进入睡眠
    Resume, // 从睡眠唤醒
}

/// <summary>
/// 进程事件类型。
/// </summary>
public enum ProcessEventInfoType
{
    Started, // 进程启动
    Stopped // 进程退出
}

/// <summary>
/// 安装包安装后所需的重启方式；数值对应联想安装包的 Reboot 字段。
/// </summary>
public enum RebootType
{
    NotRequired = 0, // 无需重启
    Forced = 1, // 安装器强制重启
    Requested = 3, // 建议重启
    ForcedPowerOff = 4, // 强制关机
    Delayed = 5 // 延迟重启
}

/// <summary>
/// RGB 键盘灯效变化事件的无成员标记类型（仅用于事件/泛型标识）。
/// </summary>
public enum RGBKeyboardBacklightChanged;

/// <summary>
/// RGB 键盘亮度档位。
/// </summary>
public enum RGBKeyboardBacklightBrightness
{
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightBrightness_Low")]
    Low,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightBrightness_High")]
    High
}

/// <summary>
/// 四分区 RGB 键盘的灯效类型。
/// </summary>
public enum RGBKeyboardBacklightEffect
{
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightEffect_Static")]
    Static,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightEffect_Breath")]
    Breath,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightEffect_Smooth")]
    Smooth,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightEffect_WaveRTL")]
    WaveRTL,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightEffect_WaveLTR")]
    WaveLTR
}

/// <summary>
/// RGB 键盘预设槽位；Off(-1) 表示关闭背光，One..Four 对应四个可编辑预设。
/// </summary>
public enum RGBKeyboardBacklightPreset
{
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightPreset_Off")]
    Off = -1,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightPreset_One")]
    One = 0,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightPreset_Two")]
    Two = 1,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightPreset_Three")]
    Three = 2,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightPreset_Four")]
    Four = 3
}

/// <summary>
/// RGB 键盘灯效速度档位。
/// </summary>
public enum RGBKeyboardBacklightSpeed
{
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightSpeed_Slowest")]
    Slowest,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightSpeed_Slow")]
    Slow,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightSpeed_Fast")]
    Fast,
    [Display(ResourceType = typeof(Resource), Name = "RGBKeyboardBacklightSpeed_Fastest")]
    Fastest
}

/// <summary>
/// 扬声器静音状态。
/// </summary>
public enum SpeakerState
{
    [Display(ResourceType = typeof(Resource), Name = "SpeakerState_Mute")]
    Mute,
    [Display(ResourceType = typeof(Resource), Name = "SpeakerState_Unmute")]
    Unmute
}

/// <summary>
/// 联想官方软件的安装/启用状态；NotFound 表示未安装。
/// </summary>
public enum SoftwareStatus
{
    Enabled,
    Disabled,
    NotFound
}

/// <summary>
/// 特殊功能键编码（由驱动通过 WMI 事件上报）。
/// 数值为固件/驱动定义的事件码，部分键在不同机型上存在两个编码（如 FnR/FnR2）。
/// </summary>
public enum SpecialKey
{
    FnF9 = 1,
    FnLockOn = 2,
    FnLockOff = 3,
    FnPrtSc = 4,
    FnPrtSc2 = 45, // 新版驱动使用的 Fn+PrtSc 编码
    CameraOn = 12,
    CameraOff = 13,
    FnR = 16,
    FnR2 = 0x0041002A, // 部分机型（尤其 AMD 平台）使用的复合编码
    SpectrumBacklightOff = 24,
    SpectrumBacklight1 = 25,
    SpectrumBacklight2 = 26,
    SpectrumBacklight3 = 38,
    SpectrumPreset1 = 32,
    SpectrumPreset2 = 33,
    SpectrumPreset3 = 34,
    SpectrumPreset4 = 35,
    SpectrumPreset5 = 36,
    SpectrumPreset6 = 37,
    FnN = 42,
    FnF4 = 62,
    FnF8 = 63,
    WhiteBacklightOff = 64,
    WhiteBacklight1 = 65,
    WhiteBacklight2 = 66
}

/// <summary>
/// 全彩（Spectrum）键盘亮度档位。
/// </summary>
public enum SpectrumKeyboardBacklightBrightness
{
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightBrightness_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightBrightness_Low")]
    Low,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightBrightness_Medium")]
    Medium,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightBrightness_High")]
    High
}

/// <summary>
/// 旋转类灯效的旋转方向。
/// </summary>
public enum SpectrumKeyboardBacklightClockwiseDirection
{
    None,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightDirection_Clockwise")]
    Clockwise,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightDirection_CounterClockwise")]
    CounterClockwise
}

/// <summary>
/// 流动类灯效的方向。
/// </summary>
public enum SpectrumKeyboardBacklightDirection
{
    None,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightDirection_BottomToTop")]
    BottomToTop,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightDirection_TopToBottom")]
    TopToBottom,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightDirection_LeftToRight")]
    LeftToRight,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightDirection_RightToLeft")]
    RightToLeft
}

/// <summary>
/// 全彩（Spectrum）键盘的灯效类型。
/// </summary>
public enum SpectrumKeyboardBacklightEffectType
{
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_Always")]
    Always,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_RainbowScrew")]
    RainbowScrew,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_RainbowWave")]
    RainbowWave,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_ColorChange")]
    ColorChange,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_ColorWave")]
    ColorWave,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_ColorPulse")]
    ColorPulse,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_Smooth")]
    Smooth,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_Rain")]
    Rain,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_Ripple")]
    Ripple,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_Type")]
    Type,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_AudioBounce")]
    AudioBounce,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_AudioRipple")]
    AudioRipple,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightEffectType_AuroraSync")]
    AuroraSync
}

/// <summary>
/// 全彩（Spectrum）键盘灯效速度档位。
/// </summary>
public enum SpectrumKeyboardBacklightSpeed
{
    None,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightSpeed_Speed1")]
    Speed1,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightSpeed_Speed2")]
    Speed2,
    [Display(ResourceType = typeof(Resource), Name = "SpectrumKeyboardBacklightSpeed_Speed3")]
    Speed3
}

/// <summary>
/// 全彩键盘的物理布局，决定可用灯区（仅键盘 / 键盘+前沿 / 全区域）。
/// </summary>
public enum SpectrumLayout
{
    KeyboardOnly,
    KeyboardAndFront,
    Full,
    FullAlternative
}

/// <summary>
/// 应用界面主题。
/// </summary>
public enum Theme
{
    [Display(ResourceType = typeof(Resource), Name = "Theme_System")]
    System,
    [Display(ResourceType = typeof(Resource), Name = "Theme_Light")]
    Light,
    [Display(ResourceType = typeof(Resource), Name = "Theme_Dark")]
    Dark
}

/// <summary>
/// 主题强调色来源：跟随系统或自定义。
/// </summary>
public enum AccentColorSource
{
    [Display(ResourceType = typeof(Resource), Name = "AccentColorSource_System")]
    System,
    [Display(ResourceType = typeof(Resource), Name = "AccentColorSource_Custom")]
    Custom
}

/// <summary>
/// 温度单位：摄氏度 / 华氏度。
/// </summary>
public enum TemperatureUnit
{
    C,
    F
}

/// <summary>
/// 散热模式状态（由 EC/WMI 上报）。GodMode 使用 255 作为特殊值。
/// </summary>
public enum ThermalModeState
{
    Unknown,
    Quiet,
    Balance,
    Performance,
    GodMode = 255 // 固件约定的自定义模式标识
}

/// <summary>
/// 触摸板锁定状态。
/// </summary>
public enum TouchpadLockState
{
    [Display(ResourceType = typeof(Resource), Name = "TouchpadLockState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "TouchpadLockState_On")]
    On
}

/// <summary>
/// 自动检查更新的频率。
/// </summary>
public enum UpdateCheckFrequency
{
    [Display(ResourceType = typeof(Resource), Name = "UpdateCheckFrequency_PerHour")]
    PerHour,
    [Display(ResourceType = typeof(Resource), Name = "UpdateCheckFrequency_PerThreeHours")]
    PerThreeHours,
    [Display(ResourceType = typeof(Resource), Name = "UpdateCheckFrequency_PerTwelveHours")]
    PerTwelveHours,
    [Display(ResourceType = typeof(Resource), Name = "UpdateCheckFrequency_PerDay")]
    PerDay,
    [Display(ResourceType = typeof(Resource), Name = "UpdateCheckFrequency_PerWeek")]
    PerWeek,
    [Display(ResourceType = typeof(Resource), Name = "UpdateCheckFrequency_PerMonth")]
    PerMonth
}

/// <summary>
/// 更新检查结果；RateLimitReached 表示触发了 GitHub API 速率限制。
/// </summary>
public enum UpdateCheckStatus
{
    Success,
    RateLimitReached,
    Error
}

/// <summary>
/// 白色背光键盘亮度档位（关/低/高）。
/// </summary>
public enum WhiteKeyboardBacklightState
{
    [Display(ResourceType = typeof(Resource), Name = "WhiteKeyboardBacklightState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "WhiteKeyboardBacklightState_Low")]
    Low,
    [Display(ResourceType = typeof(Resource), Name = "WhiteKeyboardBacklightState_High")]
    High
}

/// <summary>
/// Windows 的电源模式（Windows 11 的"电源模式"滑块）。
/// </summary>
public enum WindowsPowerMode
{
    [Display(Name = "Best power efficiency")]
    BestPowerEfficiency,
    [Display(Name = "Balanced")]
    Balanced,
    [Display(Name = "Best performance")]
    BestPerformance
}

/// <summary>
/// Win 键锁定状态。
/// </summary>
public enum WinKeyState
{
    [Display(ResourceType = typeof(Resource), Name = "WinKeyState_Off")]
    Off,
    [Display(ResourceType = typeof(Resource), Name = "WinKeyState_On")]
    On
}

/// <summary>
/// Win 键状态变化事件的无成员标记类型（仅用于事件/泛型标识）。
/// </summary>
public enum WinKeyChanged;

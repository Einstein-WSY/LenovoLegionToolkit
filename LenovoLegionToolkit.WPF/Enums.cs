namespace LenovoLegionToolkit.WPF;

/// <summary>
/// 仪表盘分组类型，决定分组的标题来源与内置默认项。
/// </summary>
public enum DashboardGroupType
{
    Power,
    Graphics,
    Display,
    Other,
    Custom
}

/// <summary>
/// 仪表盘上可展示的单个功能项。
/// </summary>
public enum DashboardItem
{
    PowerMode,
    BatteryMode,
    BatteryNightChargeMode,
    AlwaysOnUsb,
    InstantBoot,
    HybridMode,
    DiscreteGpu,
    OverclockDiscreteGpu,
    PanelLogoBacklight,
    PortsBacklight,
    Resolution,
    RefreshRate,
    DpiScale,
    Hdr,
    OverDrive,
    TurnOffMonitors,
    Microphone,
    FlipToStart,
    TouchpadLock,
    FnLock,
    WinKeyLock,
    WhiteKeyboardBacklight
}

/// <summary>
/// 消息提示条（Snackbar）的样式类型。
/// </summary>
public enum SnackbarType
{
    Success,
    Warning,
    Error,
    Info
}

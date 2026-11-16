using System;
using System.Windows;
using LenovoLegionToolkit.WPF.Resources;

namespace LenovoLegionToolkit.WPF;

/// <summary>
/// 仪表盘分组定义，包含分组类型、自定义名称以及该组下的功能项列表。
/// </summary>
/// <param name="type">分组类型。</param>
/// <param name="customName">自定义分组的显示名称，仅当类型为 <see cref="DashboardGroupType.Custom"/> 时生效。</param>
/// <param name="items">分组内包含的仪表盘项。</param>
public readonly struct DashboardGroup(DashboardGroupType type, string? customName, params DashboardItem[] items)
{
    /// <summary>
    /// 内置的默认分组配置（电源、显卡、显示、其他）。
    /// </summary>
    public static readonly DashboardGroup[] DefaultGroups =
    [
        new(DashboardGroupType.Power, null,
            DashboardItem.PowerMode,
            DashboardItem.BatteryMode,
            DashboardItem.BatteryNightChargeMode,
            DashboardItem.AlwaysOnUsb,
            DashboardItem.InstantBoot,
            DashboardItem.FlipToStart),
        new(DashboardGroupType.Graphics, null,
            DashboardItem.HybridMode,
            DashboardItem.DiscreteGpu,
            DashboardItem.OverclockDiscreteGpu),
        new(DashboardGroupType.Display, null,
            DashboardItem.Resolution,
            DashboardItem.RefreshRate,
            DashboardItem.DpiScale,
            DashboardItem.Hdr,
            DashboardItem.OverDrive,
            DashboardItem.TurnOffMonitors),
        new(DashboardGroupType.Other, null,
            DashboardItem.Microphone,
            DashboardItem.WhiteKeyboardBacklight,
            DashboardItem.PanelLogoBacklight,
            DashboardItem.PortsBacklight,
            DashboardItem.TouchpadLock,
            DashboardItem.FnLock,
            DashboardItem.WinKeyLock)
    ];

    /// <summary>分组类型。</summary>
    public DashboardGroupType Type { get; } = type;

    /// <summary>自定义名称；非自定义分组时始终为 <c>null</c>。</summary>
    public string? CustomName { get; } = type == DashboardGroupType.Custom ? customName : null;

    /// <summary>分组内的功能项。</summary>
    public DashboardItem[] Items { get; } = items;

    /// <summary>
    /// 获取分组显示名称：内置分组从本地化资源读取，自定义分组使用 <see cref="CustomName"/>。
    /// </summary>
    /// <returns>本地化后的分组名称。</returns>
    public string GetName() => Type switch
    {
        DashboardGroupType.Power => Resource.DashboardPage_Power_Title,
        DashboardGroupType.Graphics => Resource.DashboardPage_Graphics_Title,
        DashboardGroupType.Display => Resource.DashboardPage_Display_Title,
        DashboardGroupType.Other => Resource.DashboardPage_Other_Title,
        DashboardGroupType.Custom => CustomName ?? string.Empty,
        _ => throw new InvalidOperationException($"Invalid type {Type}"),
    };

    /// <summary>输出分组类型、名称与包含项，便于调试。</summary>
    /// <returns>分组的字符串表示。</returns>
    public override string ToString() =>
        $"{nameof(Type)}: {Type}," +
        $" {nameof(CustomName)}: {CustomName}," +
        $" {nameof(Items)}: {string.Join(",", Items)}";
}

/// <summary>
/// 屏幕信息，包含工作区矩形、DPI 与是否为主屏。
/// </summary>
/// <param name="workArea">屏幕工作区（已排除任务栏）。</param>
/// <param name="dpiX">水平 DPI。</param>
/// <param name="dpiY">垂直 DPI。</param>
/// <param name="isPrimary">是否为主显示器。</param>
public readonly struct ScreenInfo(Rect workArea, uint dpiX, uint dpiY, bool isPrimary)
{
    /// <summary>屏幕工作区。</summary>
    public Rect WorkArea { get; } = workArea;
    /// <summary>水平 DPI。</summary>
    public uint DpiX { get; } = dpiX;
    /// <summary>垂直 DPI。</summary>
    public uint DpiY { get; } = dpiY;
    /// <summary>是否为主显示器。</summary>
    public bool IsPrimary { get; } = isPrimary;
}

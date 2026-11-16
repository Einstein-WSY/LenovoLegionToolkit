using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace LenovoLegionToolkit.WPF.Utils;

/// <summary>
/// 屏幕信息辅助类，枚举所有显示器并记录工作区、DPI 与主屏标记。
/// </summary>
public static class ScreenHelper
{
    /// <summary>当前已枚举的屏幕信息列表。</summary>
    public static List<ScreenInfo> Screens { get; } = [];

    /// <summary>主显示器信息；不存在时返回 <c>null</c>。</summary>
    public static ScreenInfo? PrimaryScreen => Screens.FirstOrDefault(s => s.IsPrimary);

    /// <summary>重新枚举所有显示器并刷新 <see cref="Screens"/>。</summary>
    public static void UpdateScreenInfos()
    {
        Screens.Clear();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnumProc, IntPtr.Zero);
    }

    /// <summary>枚举显示器的 Win32 API 声明。</summary>
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumDisplayMonitorsDelegate lpfnEnum, IntPtr dwData);

    private delegate bool EnumDisplayMonitorsDelegate(HMONITOR hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

    // 枚举回调：读取每个显示器的工作区与 DPI，并加入 Screens
    private static bool MonitorEnumProc(HMONITOR hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData)
    {
        MONITORINFO monitorInfo = new() { cbSize = (uint)Marshal.SizeOf<MONITORINFO>() };

        if (!PInvoke.GetMonitorInfo(hMonitor, ref monitorInfo))
            return true;

        // 获取显示器有效 DPI（部分系统上该 API 受版本限制）
#pragma warning disable CA1416
        if (!PInvoke.GetDpiForMonitor(hMonitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out var dpiY).Succeeded)
#pragma warning restore CA1416
            return true;

        var workArea = monitorInfo.rcWork;
        // 将物理像素换算为 96 DPI 基准的逻辑像素
        var multiplierX = 96d / dpiX;
        var multiplierY = 96d / dpiY;

        Screens.Add(new ScreenInfo(
            new Rect(workArea.X, workArea.Y, workArea.Width * multiplierX, workArea.Height * multiplierY),
            dpiX, dpiY,
            (monitorInfo.dwFlags & PInvoke.MONITORINFOF_PRIMARY) != 0
        ));

        return true;
    }
}

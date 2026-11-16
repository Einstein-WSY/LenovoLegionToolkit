using System;
using System.Diagnostics;
using System.Windows.Forms;
using LenovoLegionToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace LenovoLegionToolkit.WPF.Utils;

/// <summary>
/// 全屏状态检测辅助类，用于判断当前是否有应用处于全屏（如游戏），以便暂停某些自动化行为。
/// </summary>
public static class FullscreenHelper
{
    /// <summary>
    /// 判断当前是否有应用程序处于全屏状态。
    /// </summary>
    /// <returns>存在全屏应用返回 <c>true</c>。</returns>
    public static unsafe bool IsAnyApplicationFullscreen()
    {
        try
        {
            var desktopWindowHandle = PInvoke.GetDesktopWindow();
            var shellWindowHandle = PInvoke.GetShellWindow();

            // 排除桌面与任务栏（Shell）窗口，它们也可能铺满屏幕
            var foregroundWindowHandle = PInvoke.GetForegroundWindow();
            if (foregroundWindowHandle == HWND.Null)
                return false;
            if (foregroundWindowHandle == desktopWindowHandle)
                return false;
            if (foregroundWindowHandle == shellWindowHandle)
                return false;

            if (!PInvoke.GetWindowRect(foregroundWindowHandle, out var appBounds))
                return false;

            // 窗口矩形完整覆盖所在屏幕时视为全屏
            var screenBounds = Screen.FromHandle(foregroundWindowHandle).Bounds;
            var coversFullScreen = appBounds.bottom - appBounds.top == screenBounds.Height && appBounds.right - appBounds.left == screenBounds.Width;
            if (!coversFullScreen)
                return false;

            // 最后排除 explorer（桌面本身）
            var processId = 0u;
            _ = PInvoke.GetWindowThreadProcessId(foregroundWindowHandle, &processId);
            var process = Process.GetProcessById((int)processId);
            return process.ProcessName != "explorer";
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't check if application is full screen.", ex);

            return false;
        }
    }
}

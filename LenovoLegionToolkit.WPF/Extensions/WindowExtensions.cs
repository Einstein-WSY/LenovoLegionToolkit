using System.Windows;

namespace LenovoLegionToolkit.WPF.Extensions;

/// <summary>
/// 提供窗口显示与焦点控制相关的扩展方法。
/// </summary>
public static class WindowExtensions
{
    /// <summary>
    /// 将窗口恢复到正常状态并置于前台。
    /// </summary>
    /// <param name="window">目标窗口。</param>
    public static void BringToForeground(this Window window)
    {
        window.ShowInTaskbar = true;

        // 最小化或隐藏的窗口需要先恢复显示，否则 Activate 无法生效
        if (window.WindowState == WindowState.Minimized || window.Visibility == Visibility.Hidden)
        {
            window.Show();
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
        // 临时切换 Topmost 强制窗口跳到 Z 序顶层，随后立即恢复，避免长期置顶
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }
}

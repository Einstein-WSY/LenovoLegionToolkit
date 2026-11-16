using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.WPF.Windows;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Utils;

/// <summary>
/// 消息提示条（Snackbar）辅助类，统一主窗口中提示的文本、外观与显示时长。
/// </summary>
public static class SnackbarHelper
{
    /// <summary>
    /// 异步显示提示条：先隐藏已有提示，避免多个提示叠加。
    /// </summary>
    /// <param name="title">标题。</param>
    /// <param name="message">正文，可为空。</param>
    /// <param name="type">提示类型。</param>
    public static async Task ShowAsync(string title, string? message = null, SnackbarType type = SnackbarType.Success)
    {
        var mainWindow = Application.Current.MainWindow as MainWindow;
        var snackBar = mainWindow?.Snackbar;

        // 主窗口或提示条不存在时静默返回
        if (snackBar is null)
            return;

        SetupSnackbarAppearance(snackBar, title, message, type);
        SetTitleAndMessage(snackBar, title, message);
        await snackBar.HideAsync();
        await snackBar.ShowAsync();
    }

    /// <summary>
    /// 同步显示提示条（不等待隐藏/显示动画完成）。
    /// </summary>
    /// <param name="title">标题。</param>
    /// <param name="message">正文，可为空。</param>
    /// <param name="type">提示类型。</param>
    public static void Show(string title, string? message = null, SnackbarType type = SnackbarType.Success)
    {
        var mainWindow = Application.Current.MainWindow as MainWindow;
        var snackBar = mainWindow?.Snackbar;

        if (snackBar is null)
            return;

        SetupSnackbarAppearance(snackBar, title, message, type);
        SetTitleAndMessage(snackBar, title, message);
        snackBar.Show();
    }

    // 根据类型设置外观、图标、超时时间与关闭按钮
    private static void SetupSnackbarAppearance(Snackbar snackBar, string title, string? message, SnackbarType type)
    {
        snackBar.Appearance = type switch
        {
            SnackbarType.Warning => ControlAppearance.Caution,
            SnackbarType.Error => ControlAppearance.Danger,
            _ => ControlAppearance.Secondary
        };
        snackBar.Icon = type switch
        {
            SnackbarType.Warning => SymbolRegular.Warning24,
            SnackbarType.Error => SymbolRegular.ErrorCircle24,
            SnackbarType.Info => SymbolRegular.Info24,
            _ => SymbolRegular.Checkmark24
        };
        // 成功提示固定 2 秒，其余按文本长度动态计算并限制在 5-10 秒
        snackBar.Timeout = type switch
        {
            SnackbarType.Success => 2000,
            _ => Math.Clamp(GetTextLengthInMilliseconds(title, message), 5000, 10000)
        };
        snackBar.CloseButtonEnabled = type switch
        {
            SnackbarType.Success => false,
            _ => true
        };
    }

    // 通过模板内命名元素设置标题与消息文本
    private static void SetTitleAndMessage(FrameworkElement snackBar, string title, string? message)
    {
        if (snackBar.FindName("_snackbarTitle") is TextBlock snackbarTitle)
            snackbarTitle.Text = title;

        if (snackBar.FindName("_snackbarMessage") is TextBlock snackbarMessage)
        {
            snackbarMessage.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
            snackbarMessage.Text = message;
        }
    }

    // 依据标题与消息长度估算显示时长（单位毫秒）
    private static int GetTextLengthInMilliseconds(string title, string? message)
    {
        var length = 2 + (title.Length + (message?.Length ?? 0)) % 10;
        return length * 1000;
    }
}

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;
using MessageBox = Wpf.Ui.Controls.MessageBox;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace LenovoLegionToolkit.WPF.Utils;

/// <summary>
/// 消息框辅助类，基于 WPF-UI 的 <see cref="MessageBox"/> 提供异步确认与文本输入对话框。
/// </summary>
public static class MessageBoxHelper
{
    /// <summary>
    /// 以指定元素所属窗口为所有者显示确认框。
    /// </summary>
    /// <param name="dependencyObject">用于定位父窗口的元素。</param>
    /// <param name="title">标题。</param>
    /// <param name="message">内容。</param>
    /// <param name="leftButton">左侧（确认）按钮文本。</param>
    /// <param name="rightButton">右侧（取消）按钮文本。</param>
    /// <returns>点击确认返回 <c>true</c>。</returns>
    public static Task<bool> ShowAsync(DependencyObject dependencyObject,
        string title,
        string message,
        string? leftButton = null,
        string? rightButton = null
    )
    {
        // 优先使用元素所属窗口，回退到主窗口
        var window = Window.GetWindow(dependencyObject)
                     ?? Application.Current.MainWindow
                     ?? throw new InvalidOperationException("Cannot show message without window");
        return ShowAsync(window, title, message, leftButton, rightButton);
    }

    /// <summary>
    /// 以指定窗口为所有者显示确认框。
    /// </summary>
    /// <param name="window">所有者窗口。</param>
    /// <param name="title">标题。</param>
    /// <param name="message">内容。</param>
    /// <param name="primaryButton">主按钮文本，默认“是”。</param>
    /// <param name="secondaryButton">次按钮文本，默认“否”。</param>
    /// <returns>点击主按钮返回 <c>true</c>；关闭窗口按取消处理。</returns>
    public static Task<bool> ShowAsync(Window window,
        string title,
        string message,
        string? primaryButton = null,
        string? secondaryButton = null)
    {
        var tcs = new TaskCompletionSource<bool>();

        var messageBox = new MessageBox
        {
            Owner = window,
            Title = title,
            Content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
            },
            ButtonLeftName = primaryButton ?? Resource.Yes,
            ButtonRightName = secondaryButton ?? Resource.No,
            ShowInTaskbar = false,
            Topmost = false,
            ResizeMode = ResizeMode.NoResize,
        };
        // 通过 TaskCompletionSource 将按钮点击转换为可等待的结果
        messageBox.ButtonLeftClick += (_, _) =>
        {
            tcs.SetResult(true);
            messageBox.Close();
        };
        messageBox.ButtonRightClick += (_, _) =>
        {
            tcs.SetResult(false);
            messageBox.Close();
        };
        // 直接关闭窗口视为取消，避免任务永远不完成
        messageBox.Closing += (_, _) =>
        {
            tcs.TrySetResult(false);
        };
        messageBox.Show();

        return tcs.Task;
    }

    /// <summary>
    /// 以指定元素所属窗口为所有者显示文本输入框。
    /// </summary>
    /// <param name="dependencyObject">用于定位父窗口的元素。</param>
    /// <param name="title">标题。</param>
    /// <param name="placeholder">输入框占位文本。</param>
    /// <param name="text">初始文本。</param>
    /// <param name="primaryButton">主按钮文本。</param>
    /// <param name="secondaryButton">次按钮文本。</param>
    /// <param name="allowEmpty">是否允许空输入。</param>
    /// <returns>确认时返回输入文本，取消返回 <c>null</c>。</returns>
    public static Task<string?> ShowInputAsync(
        DependencyObject dependencyObject,
        string title,
        string? placeholder = null,
        string? text = null,
        string? primaryButton = null,
        string? secondaryButton = null,
        bool allowEmpty = false
    )
    {
        var window = Window.GetWindow(dependencyObject)
                     ?? Application.Current.MainWindow
                     ?? throw new InvalidOperationException("Cannot show message without window");
        return ShowInputAsync(window, title, placeholder, text, primaryButton, secondaryButton, allowEmpty);
    }

    /// <summary>
    /// 以指定窗口为所有者显示文本输入框。
    /// </summary>
    /// <param name="window">所有者窗口。</param>
    /// <param name="title">标题。</param>
    /// <param name="placeholder">输入框占位文本。</param>
    /// <param name="text">初始文本。</param>
    /// <param name="primaryButton">主按钮文本。</param>
    /// <param name="secondaryButton">次按钮文本。</param>
    /// <param name="allowEmpty">是否允许空输入。</param>
    /// <returns>确认时返回去除首尾空白的文本，取消返回 <c>null</c>。</returns>
    public static Task<string?> ShowInputAsync(
        Window window,
        string title,
        string? placeholder = null,
        string? text = null,
        string? primaryButton = null,
        string? secondaryButton = null,
        bool allowEmpty = false
    )
    {
        var tcs = new TaskCompletionSource<string?>();

        var textBox = new TextBox
        {
            MaxLines = 1,
            MaxLength = 50,
            PlaceholderText = placeholder,
            TextWrapping = TextWrapping.Wrap
        };
        var messageBox = new MessageBox
        {
            Owner = window,
            Title = title,
            Content = textBox,
            ButtonLeftAppearance = ControlAppearance.Transparent,
            ButtonLeftName = primaryButton ?? Resource.OK,
            ButtonRightName = secondaryButton ?? Resource.Cancel,
            ShowInTaskbar = false,
            Topmost = false,
            MinHeight = 160,
            MaxHeight = 160,
            ResizeMode = ResizeMode.NoResize,
        };

        // 输入为空且不允许空值时，降低主按钮视觉权重
        textBox.TextChanged += (_, _) =>
        {
            var isEmpty = !allowEmpty && string.IsNullOrWhiteSpace(textBox.Text);
            messageBox.ButtonLeftAppearance = isEmpty ? ControlAppearance.Transparent : ControlAppearance.Primary;
        };
        messageBox.ButtonLeftClick += (_, _) =>
        {
            // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
            var content = textBox.Text?.Trim();
            var newText = string.IsNullOrWhiteSpace(content) ? null : content;
            if (!allowEmpty && newText is null)
                return;
            tcs.SetResult(newText);
            messageBox.Close();
        };
        messageBox.ButtonRightClick += (_, _) =>
        {
            tcs.SetResult(null);
            messageBox.Close();
        };
        messageBox.Closing += (_, _) =>
        {
            tcs.TrySetResult(null);
        };
        messageBox.Show();

        // 设置初始文本并把光标定位到末尾，同时让输入框获得焦点
        textBox.Text = text ?? string.Empty;
        textBox.SelectionStart = text?.Length ?? 0;
        textBox.SelectionLength = 0;

        FocusManager.SetFocusedElement(window, textBox);

        return tcs.Task;
    }
}

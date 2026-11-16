using System;
using System.Linq;
using System.Windows;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Macro;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Pages;

/// <summary>
/// “宏”页面：提供宏功能总开关，并通过小键盘按钮选择要编辑的触发键，展示对应的按键序列。
/// </summary>
public partial class MacroPage
{
    /// <summary>宏控制器，负责启用宏功能并读写按键序列。</summary>
    private readonly MacroController _controller = IoCContainer.Resolve<MacroController>();

    /// <summary>
    /// 初始化页面：先订阅初始化事件，再加载 XAML 视图。
    /// </summary>
    public MacroPage()
    {
        Initialized += MacroPage_Initialized;

        InitializeComponent();
    }

    /// <summary>
    /// 初始化完成回调：同步宏开关状态，并默认选中数字键盘最后一项（数字 0）作为当前编辑目标。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数。</param>
    private void MacroPage_Initialized(object? sender, EventArgs e)
    {
        _enableMacroToggle.IsChecked = _controller.IsEnabled;

        var zeroNumberButton = _numberPad.Children.OfType<Button>().Last();
        Reload(zeroNumberButton);
    }

    /// <summary>
    /// 宏功能总开关点击：把开关状态写入宏控制器。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void EnableMacroToggle_Click(object sender, RoutedEventArgs e)
    {
        _controller.SetEnabled(_enableMacroToggle.IsChecked ?? false);
    }

    /// <summary>
    /// 小键盘按钮点击：把全部按钮恢复为次色外观，再高亮当前选中项并载入其按键序列。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void NumberPadButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        _numberPad.Children
            .OfType<Button>()
            .ForEach(b => b.Appearance = ControlAppearance.Secondary);

        Reload(button);
    }

    /// <summary>
    /// 高亮指定按钮并把其 Tag 中的十六进制按键码载入按键序列控件。
    /// </summary>
    /// <param name="button">当前选中的小键盘按钮。</param>
    private void Reload(Button button)
    {
        button.Appearance = ControlAppearance.Primary;

        // 按钮 Tag 为十六进制字符串形式的 HID 按键码。
        var key = Convert.ToUInt64((string)button.Tag, 16);
        _sequenceControl.Set(new(MacroSource.Keyboard, key));
    }
}

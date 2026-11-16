using System;
using System.Threading.Tasks;
using System.Windows;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.RGB;
using LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.Spectrum;

namespace LenovoLegionToolkit.WPF.Pages;

/// <summary>
/// “键盘背光”页面：按硬件能力优先加载 Spectrum 控制面板，否则回退到 RGB 面板；
/// 两者都不支持时显示未检测到键盘的提示。
/// </summary>
public partial class KeyboardBacklightPage
{
    /// <summary>
    /// 初始化页面并加载对应的 XAML 视图。
    /// </summary>
    public KeyboardBacklightPage() => InitializeComponent();

    /// <summary>
    /// 初始化完成回调：延迟探测控制器支持性并挂载对应控制面板。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数。</param>
    private async void KeyboardBacklightPage_Initialized(object? sender, EventArgs e)
    {
        _titleTextBlock.Visibility = Visibility.Collapsed;

        // 延迟 1 秒探测，让加载指示器先完整呈现，避免瞬间切换。
        await Task.Delay(TimeSpan.FromSeconds(1));

        _titleTextBlock.Visibility = Visibility.Visible;

        var spectrumController = IoCContainer.Resolve<SpectrumKeyboardBacklightController>();
        if (await spectrumController.IsSupportedAsync())
        {
            var control = new SpectrumKeyboardBacklightControl();
            _content.Children.Add(control);
            _loader.IsLoading = false;
            return;
        }

        var rgbController = IoCContainer.Resolve<RGBKeyboardBacklightController>();
        if (await rgbController.IsSupportedAsync())
        {
            var control = new RGBKeyboardBacklightControl();
            _content.Children.Add(control);
            _loader.IsLoading = false;
            return;
        }

        // 两种背光控制器均不支持，提示用户当前机型没有可用键盘灯控。
        _noKeyboardsText.Visibility = Visibility.Visible;
        _loader.IsLoading = false;
    }

    /// <summary>
    /// 判断当前机型是否支持任意一种键盘背光控制（Spectrum 或 RGB）。
    /// </summary>
    /// <returns>支持任一背光类型时返回 true。</returns>
    public static async Task<bool> IsSupportedAsync()
    {
        var spectrumController = IoCContainer.Resolve<SpectrumKeyboardBacklightController>();
        if (await spectrumController.IsSupportedAsync())
            return true;

        var rgbController = IoCContainer.Resolve<RGBKeyboardBacklightController>();
        if (await rgbController.IsSupportedAsync())
            return true;

        return false;
    }
}

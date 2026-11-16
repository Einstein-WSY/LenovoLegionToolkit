using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Windows.Dashboard;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using CardControl = LenovoLegionToolkit.WPF.Controls.Custom.CardControl;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “独立显卡超频”仪表盘卡片控件，展示并切换 GPU 超频开关；
/// 开关切换后保存配置并立即应用到硬件，设置窗口关闭后会重新读取状态。
/// </summary>
public class OverclockDiscreteGPUControl : AbstractRefreshingControl
{
    /// <summary>GPU 超频控制器，负责读取/保存超频配置并应用到硬件。</summary>
    private readonly GPUOverclockController _controller = IoCContainer.Resolve<GPUOverclockController>();
    /// <summary>原生 Windows 消息监听器，用于感知显示设备到达等系统事件。</summary>
    private readonly NativeWindowsMessageListener _nativeWindowsMessageListener = IoCContainer.Resolve<NativeWindowsMessageListener>();

    /// <summary>外层卡片容器，决定整张卡片的图标与下边距。</summary>
    private readonly CardControl _cardControl = new()
    {
        Icon = SymbolRegular.DeveloperBoardLightning20,
        Margin = new(0, 0, 0, 8)
    };

    /// <summary>卡片头部，承载标题、副标题与右侧附加区。</summary>
    private readonly CardHeaderControl _cardHeaderControl = new()
    {
        Title = Resource.OverclockDiscreteGPUControl_Title,
        Subtitle = Resource.OverclockDiscreteGPUControl_Message
    };

    /// <summary>横向排列附加区（开关与设置按钮）的容器。</summary>
    private readonly StackPanel _accessoryStackPanel = new()
    {
        Orientation = Orientation.Horizontal
    };

    /// <summary>超频总开关，初始隐藏，待特性支持性检测通过后显示。</summary>
    private readonly ToggleSwitch _toggle = new()
    {
        Visibility = Visibility.Hidden,
        Margin = new(8, 0, 0, 0)
    };

    /// <summary>打开超频详细设置窗口的按钮。</summary>
    private readonly Button _configButton = new()
    {
        Icon = SymbolRegular.Settings24,
        FontSize = 20,
        Margin = new(8, 0, 0, 0)
    };

    /// <summary>
    /// 初始化控件，构建界面并订阅系统消息与超频控制器变化事件。
    /// </summary>
    public OverclockDiscreteGPUControl()
    {
        InitializeComponent();

        _nativeWindowsMessageListener.Changed += NativeWindowsMessageListener_Changed;
        _controller.Changed += Controller_Changed;
    }

    /// <summary>
    /// 构建卡片界面：设置无障碍名称与帮助文本、挂接事件，并将头部与附加区组装进卡片。
    /// </summary>
    private void InitializeComponent()
    {
        AutomationProperties.SetName(_toggle, Resource.OverclockDiscreteGPUControl_Title);
        AutomationProperties.SetName(_configButton, Resource.OverclockDiscreteGPUControl_Title);
        AutomationProperties.SetHelpText(_configButton, Resource.Settings);

        _toggle.Click += Toggle_Click;
        _configButton.Click += ConfigButton_Click;

        _accessoryStackPanel.Children.Add(_toggle);
        _accessoryStackPanel.Children.Add(_configButton);

        _cardHeaderControl.Accessory = _accessoryStackPanel;
        _cardControl.Header = _cardHeaderControl;

        Content = _cardControl;
    }

    /// <summary>
    /// 加载完成回调，此控件无需额外处理。
    /// </summary>
    protected override void OnFinishedLoading() { }

    /// <summary>
    /// 刷新控件：不支持超频的机型抛出异常以隐藏卡片，否则同步开关状态并显示。
    /// </summary>
    /// <returns>异步刷新任务。</returns>
    protected override async Task OnRefreshAsync()
    {
        if (!await _controller.IsSupportedAsync())
            throw new NotSupportedException();

        // GetState 返回是否启用及当前超频参数，此处仅关心开关状态。
        var (enabled, _) = _controller.GetState();
        _toggle.IsChecked = enabled;
        _toggle.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 系统消息回调：显示设备到达（如外接显示器）时重新显示卡片并刷新超频状态。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">包含窗口消息类型的参数。</param>
    private async void NativeWindowsMessageListener_Changed(object? sender, NativeWindowsMessageListener.ChangedEventArgs e)
    {
        if (e.Message != NativeWindowsMessage.OnDisplayDeviceArrival)
            return;

        Visibility = Visibility.Visible;
        await RefreshAsync();
    }

    /// <summary>
    /// 超频控制器变化回调：切回 UI 线程重新显示并刷新卡片。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数（无附加数据）。</param>
    private void Controller_Changed(object? sender, EventArgs e) => Dispatcher.Invoke(async () =>
    {
        Visibility = Visibility.Visible;
        await RefreshAsync();
    });

    /// <summary>
    /// 超频开关点击：保存新状态并立即应用到硬件，失败时仅记录日志。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private async void Toggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // 刷新过程中或开关状态未知时忽略点击，避免写入错误值。
            if (IsRefreshing || _toggle.IsChecked is null)
                return;

            var enabled = _toggle.IsChecked.Value;
            var (_, info) = _controller.GetState();
            _controller.SaveState(enabled, info);
            await _controller.ApplyStateAsync(true);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to change state.", ex);
        }
    }

    /// <summary>
    /// 设置按钮点击：以模态方式打开超频设置窗口，关闭后重新刷新开关状态。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void ConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new OverclockDiscreteGPUSettingsWindow { Owner = Window.GetWindow(this) };
        window.Closed += async (_, _) => await RefreshAsync();
        window.ShowDialog();
    }
}

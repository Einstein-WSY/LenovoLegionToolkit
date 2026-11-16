using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;
using LenovoLegionToolkit.WPF.Windows.Dashboard;
using Wpf.Ui.Common;
using Button = Wpf.Ui.Controls.Button;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “电源模式”仪表盘卡片控件，以下拉框在均衡/性能/自定义等电源模式间切换；
/// 同时监听热模式与电源模式变化，并在性能/极客模式未接电源时给出警告。
/// </summary>
public class PowerModeControl : AbstractComboBoxFeatureCardControl<PowerModeState>
{
    /// <summary>热模式监听器，用于感知固件侧热模式变化。</summary>
    private readonly ThermalModeListener _thermalModeListener = IoCContainer.Resolve<ThermalModeListener>();
    /// <summary>电源模式监听器，用于感知其他来源触发的电源模式变化。</summary>
    private readonly PowerModeListener _powerModeListener = IoCContainer.Resolve<PowerModeListener>();

    /// <summary>节流调度器：500ms 内多次变化只执行一次刷新，降低固件读取频率。</summary>
    private readonly ThrottleLastDispatcher _throttleDispatcher = new(TimeSpan.FromMilliseconds(500), nameof(PowerModeControl));

    /// <summary>打开当前模式详细设置窗口的按钮，仅在支持对应模式的机型上显示。</summary>
    private readonly Button _configButton = new()
    {
        Icon = SymbolRegular.Settings24,
        FontSize = 20,
        Margin = new(8, 0, 0, 0),
        Visibility = Visibility.Collapsed,
    };

    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题、副标题，并订阅热模式与电源模式变化事件。
    /// </summary>
    public PowerModeControl()
    {
        Icon = SymbolRegular.Gauge24;
        Title = Resource.PowerModeControl_Title;
        Subtitle = Resource.PowerModeControl_Message;

        AutomationProperties.SetName(_configButton, Resource.PowerModeControl_Title);

        _thermalModeListener.Changed += ThermalModeListener_Changed;
        _powerModeListener.Changed += PowerModeListener_Changed;
    }

    /// <summary>
    /// 热模式变化回调：经节流后切回 UI 线程刷新，卡片可见时才执行。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">包含热模式变化信息的参数。</param>
    private async void ThermalModeListener_Changed(object? sender, ThermalModeListener.ChangedEventArgs e) => await _throttleDispatcher.DispatchAsync(async () =>
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            if (IsLoaded && IsVisible)
                await RefreshAsync();
        });
    });

    /// <summary>
    /// 电源模式变化回调：经节流后切回 UI 线程刷新，卡片可见时才执行。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">包含电源模式变化信息的参数。</param>
    private async void PowerModeListener_Changed(object? sender, PowerModeListener.ChangedEventArgs e) => await _throttleDispatcher.DispatchAsync(async () =>
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            if (IsLoaded && IsVisible)
                await RefreshAsync();
        });
    });

    /// <summary>
    /// 刷新控件：未接电源适配器且选中性能/极客模式时显示警告提示。
    /// </summary>
    /// <returns>异步刷新任务。</returns>
    protected override async Task OnRefreshAsync()
    {
        await base.OnRefreshAsync();

        // 性能与极客模式需要足够供电，未接适配器时固件可能拒绝该设置。
        if (await Power.IsPowerAdapterConnectedAsync() != PowerAdapterStatus.Connected
            && TryGetSelectedItem(out var state)
            && state is PowerModeState.Performance or PowerModeState.GodMode)
            Warning = Resource.PowerModeControl_Warning;
        else
            Warning = string.Empty;
    }

    /// <summary>
    /// 模式切换完成回调：根据机型能力决定是否显示“详细设置”按钮。
    /// </summary>
    /// <param name="comboBox">触发切换的下拉框。</param>
    /// <param name="feature">电源模式功能实现。</param>
    /// <param name="newValue">切换后的模式。</param>
    /// <param name="oldValue">切换前的模式。</param>
    /// <returns>异步操作任务。</returns>
    protected override async Task OnStateChangeAsync(ComboBox comboBox, IFeature<PowerModeState> feature, PowerModeState? newValue, PowerModeState? oldValue)
    {
        await base.OnStateChangeAsync(comboBox, feature, newValue, oldValue);

        var mi = await Compatibility.GetMachineInformationAsync();

        // 仅当机型支持 AI 模式（均衡档）或极客模式时，才提供对应设置入口。
        switch (newValue)
        {
            case PowerModeState.Balance when mi.Properties.SupportsAIMode:
            case PowerModeState.GodMode when mi.Properties.SupportsGodMode:
                _configButton.ToolTip = Resource.PowerModeControl_Settings;
                _configButton.Visibility = Visibility.Visible;
                break;
            default:
                _configButton.ToolTip = null;
                _configButton.Visibility = Visibility.Collapsed;
                break;
        }
    }

    /// <summary>
    /// 模式切换异常回调：未接电源时切换性能模式会抛出特定异常，这里以提示条通知用户。
    /// </summary>
    /// <param name="exception">切换过程中抛出的异常。</param>
    protected override void OnStateChangeException(Exception exception)
    {
        if (exception is PowerModeUnavailableWithoutACException ex1)
        {
            SnackbarHelper.Show(Resource.PowerModeUnavailableWithoutACException_Title,
                string.Format(Resource.PowerModeUnavailableWithoutACException_Message, ex1.PowerMode.GetDisplayName()),
                SnackbarType.Warning);
        }
    }

    /// <summary>
    /// 构建卡片附加区：在模式下拉框左侧放置“详细设置”按钮，并挂接其点击事件。
    /// </summary>
    /// <param name="comboBox">基类创建的模式下拉框。</param>
    /// <returns>包含设置按钮与下拉框的横向布局容器。</returns>
    protected override FrameworkElement GetAccessory(ComboBox comboBox)
    {
        // 附加区由基类在首次刷新时创建一次，此处订阅点击事件不会重复。
        _configButton.Click += ConfigButton_Click;

        var stackPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
        };
        stackPanel.Children.Add(_configButton);
        stackPanel.Children.Add(comboBox);

        return stackPanel;
    }

    /// <summary>
    /// “详细设置”按钮点击：按当前模式打开对应的设置窗口（均衡模式或极客模式）。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void ConfigButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedItem(out var state))
            return;

        switch (state)
        {
            case PowerModeState.Balance:
                {
                    var window = new BalanceModeSettingsWindow { Owner = Window.GetWindow(this) };
                    window.ShowDialog();
                    break;
                }
            case PowerModeState.GodMode:
                {
                    var window = new GodModeSettingsWindow { Owner = Window.GetWindow(this) };
                    window.ShowDialog();
                    break;
                }
        }
    }
}

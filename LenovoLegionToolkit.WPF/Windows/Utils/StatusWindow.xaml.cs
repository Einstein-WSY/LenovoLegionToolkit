using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Controllers.GodMode;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using Wpf.Ui.Appearance;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Windows.Utils;

/// <summary>
/// 状态浮窗，在鼠标附近汇总展示电源模式、独立显卡、电池与更新状态。
/// </summary>
public partial class StatusWindow
{
    /// <summary>
    /// 状态浮窗需要在初始化时展示的聚合数据。
    /// </summary>
    private readonly struct StatusWindowData(
        PowerModeState? powerModeState,
        string? godModePresetName,
        GPUStatus? gpuStatus,
        BatteryInformation? batteryInformation,
        BatteryState? batteryState)
    {
        /// <summary>当前电源模式。</summary>
        public PowerModeState? PowerModeState { get; } = powerModeState;
        /// <summary>上帝模式下当前预设的名称。</summary>
        public string? GodModePresetName { get; } = godModePresetName;
        /// <summary>独立显卡状态。</summary>
        public GPUStatus? GPUStatus { get; } = gpuStatus;
        /// <summary>电池信息。</summary>
        public BatteryInformation? BatteryInformation { get; } = batteryInformation;
        /// <summary>电池保养状态。</summary>
        public BatteryState? BatteryState { get; } = batteryState;
    }

    /// <summary>
    /// 异步采集各项状态并创建浮窗实例。
    /// </summary>
    /// <returns>已完成数据填充的状态浮窗。</returns>
    public static async Task<StatusWindow> CreateAsync() => new(await GetStatusWindowDataAsync());

    /// <summary>
    /// 逐项容错地采集状态数据，任意一项失败都不影响其余信息展示。
    /// </summary>
    /// <returns>聚合后的状态数据。</returns>
    private static async Task<StatusWindowData> GetStatusWindowDataAsync()
    {
        var powerModeFeature = IoCContainer.Resolve<PowerModeFeature>();
        var godModeController = IoCContainer.Resolve<GodModeController>();
        var gpuController = IoCContainer.Resolve<GPUController>();
        var batteryFeature = IoCContainer.Resolve<BatteryFeature>();

        PowerModeState? state = null;
        string? godModePresetName = null;
        GPUStatus? gpuStatus = null;
        BatteryInformation? batteryInformation = null;
        BatteryState? batteryState = null;

        // 【性能优化】电源模式、独显状态、电池信息、电池模式四项数据互不依赖，
        // 原先串行 await 使浮窗显示延迟等于各次 WMI/NVAPI 往返之和；这里并行采集以缩短首屏延迟。
        // 每项仍保留独立的 try/catch，单项失败不影响其余信息。
        var powerModeTask = Task.Run(async () =>
        {
            try
            {
                if (await powerModeFeature.IsSupportedAsync())
                {
                    state = await powerModeFeature.GetStateAsync();

                    if (state == PowerModeState.GodMode)
                        godModePresetName = await godModeController.GetActivePresetNameAsync();
                }
            }
            catch { /* Ignored */ }
        });

        var gpuTask = Task.Run(async () =>
        {
            try
            {
                if (gpuController.IsSupported())
                    gpuStatus = await gpuController.RefreshNowAsync();

            }
            catch { /* Ignored */ }
        });

        var batteryInfoTask = Task.Run(() =>
        {
            try
            {
                batteryInformation = Battery.GetBatteryInformation();
            }
            catch { /* Ignored */ }
        });

        var batteryStateTask = Task.Run(async () =>
        {
            try
            {
                if (await batteryFeature.IsSupportedAsync())
                    batteryState = await batteryFeature.GetStateAsync();

            }
            catch { /* Ignored */ }
        });

        await Task.WhenAll(powerModeTask, gpuTask, batteryInfoTask, batteryStateTask);

        return new(state, godModePresetName, gpuStatus, batteryInformation, batteryState);
    }

    /// <summary>
    /// 初始化浮窗样式，并把聚合数据填充到各展示区块。
    /// </summary>
    private StatusWindow(StatusWindowData data)
    {
        InitializeComponent();

        Loaded += StatusWindow_Loaded;

        WindowStyle = WindowStyle.None;
        WindowStartupLocation = WindowStartupLocation.Manual;
        WindowBackdropType = BackgroundType.None;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;

        Focusable = false;
        Topmost = true;
        ExtendsContentIntoTitleBar = true;
        ShowInTaskbar = false;
        ShowActivated = false;

#if DEBUG
        _title.Text += " [DEBUG]";
#else
        var version = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
        if (version == new Version(0, 0, 1, 0) || version?.Build == 99)
            _title.Text += " [BETA]";
#endif

        if (Log.Instance.IsTraceEnabled)
            _title.Text += " [LOGGING ENABLED]";

        RefreshPowerMode(data.PowerModeState, data.GodModePresetName);
        RefreshDiscreteGpu(data.GPUStatus);
        RefreshBattery(data.BatteryInformation, data.BatteryState);
    }

    private void StatusWindow_Loaded(object sender, RoutedEventArgs e) => MoveBottomRightEdgeOfWindowToMousePosition();

    /// <summary>
    /// 将浮窗定位到鼠标位置附近，并在靠近屏幕边缘时自动翻转方向避免溢出。
    /// </summary>
    private void MoveBottomRightEdgeOfWindowToMousePosition()
    {
        // 借助呈现源的变换矩阵把物理像素坐标换算为 DIP
        var transform = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice;
        if (!transform.HasValue)
        {
            Left = 0;
            Top = 0;
            return;
        }

        const double offset = 8;

        var mousePoint = Control.MousePosition;
        var screenRectangle = Screen.FromPoint(mousePoint).WorkingArea;

        var mouse = transform.Value.Transform(new Point(mousePoint.X, mousePoint.Y));
        var screen = transform.Value.Transform(new Vector(screenRectangle.Width, screenRectangle.Height));

        if (mouse.X + offset + ActualWidth > screen.X)
            Left = mouse.X - ActualWidth - offset;
        else
            Left = mouse.X + offset;

        // 底部空间不足时改为向上展开
        if (mouse.Y + offset + ActualHeight > screen.Y)
            Top = mouse.Y - ActualHeight - offset;
        else
            Top = mouse.Y + offset;
    }

    /// <summary>
    /// 刷新电源模式区块；处于上帝模式时额外显示当前预设名称。
    /// </summary>
    private void RefreshPowerMode(PowerModeState? powerModeState, string? godModePresetName)
    {
        _powerModeValueLabel.Content = powerModeState?.GetDisplayName() ?? "-";
        _powerModeValueIndicator.Fill = powerModeState?.GetSolidColorBrush() ?? new(Colors.Transparent);

        if (powerModeState == PowerModeState.GodMode)
        {
            _powerModePresetValueLabel.Content = godModePresetName ?? "-";

            _powerModePresetLabel.Visibility = Visibility.Visible;
            _powerModePresetValueLabel.Visibility = Visibility.Visible;
        }
        else
        {
            _powerModePresetLabel.Visibility = Visibility.Collapsed;
            _powerModePresetValueLabel.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// 刷新独立显卡区块，按活动、关闭、休眠三种状态切换展示内容。
    /// </summary>
    private void RefreshDiscreteGpu(GPUStatus? status)
    {
        if (!status.HasValue)
        {
            _gpuGrid.Visibility = Visibility.Collapsed;
            return;
        }

        if (status.Value.State is GPUState.Active or GPUState.MonitorConnected)
        {
            _gpuPowerStateValueLabel.Content = status.Value.PerformanceState ?? "-";

            _gpuActive.Visibility = Visibility.Visible;
            _gpuInactive.Visibility = Visibility.Collapsed;
            _gpuPoweredOff.Visibility = Visibility.Collapsed;
            _gpuPowerStateValue.Visibility = Visibility.Visible;
            _gpuPowerStateValueLabel.Visibility = Visibility.Visible;
        }
        else if (status.Value.State is GPUState.PoweredOff)
        {
            _gpuPowerStateValueLabel.Content = null;

            _gpuActive.Visibility = Visibility.Collapsed;
            _gpuInactive.Visibility = Visibility.Collapsed;
            _gpuPoweredOff.Visibility = Visibility.Visible;
            _gpuPowerStateValue.Visibility = Visibility.Collapsed;
            _gpuPowerStateValueLabel.Visibility = Visibility.Collapsed;
        }
        else
        {
            _gpuPowerStateValueLabel.Content = status.Value.PerformanceState ?? "-";

            _gpuActive.Visibility = Visibility.Collapsed;
            _gpuInactive.Visibility = Visibility.Visible;
            _gpuPoweredOff.Visibility = Visibility.Collapsed;
            _gpuPowerStateValue.Visibility = Visibility.Visible;
            _gpuPowerStateValueLabel.Visibility = Visibility.Visible;
        }

        _gpuGrid.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 刷新电池区块，按电量选择图标，并在充电时区分养护模式。
    /// </summary>
    private void RefreshBattery(BatteryInformation? batteryInformation, BatteryState? batteryState)
    {
        if (!batteryInformation.HasValue || !batteryState.HasValue)
        {
            _batteryIcon.Symbol = SymbolRegular.Battery024;
            _batteryValueLabel.Content = "-";
            _batteryModeValueLabel.Content = "-";
            _batteryDischargeValueLabel.Content = "-";
            _batteryMinDischargeValueLabel.Content = "-";
            _batteryMaxDischargeValueLabel.Content = "-";
            return;
        }

        var symbol = (int)Math.Round(batteryInformation.Value.BatteryPercentage / 10.0) switch
        {
            10 => SymbolRegular.Battery1024,
            9 => SymbolRegular.Battery924,
            8 => SymbolRegular.Battery824,
            7 => SymbolRegular.Battery724,
            6 => SymbolRegular.Battery624,
            5 => SymbolRegular.Battery524,
            4 => SymbolRegular.Battery424,
            3 => SymbolRegular.Battery324,
            2 => SymbolRegular.Battery224,
            1 => SymbolRegular.Battery124,
            _ => SymbolRegular.Battery024,
        };

        if (batteryInformation.Value.IsCharging)
            symbol = batteryState == BatteryState.Conservation ? SymbolRegular.BatterySaver24 : SymbolRegular.BatteryCharge24;

        if (batteryInformation.Value.IsLowBattery)
            _batteryValueLabel.SetResourceReference(ForegroundProperty, "SystemFillColorCautionBrush");

        _batteryIcon.Symbol = symbol;
        _batteryValueLabel.Content = $"{batteryInformation.Value.BatteryPercentage}%";
        _batteryModeValueLabel.Content = batteryState.GetDisplayName();
        _batteryDischargeValueLabel.Content = $"{batteryInformation.Value.DischargeRate / 1000.0:+0.00;-0.00;0.00} W";
        _batteryMinDischargeValueLabel.Content = $"{batteryInformation.Value.MinDischargeRate / 1000.0:+0.00;-0.00;0.00} W";
        _batteryMaxDischargeValueLabel.Content = $"{batteryInformation.Value.MaxDischargeRate / 1000.0:+0.00;-0.00;0.00} W";
    }
}

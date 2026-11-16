using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Humanizer;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers.Sensors;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Settings;
using Wpf.Ui.Common;
using MenuItem = Wpf.Ui.Controls.MenuItem;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “传感器”仪表盘卡片控件，展示 CPU/GPU 的利用率、频率、温度与风扇转速；
/// 通过后台循环按用户设置的间隔（1/2/3/5 秒）轮询传感器控制器，仅在卡片可见时运行。
/// </summary>
public partial class SensorsControl
{
    /// <summary>传感器控制器，负责读取 CPU/GPU 各项指标。</summary>
    private readonly ISensorsController _controller = IoCContainer.Resolve<ISensorsController>();
    /// <summary>应用设置，用于读取用户偏好的温度单位。</summary>
    private readonly ApplicationSettings _applicationSettings = IoCContainer.Resolve<ApplicationSettings>();
    /// <summary>仪表盘设置，用于读取并保存传感器刷新间隔。</summary>
    private readonly DashboardSettings _dashboardSettings = IoCContainer.Resolve<DashboardSettings>();

    /// <summary>后台刷新循环的取消令牌源，卡片隐藏时取消。</summary>
    private CancellationTokenSource? _cts;
    /// <summary>后台刷新循环任务，卡片隐藏时等待其结束。</summary>
    private Task? _refreshTask;

    /// <summary>
    /// 初始化控件，加载 XAML 视图、构建右键刷新间隔菜单，并订阅可见性变化事件。
    /// </summary>
    public SensorsControl()
    {
        InitializeComponent();
        InitializeContextMenu();

        IsVisibleChanged += SensorsControl_IsVisibleChanged;
    }

    /// <summary>
    /// 构建右键上下文菜单：列出可选刷新间隔，当前值以勾选图标标记，选择后立即持久化设置。
    /// </summary>
    private void InitializeContextMenu()
    {
        ContextMenu = new ContextMenu();
        ContextMenu.Items.Add(new MenuItem { Header = Resource.SensorsControl_RefreshInterval, IsEnabled = false });

        // 重建菜单时附带当前选中状态，避免菜单项勾选滞后。
        foreach (var interval in new[] { 1, 2, 3, 5 })
        {
            var item = new MenuItem
            {
                SymbolIcon = _dashboardSettings.Store.SensorsRefreshIntervalSeconds == interval ? SymbolRegular.Checkmark24 : SymbolRegular.Empty,
                Header = TimeSpan.FromSeconds(interval).Humanize(culture: Resource.Culture)
            };
            item.Click += (_, _) =>
            {
                _dashboardSettings.Store.SensorsRefreshIntervalSeconds = interval;
                _dashboardSettings.SynchronizeStore();
                // 重建菜单以刷新勾选标记。
                InitializeContextMenu();
            };
            ContextMenu.Items.Add(item);
        }
    }

    /// <summary>
    /// 可见性变化回调：卡片出现时启动刷新循环，隐藏时取消循环、等待任务结束并清空数值。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">依赖属性变化参数。</param>
    private async void SensorsControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            Refresh();
            return;
        }

        // 先请求取消，再等待后台任务退出，确保不会残留轮询。
        if (_cts is not null)
            await _cts.CancelAsync();

        _cts = null;

        if (_refreshTask is not null)
            await _refreshTask;

        _refreshTask = null;

        // 隐藏后重置为占位数据，避免下次显示时短暂显示过期数值。
        UpdateValues(SensorsData.Empty);
    }

    /// <summary>
    /// 启动传感器后台轮询：先在后台线程检测支持性与准备工作，随后按设定间隔循环读取数据。
    /// </summary>
    private void Refresh()
    {
        // 若存在上一轮循环，先取消以避免两个轮询同时运行。
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        var token = _cts.Token;

        _refreshTask = Task.Run(async () =>
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Sensors refresh started...");

            // 本机不支持传感器读取时直接隐藏卡片，不再启动循环。
            if (!await _controller.IsSupportedAsync())
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Sensors not supported.");

                // 【性能优化】用 InvokeAsync 投递而非阻塞等待 UI 线程，避免轮询线程被界面渲染阻塞导致采样间隔抖动
                _ = Dispatcher.InvokeAsync(() => Visibility = Visibility.Collapsed);
                return;
            }

            // 读取前的一次性初始化（如打开硬件访问通道）。
            await _controller.PrepareAsync();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var data = await _controller.GetDataAsync();
                    // 传感器数据更新必须回到 UI 线程写入控件。
                    // 【性能优化】异步投递更新，采样线程无需等待 UI 线程空闲
                    _ = Dispatcher.InvokeAsync(() => UpdateValues(data));
                    // 按用户在右键菜单中设置的间隔等待，以可取消的方式延时。
                    await Task.Delay(TimeSpan.FromSeconds(_dashboardSettings.Store.SensorsRefreshIntervalSeconds), token);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Sensors refresh failed.", ex);

                    // 读取失败时清空显示，避免残留过期数值误导用户。
                    _ = Dispatcher.InvokeAsync(() => UpdateValues(SensorsData.Empty));
                }
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Sensors refresh stopped.");
        }, token);
    }

    /// <summary>
    /// 将一组传感器数据写入进度条与数值标签；频率由 MHz 换算为 GHz，温度按设置换算为摄氏或华氏。
    /// </summary>
    /// <param name="data">本次采样得到的 CPU/GPU 传感器数据。</param>
    private void UpdateValues(SensorsData data)
    {
        UpdateValue(_cpuUtilizationBar, _cpuUtilizationLabel, data.CPU.MaxUtilization, data.CPU.Utilization,
            $"{data.CPU.Utilization}%");
        // CPU 核心频率以 MHz 上报，这里除以 1000 换算为 GHz 并保留一位小数。
        UpdateValue(_cpuCoreClockBar, _cpuCoreClockLabel, data.CPU.MaxCoreClock, data.CPU.CoreClock,
            $"{data.CPU.CoreClock / 1000.0:0.0} {Resource.GHz}", $"{data.CPU.MaxCoreClock / 1000.0:0.0} {Resource.GHz}");
        UpdateValue(_cpuTemperatureBar, _cpuTemperatureLabel, data.CPU.MaxTemperature, data.CPU.Temperature,
            GetTemperatureText(data.CPU.Temperature), GetTemperatureText(data.CPU.MaxTemperature));
        UpdateValue(_cpuFanSpeedBar, _cpuFanSpeedLabel, data.CPU.MaxFanSpeed, data.CPU.FanSpeed,
            $"{data.CPU.FanSpeed} {Resource.RPM}", $"{data.CPU.MaxFanSpeed} {Resource.RPM}");

        UpdateValue(_gpuUtilizationBar, _gpuUtilizationLabel, data.GPU.MaxUtilization, data.GPU.Utilization,
            $"{data.GPU.Utilization} %");
        UpdateValue(_gpuCoreClockBar, _gpuCoreClockLabel, data.GPU.MaxCoreClock, data.GPU.CoreClock,
            $"{data.GPU.CoreClock} {Resource.MHz}", $"{data.GPU.MaxCoreClock} {Resource.MHz}");
        UpdateValue(_gpuMemoryClockBar, _gpuMemoryClockLabel, data.GPU.MaxMemoryClock, data.GPU.MemoryClock,
            $"{data.GPU.MemoryClock} {Resource.MHz}", $"{data.GPU.MaxMemoryClock} {Resource.MHz}");
        UpdateValue(_gpuTemperatureBar, _gpuTemperatureLabel, data.GPU.MaxTemperature, data.GPU.Temperature,
            GetTemperatureText(data.GPU.Temperature), GetTemperatureText(data.GPU.MaxTemperature));
        UpdateValue(_gpuFanSpeedBar, _gpuFanSpeedLabel, data.GPU.MaxFanSpeed, data.GPU.FanSpeed,
            $"{data.GPU.FanSpeed} {Resource.RPM}", $"{data.GPU.MaxFanSpeed} {Resource.RPM}");
    }

    /// <summary>
    /// 根据应用设置中的温度单位格式化温度文本，华氏度由摄氏度按 F = C × 9/5 + 32 换算并取整。
    /// </summary>
    /// <param name="temperature">摄氏温度值。</param>
    /// <returns>带单位后缀的温度文本。</returns>
    private string GetTemperatureText(double temperature)
    {
        if (_applicationSettings.Store.TemperatureUnit == TemperatureUnit.F)
        {
            // 摄氏转华氏：先乘 9/5，再加 32。
            temperature *= 9.0 / 5.0;
            temperature += 32;
            return $"{temperature:0} {Resource.Fahrenheit}";
        }

        return $"{temperature:0} {Resource.Celsius}";
    }

    /// <summary>
    /// 更新单个指标：数值无效（负值）时显示占位符并复位进度条，否则按最大值设定进度条量程。
    /// </summary>
    /// <param name="bar">显示用进度条。</param>
    /// <param name="label">显示数值文本的标签。</param>
    /// <param name="max">该指标的量程上限。</param>
    /// <param name="value">当前值。</param>
    /// <param name="text">主显示文本。</param>
    /// <param name="toolTipText">可选的提示文本（通常为最大值说明）。</param>
    private static void UpdateValue(RangeBase bar, ContentControl label, double max, double value, string text, string? toolTipText = null)
    {
        // 负值表示传感器未上报，统一显示 “-” 并让进度条归零。
        if (max < 0 || value < 0)
        {
            bar.Minimum = 0;
            bar.Maximum = 1;
            bar.Value = 0;
            label.Content = "-";
            label.ToolTip = null;
            label.Tag = 0;
        }
        else
        {
            bar.Minimum = 0;
            bar.Maximum = max;
            bar.Value = value;
            label.Content = text;
            // 提示文本为最大值摘要，无最大值信息时不显示提示。
            label.ToolTip = toolTipText is null ? null : string.Format(Resource.SensorsControl_Maximum, toolTipText);
            label.Tag = value;
        }
    }
}

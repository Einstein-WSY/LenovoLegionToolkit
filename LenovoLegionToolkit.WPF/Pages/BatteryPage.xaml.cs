using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Humanizer;
using Humanizer.Localisation;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Pages;

/// <summary>
/// “电池”页面：每 2 秒轮询一次电池信息与电源适配器状态，展示电量、健康度、
/// 充放电功率、容量、温度与使用/制造日期等指标；页面不可见时停止轮询。
/// </summary>
public partial class BatteryPage
{
    /// <summary>应用设置，用于读取用户偏好的温度单位。</summary>
    private readonly ApplicationSettings _settings = IoCContainer.Resolve<ApplicationSettings>();

    /// <summary>刷新循环的取消令牌源，页面隐藏时取消。</summary>
    private CancellationTokenSource? _cts;
    /// <summary>刷新循环任务，页面隐藏时等待其结束。</summary>
    private Task? _refreshTask;

    /// <summary>
    /// 初始化页面并加载 XAML 视图，同时订阅可见性变化事件。
    /// </summary>
    public BatteryPage()
    {
        InitializeComponent();

        IsVisibleChanged += BatteryPage_IsVisibleChanged;
    }

    /// <summary>
    /// 可见性变化回调：页面出现时启动轮询，隐藏时取消循环并等待任务退出。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">依赖属性变化参数。</param>
    private async void BatteryPage_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            Refresh();
            return;
        }

        if (_cts is not null)
            await _cts.CancelAsync();

        _cts = null;

        if (_refreshTask is not null)
            await _refreshTask;

        _refreshTask = null;
    }

    /// <summary>
    /// 启动电池信息轮询循环（2 秒一次），并在 UI 线程上更新页面内容。
    /// </summary>
    private void Refresh()
    {
        // 若存在上一轮循环，先取消以避免并发轮询。
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        var token = _cts.Token;

        _refreshTask = Task.Run(async () =>
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Battery information refresh started...");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var batteryInfo = Battery.GetBatteryInformation();
                    var powerAdapterStatus = await Power.IsPowerAdapterConnectedAsync();
                    var onBatterySince = Battery.GetOnBatterySince();
                    // 界面更新必须回到 UI 线程执行。
                    // 【性能优化】异步投递更新，轮询线程无需阻塞等待 UI 线程，保持 2 秒节奏稳定
                    _ = Dispatcher.InvokeAsync(() => Set(batteryInfo, powerAdapterStatus, onBatterySince));

                    // 2 秒轮询一次，兼顾及时性与系统开销。
                    await Task.Delay(TimeSpan.FromSeconds(2), token);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Battery information refresh failed.", ex);
                }
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Battery information refresh stopped.");
        }, token);
    }

    /// <summary>
    /// 将一次采样的电池数据写入界面各字段。
    /// </summary>
    /// <param name="batteryInfo">电池信息快照。</param>
    /// <param name="powerAdapterStatus">电源适配器连接状态。</param>
    /// <param name="onBatterySince">开始使用电池的时间（未知时为 null）。</param>
    private void Set(BatteryInformation batteryInfo, PowerAdapterStatus powerAdapterStatus, DateTime? onBatterySince)
    {
        // 电量百分比按 10% 一档取整，映射到对应的电池图标。
        var number = (int)Math.Round(batteryInfo.BatteryPercentage / 10.0);
        _batteryIcon.Symbol = number switch
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

        _percentRemaining.Text = $"{batteryInfo.BatteryPercentage}%";
        _status.Text = GetStatusText(batteryInfo);
        _lowBattery.Visibility = batteryInfo.IsLowBattery ? Visibility.Visible : Visibility.Collapsed;
        _lowWattageCharger.Visibility = powerAdapterStatus == PowerAdapterStatus.ConnectedLowWattage ? Visibility.Visible : Visibility.Collapsed;

        if (batteryInfo.BatteryTemperatureC is not null)
            _batteryTemperatureText.Text = GetTemperatureText(batteryInfo.BatteryTemperatureC);
        else
            _batteryTemperatureCardControl.Visibility = Visibility.Collapsed;

        if (!batteryInfo.IsCharging && onBatterySince.HasValue)
        {
            var onBatterySinceValue = onBatterySince.Value;
            var dateText = onBatterySinceValue.ToString("G", Resource.Culture);
            var duration = DateTime.Now.Subtract(onBatterySinceValue);
            // 同时显示起始时间与已使用时长（保留两级单位，如“2 小时 5 分钟”）。
            _onBatterySinceText.Text = $"{dateText} ({duration.Humanize(2, Resource.Culture, minUnit: TimeUnit.Second)})";
        }
        else
        {
            _onBatterySinceText.Text = "-";
        }

        // 硬件以毫瓦上报功率，除以 1000 换算为瓦，并保留正负号区分充放电。
        _batteryDischargeRateText.Text = $"{batteryInfo.DischargeRate / 1000.0:+0.00;-0.00;0.00} W";
        _batteryMinDischargeRateText.Text = $"{batteryInfo.MinDischargeRate / 1000.0:+0.00;-0.00;0.00} W";
        _batteryMaxDischargeRateText.Text = $"{batteryInfo.MaxDischargeRate / 1000.0:+0.00;-0.00;0.00} W";
        // 容量以毫瓦时上报，除以 1000 换算为瓦时。
        _batteryCapacityText.Text = $"{batteryInfo.EstimateChargeRemaining / 1000.0:0.00} Wh";
        _batteryFullChargeCapacityText.Text = $"{batteryInfo.FullChargeCapacity / 1000.0:0.00} Wh";
        _batteryDesignCapacityText.Text = $"{batteryInfo.DesignCapacity / 1000.0:0.00} Wh";
        _batteryHealthText.Text = $"{batteryInfo.BatteryHealth:0.00} %";

        if (batteryInfo.ManufactureDate is not null)
            _batteryManufactureDateText.Text = batteryInfo.ManufactureDate?.ToString(LocalizationHelper.ShortDateFormat) ?? "-";
        else
            _batteryManufactureDateCardControl.Visibility = Visibility.Collapsed;

        if (batteryInfo.FirstUseDate is not null)
            _batteryFirstUseDateText.Text = batteryInfo.FirstUseDate?.ToString(LocalizationHelper.ShortDateFormat) ?? "-";
        else
            _batteryFirstUseDateCardControl.Visibility = Visibility.Collapsed;

        _batteryCycleCountText.Text = $"{batteryInfo.CycleCount}";
    }

    /// <summary>
    /// 根据充电状态与剩余时间生成状态描述文本。
    /// </summary>
    /// <param name="batteryInfo">电池信息快照。</param>
    /// <returns>本地化的状态文本。</returns>
    private static string GetStatusText(BatteryInformation batteryInfo)
    {
        if (batteryInfo.IsCharging)
        {
            // 放电功率大于 0 说明同时存在放电，通常为充电功率不足导致的边充边放。
            if (batteryInfo.DischargeRate > 0)
                return Resource.BatteryPage_ACAdapterConnectedAndCharging;

            return Resource.BatteryPage_ACAdapterConnectedNotCharging;
        }

        // 剩余时间为负值表示系统仍在估算，无法给出预计续航。
        if (batteryInfo.BatteryLifeRemaining < 0)
            return Resource.BatteryPage_EstimatingBatteryLife;

        var time = TimeSpan.FromSeconds(batteryInfo.BatteryLifeRemaining).Humanize(2, Resource.Culture);
        return string.Format(Resource.BatteryPage_EstimatedBatteryLifeRemaining, time);
    }

    /// <summary>
    /// 按应用设置格式化电池温度：华氏度由摄氏度按 F = C × 9/5 + 32 换算，保留一位小数。
    /// </summary>
    /// <param name="temperature">摄氏温度值。</param>
    /// <returns>带单位后缀的温度文本。</returns>
    private string GetTemperatureText(double? temperature)
    {
        // Tag 保留原始摄氏度值，便于其他位置复用。
        _batteryTemperatureCardControl.Tag = temperature;

        if (temperature is null)
            return "—";

        if (_settings.Store.TemperatureUnit == TemperatureUnit.F)
        {
            // 摄氏转华氏：先乘 9/5，再加 32。
            temperature *= 9.0 / 5.0;
            temperature += 32;
            return $"{temperature:0.0} {Resource.Fahrenheit}";
        }

        return $"{temperature:0.0} {Resource.Celsius}";
    }
}

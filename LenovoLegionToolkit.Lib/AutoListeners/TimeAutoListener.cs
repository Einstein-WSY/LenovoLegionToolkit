using System;
using System.Threading.Tasks;
using System.Timers;
using LenovoLegionToolkit.Lib.Extensions;

namespace LenovoLegionToolkit.Lib.AutoListeners;

/// <summary>
/// 时间变化监听器：每分钟触发一次，上报当前时间与星期，用于定时类自动化规则。
/// </summary>
public class TimeAutoListener : AbstractAutoListener<TimeAutoListener.ChangedEventArgs>
{
    /// <summary>
    /// 时间事件参数。
    /// </summary>
    public class ChangedEventArgs(Time time, DayOfWeek day) : EventArgs
    {
        public Time Time { get; } = time; // 当前时分
        public DayOfWeek Day { get; } = day; // 当前星期
    }

    private readonly Timer _timer;

    public TimeAutoListener()
    {
        _timer = new Timer(60_000); // 60 秒轮询一次，精度足以满足按分钟触发的规则
        _timer.Elapsed += Timer_Elapsed;
        _timer.AutoReset = true;
    }

    protected override Task StartAsync()
    {
        if (!_timer.Enabled)
            _timer.Enabled = true;

        return Task.CompletedTask;
    }

    protected override Task StopAsync()
    {
        _timer.Enabled = false;

        return Task.CompletedTask;
    }

    private void Timer_Elapsed(object? sender, ElapsedEventArgs e) => RaiseChanged(new ChangedEventArgs(TimeExtensions.UtcNow, DateTime.UtcNow.DayOfWeek));
}

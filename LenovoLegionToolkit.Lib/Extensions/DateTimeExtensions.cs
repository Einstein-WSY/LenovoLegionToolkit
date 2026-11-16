using System;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供日期时间的扩展与辅助构造方法，用于构造"当天/本周指定时刻"的时间点，
/// 主要服务于自动化（如开机、关机、充电阈值等）的定时计划。
/// </summary>
public static class DateTimeExtensions
{
    /// <summary>
    /// 构造"今天"（UTC 日期）上指定小时与分钟的 UTC 时间。
    /// </summary>
    /// <param name="hours">小时（0-23）。</param>
    /// <param name="minutes">分钟（0-59）。</param>
    /// <returns>今天 UTC 时刻的 <see cref="DateTime"/>（Kind 为 Utc）。</returns>
    public static DateTime UtcFrom(int hours, int minutes)
    {
        var now = DateTime.UtcNow;
        return new(now.Year, now.Month, now.Day, hours, minutes, 0, DateTimeKind.Utc);
    }

    /// <summary>
    /// 构造"今天"（本地日期）上指定小时与分钟的本地时间。
    /// </summary>
    /// <param name="hours">小时（0-23）。</param>
    /// <param name="minutes">分钟（0-59）。</param>
    /// <returns>今天本地时刻的 <see cref="DateTime"/>（Kind 为 Local）。</returns>
    public static DateTime LocalFrom(int hours, int minutes)
    {
        var now = DateTime.Now;
        return new(now.Year, now.Month, now.Day, hours, minutes, 0, DateTimeKind.Local);
    }

    /// <summary>
    /// 构造本周内（自今天起向前推算）指定星期几的 UTC 时刻。
    /// </summary>
    /// <param name="targetDay">目标星期几。</param>
    /// <param name="hours">小时（0-23）。</param>
    /// <param name="minutes">分钟（0-59）。</param>
    /// <returns>目标星期几的 UTC 时刻；若目标日就是今天，则返回今天该时刻。</returns>
    public static DateTime UtcDayFrom(DayOfWeek targetDay, int hours, int minutes)
    {
        var now = DateTime.UtcNow;
        var date = new DateTime(now.Year, now.Month, now.Day, hours, minutes, now.Second, DateTimeKind.Utc);
        // 计算从当前星期到目标星期需增加的天数：+7 再取模，避免目标日在当前日之前时出现负数
        var daysUntilDayOfWeek = ((int)targetDay - (int)date.DayOfWeek + 7) % 7;
        return date.AddDays(daysUntilDayOfWeek);
    }

    /// <summary>
    /// 构造本周内（自今天起向前推算）指定星期几的本地时刻。
    /// </summary>
    /// <param name="targetDay">目标星期几。</param>
    /// <param name="hours">小时（0-23）。</param>
    /// <param name="minutes">分钟（0-59）。</param>
    /// <returns>目标星期几的本地时刻；若目标日就是今天，则返回今天该时刻。</returns>
    public static DateTime LocalDayFrom(DayOfWeek targetDay, int hours, int minutes)
    {
        var now = DateTime.Now;
        var date = new DateTime(now.Year, now.Month, now.Day, hours, minutes, now.Second, DateTimeKind.Local);
        // 计算从当前星期到目标星期需增加的天数：+7 再取模，避免目标日在当前日之前时出现负数
        var daysUntilDayOfWeek = ((int)targetDay - (int)date.DayOfWeek + 7) % 7;
        return date.AddDays(daysUntilDayOfWeek);
    }
}

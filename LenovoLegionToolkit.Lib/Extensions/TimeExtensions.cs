using System;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供 <see cref="Time"/> 的扩展/工厂方法。
/// </summary>
public static class TimeExtensions
{
    /// <summary>
    /// 获取当前 UTC 时间对应的时:分（忽略日期与秒）。
    /// </summary>
    public static Time UtcNow
    {
        get
        {
            var utcNow = DateTime.UtcNow;
            return new(utcNow.Hour, utcNow.Minute);
        }
    }
}

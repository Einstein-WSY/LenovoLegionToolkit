using System;

namespace LenovoLegionToolkit.Lib.Settings;

/// <summary>
/// 日出日落设置：持久化到 sunrise_sunset.json，缓存最近一次查询到的日出/日落时间，
/// 避免频繁调用在线接口（供自动切换主题等场景使用）。
/// </summary>
public class SunriseSunsetSettings()
    : AbstractSettings<SunriseSunsetSettings.SunriseSunsetSettingsStore>("sunrise_sunset.json")
{
    /// <summary>
    /// sunrise_sunset.json 的数据结构。
    /// </summary>
    public class SunriseSunsetSettingsStore
    {
        /// <summary>
        /// 上次成功查询日出日落的时间。
        /// </summary>
        public DateTime? LastCheckDateTime { get; set; }

        /// <summary>
        /// 缓存的日出时间。
        /// </summary>
        public Time? Sunrise { get; set; }

        /// <summary>
        /// 缓存的日落时间。
        /// </summary>
        public Time? Sunset { get; set; }
    }
}

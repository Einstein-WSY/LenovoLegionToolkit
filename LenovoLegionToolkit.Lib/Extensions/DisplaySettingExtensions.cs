using WindowsDisplayAPI;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供 <see cref="DisplaySetting"/> 的扩展方法。
/// </summary>
public static class DisplaySettingExtensions
{
    /// <summary>
    /// 将显示设置格式化为便于阅读和日志输出的字符串。
    /// </summary>
    /// <param name="displaySetting">显示设置。</param>
    /// <returns>形如 "1920x1080p @ 60Hz @ 32 ..." 的描述字符串，"i" 表示隔行、"p" 表示逐行。</returns>
    public static string ToExtendedString(this DisplaySetting displaySetting)
    {
        return $"{displaySetting.Resolution.Width}x{displaySetting.Resolution.Height}{(displaySetting.IsInterlaced ? "i" : "p")} @ {displaySetting.Frequency}Hz @ {displaySetting.ColorDepth} ({displaySetting.Position}, {displaySetting.Orientation}, {displaySetting.OutputScalingMode})";
    }
}

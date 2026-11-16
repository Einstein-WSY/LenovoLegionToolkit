using System;
using WindowsDisplayAPI;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供 <see cref="DisplayPossibleSetting"/> 的扩展方法。
/// </summary>
public static class DisplayPossibleSettingExtensions
{
    /// <summary>
    /// 判断该显示模式的分辨率是否过小（长边或宽边小于 1000 像素）。
    /// <para>
    /// 用于过滤显示模式列表中的低分辨率选项（如 800x600），避免出现在可选列表中。
    /// </para>
    /// </summary>
    /// <param name="dps">待判断的显示模式。</param>
    /// <returns>若宽或高中较大者小于 1000 则返回 <c>true</c>。</returns>
    public static bool IsTooSmall(this DisplayPossibleSetting dps) => Math.Max(dps.Resolution.Width, dps.Resolution.Height) < 1000;
}

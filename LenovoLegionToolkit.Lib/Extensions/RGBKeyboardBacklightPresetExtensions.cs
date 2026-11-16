namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供 RGB 键盘背光预设的扩展方法。
/// </summary>
public static class RGBKeyboardBacklightPresetExtensions
{
    /// <summary>
    /// 按"关闭 → 1 → 2 → 3 → 4 → 关闭"的顺序循环切换到下一个预设。
    /// </summary>
    /// <param name="preset">当前预设。</param>
    /// <returns>下一个预设。</returns>
    public static RGBKeyboardBacklightPreset Next(this RGBKeyboardBacklightPreset preset) => preset switch
    {
        RGBKeyboardBacklightPreset.Off => RGBKeyboardBacklightPreset.One,
        RGBKeyboardBacklightPreset.One => RGBKeyboardBacklightPreset.Two,
        RGBKeyboardBacklightPreset.Two => RGBKeyboardBacklightPreset.Three,
        RGBKeyboardBacklightPreset.Three => RGBKeyboardBacklightPreset.Four,
        _ => RGBKeyboardBacklightPreset.Off,
    };
}

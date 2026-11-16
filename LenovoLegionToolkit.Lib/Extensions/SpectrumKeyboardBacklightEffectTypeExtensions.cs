namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供 <see cref="SpectrumKeyboardBacklightEffectType"/> 的扩展方法，用于区分灯效的作用范围。
/// </summary>
public static class SpectrumKeyboardBacklightEffectTypeExtensions
{

    /// <summary>
    /// 判断该灯效是否作用于整机所有灯区（如音频律动、极光同步等全局特效）。
    /// </summary>
    /// <param name="type">灯效类型。</param>
    /// <returns>属于全局特效时返回 <c>true</c>。</returns>
    public static bool IsAllLightsEffect(this SpectrumKeyboardBacklightEffectType type) => type switch
    {
        SpectrumKeyboardBacklightEffectType.AudioBounce => true,
        SpectrumKeyboardBacklightEffectType.AudioRipple => true,
        SpectrumKeyboardBacklightEffectType.AuroraSync => true,
        _ => false
    };

    /// <summary>
    /// 判断该灯效是否作用于整个键盘（如打字波浪、涟漪等覆盖全键盘的动画）。
    /// </summary>
    /// <param name="type">灯效类型。</param>
    /// <returns>作用于整个键盘时返回 <c>true</c>。</returns>
    public static bool IsWholeKeyboardEffect(this SpectrumKeyboardBacklightEffectType type) => type switch
    {
        SpectrumKeyboardBacklightEffectType.Type => true,
        SpectrumKeyboardBacklightEffectType.Ripple => true,
        _ => false
    };
}

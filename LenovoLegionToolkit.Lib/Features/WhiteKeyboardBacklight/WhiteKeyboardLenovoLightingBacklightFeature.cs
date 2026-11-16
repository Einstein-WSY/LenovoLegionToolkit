namespace LenovoLegionToolkit.Lib.Features.WhiteKeyboardBacklight;

/// <summary>
/// 白色键盘背光特性 —— 基于 Lenovo Lighting 接口实现。
/// 灯效 ID 为 0、控制接口 0、类型 1；亮度等级从 1 开始计数，
/// 因此与枚举值之间需要 ±1 转换。
/// </summary>
public class WhiteKeyboardLenovoLightingBacklightFeature()
    : AbstractLenovoLightingFeature<WhiteKeyboardBacklightState>(0, 0, 1)
{
    // WMI 返回的 level 从 1 开始（1=关），枚举从 0 开始，故需减 1
    protected override WhiteKeyboardBacklightState FromInternal(int _, int level) => (WhiteKeyboardBacklightState)(level - 1);

    // 写回时枚举值加 1 还原为 WMI 的 level 计数
    protected override (int stateType, int level) ToInternal(WhiteKeyboardBacklightState state) => (0, (int)(state + 1));
}

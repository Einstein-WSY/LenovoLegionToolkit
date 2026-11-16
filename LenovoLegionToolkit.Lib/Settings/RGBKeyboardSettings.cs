using System.Collections.Generic;

namespace LenovoLegionToolkit.Lib.Settings;

/// <summary>
/// RGB 键盘设置：持久化到 rgb_keyboard.json，保存当前灯效状态与各预设的具体参数。
/// </summary>
public class RGBKeyboardSettings() : AbstractSettings<RGBKeyboardSettings.RGBKeyboardSettingsStore>("rgb_keyboard.json")
{
    /// <summary>
    /// rgb_keyboard.json 的数据结构。
    /// </summary>
    public class RGBKeyboardSettingsStore
    {
        /// <summary>
        /// 当前的键盘背光状态（当前预设及各预设的灯效描述）。
        /// </summary>
        public RGBKeyboardBacklightState State { get; set; }
    }

    /// <summary>
    /// 首次运行时的默认灯效：关闭状态，并内置四个常见预设（双色渐变、红色常亮、白色呼吸、白色平滑）。
    /// </summary>
    protected override RGBKeyboardSettingsStore Default => new()
    {
        State = new(RGBKeyboardBacklightPreset.Off, new Dictionary<RGBKeyboardBacklightPreset, RGBKeyboardBacklightBacklightPresetDescription> {
            { RGBKeyboardBacklightPreset.One, new(RGBKeyboardBacklightEffect.Static, RGBKeyboardBacklightSpeed.Slowest, RGBKeyboardBacklightBrightness.Low, RGBColor.Green, RGBColor.Teal, RGBColor.Purple, RGBColor.Pink) },
            { RGBKeyboardBacklightPreset.Two, new(RGBKeyboardBacklightEffect.Static, RGBKeyboardBacklightSpeed.Slowest, RGBKeyboardBacklightBrightness.Low, RGBColor.Red, RGBColor.Red, RGBColor.Red, RGBColor.Red) },
            { RGBKeyboardBacklightPreset.Three, new(RGBKeyboardBacklightEffect.Breath, RGBKeyboardBacklightSpeed.Slowest, RGBKeyboardBacklightBrightness.Low,  RGBColor.White,RGBColor.White,RGBColor.White,RGBColor.White) },
            { RGBKeyboardBacklightPreset.Four, new(RGBKeyboardBacklightEffect.Smooth, RGBKeyboardBacklightSpeed.Slowest, RGBKeyboardBacklightBrightness.Low, RGBColor.White,RGBColor.White,RGBColor.White,RGBColor.White) },
        }),
    };
}

using System;
using System.Linq;
using LenovoLegionToolkit.Lib;

namespace LenovoLegionToolkit.WPF.CLI.Features;

/// <summary>
/// CLI 功能注册表，集中声明所有可被外部 CLI 读写的硬件功能。
/// </summary>
public static class FeatureRegistry
{
    /// <summary>
    /// 全部已注册的功能；部分功能提供自定义的字符串转换器（如刷新率、分辨率）。
    /// </summary>
    public static readonly IFeatureRegistration[] All =
    [
        new FeatureRegistration<AlwaysOnUSBState>("always-on-usb"),
        new FeatureRegistration<BatteryState>("battery"),
        new FeatureRegistration<BatteryNightChargeState>("battery-night-charge"),
        new FeatureRegistration<FlipToStartState>("flip-to-start"),
        new FeatureRegistration<FnLockState>("fn-lock"),
        new FeatureRegistration<HDRState>("hdr"),
        new FeatureRegistration<HybridModeState>("hybrid-mode"),
        new FeatureRegistration<InstantBootState>("instant-boot"),
        new FeatureRegistration<MicrophoneState>("microphone"),
        new FeatureRegistration<OneLevelWhiteKeyboardBacklightState>("one-level-white-keyboard-backlight"),
        new FeatureRegistration<OverDriveState>("over-drive"),
        new FeatureRegistration<PanelLogoBacklightState>("panel-logo-backlight"),
        new FeatureRegistration<PortsBacklightState>("ports-backlight"),
        new FeatureRegistration<PowerModeState>("power-mode"),
        // 刷新率：字符串仅暴露频率数值，解析时构造 RefreshRate
        new FeatureRegistration<RefreshRate>("refresh-rate",
            s => s.Frequency.ToString(),
            s => new(Convert.ToInt32(s))),
        // 分辨率：字符串形如 "宽x高"
        new FeatureRegistration<Resolution>("resolution",
            s => $"{s.Width}x{s.Height}",
            s =>
            {
                var split = s.Split('x');
                return new(Convert.ToInt32(split.FirstOrDefault()), Convert.ToInt32(split.LastOrDefault()));
            }),
        new FeatureRegistration<SpeakerState>("speaker"),
        new FeatureRegistration<TouchpadLockState>("touchpad-lock"),
        new FeatureRegistration<WinKeyState>("win-key"),
        new FeatureRegistration<WhiteKeyboardBacklightState>("white-keyboard-backlight"),
    ];
}

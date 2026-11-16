namespace LenovoLegionToolkit.Lib.Settings;

/// <summary>
/// Spectrum 键盘设置：持久化到 spectrum_keyboard.json，保存用户选择的键盘布局（用于按键映射显示）。
/// </summary>
public class SpectrumKeyboardSettings()
    : AbstractSettings<SpectrumKeyboardSettings.SpectrumKeyboardSettingsStore>("spectrum_keyboard.json")
{
    /// <summary>
    /// spectrum_keyboard.json 的数据结构。
    /// </summary>
    public class SpectrumKeyboardSettingsStore
    {
        /// <summary>
        /// 键盘布局；为空时使用系统默认布局。
        /// </summary>
        public KeyboardLayout? KeyboardLayout { get; set; }
    }
}

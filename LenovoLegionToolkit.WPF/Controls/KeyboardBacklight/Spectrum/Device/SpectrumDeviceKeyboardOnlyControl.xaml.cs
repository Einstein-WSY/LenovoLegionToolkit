using LenovoLegionToolkit.Lib;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.Spectrum.Device;

/// <summary>
/// 仅键盘 Spectrum 设备示意图。
/// </summary>
public partial class SpectrumDeviceKeyboardOnlyControl
{
    /// <summary>
    /// 初始化控件并加载对应的 XAML 视图。
    /// </summary>
    public SpectrumDeviceKeyboardOnlyControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 将键盘区域切换为指定的物理布局。
    /// </summary>
    /// <param name="keyboardLayout">键盘物理布局。</param>
    public void SetLayout(KeyboardLayout keyboardLayout)
    {
        _keyboard.SetLayout(keyboardLayout);
    }
}

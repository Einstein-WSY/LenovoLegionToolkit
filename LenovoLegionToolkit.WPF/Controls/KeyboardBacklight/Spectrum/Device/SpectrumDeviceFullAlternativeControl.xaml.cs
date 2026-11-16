using LenovoLegionToolkit.Lib;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.Spectrum.Device;

/// <summary>
/// 整机（替代版）Spectrum 设备示意图，包含键盘与机身灯区。
/// </summary>
public partial class SpectrumDeviceFullAlternativeControl
{
    /// <summary>
    /// 初始化控件并加载对应的 XAML 视图。
    /// </summary>
    public SpectrumDeviceFullAlternativeControl()
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

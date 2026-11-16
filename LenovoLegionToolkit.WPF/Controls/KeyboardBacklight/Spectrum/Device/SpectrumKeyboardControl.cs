using System.Windows.Controls;
using LenovoLegionToolkit.Lib;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.Spectrum.Device;

/// <summary>
/// 键盘示意图选择控件：根据物理键盘布局（ANSI/ISO/JIS）选用对应的按键排布视图。
/// </summary>
public class SpectrumKeyboardControl : UserControl
{
    /// <summary>ANSI 布局视图。</summary>
    private readonly SpectrumKeyboardANSIControl _ansi = new();
    /// <summary>ISO 布局视图。</summary>
    private readonly SpectrumKeyboardISOControl _iso = new();
    /// <summary>JIS 布局视图。</summary>
    private readonly SpectrumKeyboardJisControl _jis = new();

    /// <summary>承载当前布局视图的容器。</summary>
    private readonly StackPanel _stackPanel = new();

    /// <summary>
    /// 初始化控件并以纵向容器作为内容。
    /// </summary>
    public SpectrumKeyboardControl()
    {
        Content = _stackPanel;
    }

    /// <summary>
    /// 切换键盘布局：先移除全部候选视图，再按布局类型加入对应视图。
    /// </summary>
    /// <param name="keyboardLayout">键盘物理布局。</param>
    public void SetLayout(KeyboardLayout keyboardLayout)
    {
        _stackPanel.Children.Remove(_ansi);
        _stackPanel.Children.Remove(_iso);
        _stackPanel.Children.Remove(_jis);

        switch (keyboardLayout)
        {
            case KeyboardLayout.Ansi:
                _stackPanel.Children.Add(_ansi);
                break;
            case KeyboardLayout.Iso:
                _stackPanel.Children.Add(_iso);
                break;
            case KeyboardLayout.Jis:
                _stackPanel.Children.Add(_jis);
                break;
        }

        UpdateLayout();
    }
}

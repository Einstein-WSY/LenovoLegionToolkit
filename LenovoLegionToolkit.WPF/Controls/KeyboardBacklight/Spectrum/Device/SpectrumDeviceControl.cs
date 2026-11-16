using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.WPF.Extensions;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.Spectrum.Device;

/// <summary>
/// Spectrum 设备预览控件：按设备布局类型选用整机/键盘+前灯/仅键盘等示意图，
/// 并依据支持的按键集合控制各按键热点（<see cref="SpectrumZoneControl"/>）的可见性。
/// </summary>
public class SpectrumDeviceControl : UserControl
{
    /// <summary>整机布局（替代版）示意图。</summary>
    private readonly SpectrumDeviceFullAlternativeControl _fullAlternative = new();
    /// <summary>整机布局示意图。</summary>
    private readonly SpectrumDeviceFullControl _full = new();
    /// <summary>键盘加前灯布局示意图。</summary>
    private readonly SpectrumDeviceKeyboardAndFrontControl _keyboardAndFront = new();
    /// <summary>仅键盘布局示意图。</summary>
    private readonly SpectrumDeviceKeyboardOnlyControl _keyboardOnly = new();

    /// <summary>承载当前示意图的容器。</summary>
    private readonly StackPanel _stackPanel = new();

    /// <summary>
    /// 初始化控件并以纵向容器作为内容。
    /// </summary>
    public SpectrumDeviceControl()
    {
        Content = _stackPanel;
    }

    /// <summary>
    /// 切换设备示意图并标注可用按键：先移除全部候选视图，再按布局加入对应视图并应用键盘布局。
    /// </summary>
    /// <param name="spectrumLayout">设备灯效布局类型。</param>
    /// <param name="keyboardLayout">键盘物理布局（ANSI/ISO/JIS）。</param>
    /// <param name="keys">该设备支持的按键 HID 码集合，未包含的按键将被隐藏。</param>
    public void SetLayout(SpectrumLayout spectrumLayout, KeyboardLayout keyboardLayout, HashSet<ushort> keys)
    {
        // 切换布局前先清空旧视图，保证容器内只保留一个示意图。
        _stackPanel.Children.Remove(_fullAlternative);
        _stackPanel.Children.Remove(_full);
        _stackPanel.Children.Remove(_keyboardAndFront);
        _stackPanel.Children.Remove(_keyboardOnly);

        switch (spectrumLayout)
        {
            case SpectrumLayout.FullAlternative:
                _stackPanel.Children.Add(_fullAlternative);
                _fullAlternative.SetLayout(keyboardLayout);
                break;
            case SpectrumLayout.Full:
                _stackPanel.Children.Add(_full);
                _full.SetLayout(keyboardLayout);
                break;
            case SpectrumLayout.KeyboardAndFront:
                _stackPanel.Children.Add(_keyboardAndFront);
                _keyboardAndFront.SetLayout(keyboardLayout);
                break;
            case SpectrumLayout.KeyboardOnly:
                _stackPanel.Children.Add(_keyboardOnly);
                _keyboardOnly.SetLayout(keyboardLayout);
                break;
        }

        // 强制布局，确保新加入的按键控件已完成测量后再设置可见性。
        UpdateLayout();

        foreach (var button in GetButtons())
            button.Visibility = keys.Contains(button.KeyCode) ? Visibility.Visible : Visibility.Hidden;
    }

    /// <summary>
    /// 获取当前可见的全部按键热点。
    /// </summary>
    /// <returns>可见的按键控件序列。</returns>
    public IEnumerable<SpectrumZoneControl> GetVisibleButtons() =>
        GetButtons().Where(b => b.Visibility == Visibility.Visible);

    /// <summary>
    /// 获取视图中所有带有效按键码的热点控件。
    /// </summary>
    /// <returns>按键码大于 0 的按键控件序列。</returns>
    private IEnumerable<SpectrumZoneControl> GetButtons() =>
        this.GetVisibleChildrenOfType<SpectrumZoneControl>()
            .Where(b => b.KeyCode > 0);

    /// <summary>
    /// 获取当前可见的键盘按键热点（不含前灯等非键盘区域）。
    /// </summary>
    /// <returns>可见的键盘按键控件序列。</returns>
    public IEnumerable<SpectrumZoneControl> GetVisibleKeyboardButtons() =>
        GetKeyboardButtons().Where(b => b.Visibility == Visibility.Visible);

    /// <summary>
    /// 获取键盘按键热点：HID 键盘使用码范围通常小于 0x100，据此排除前灯等扩展区域。
    /// </summary>
    /// <returns>键盘区域的按键控件序列。</returns>
    private IEnumerable<SpectrumZoneControl> GetKeyboardButtons() =>
        this.GetVisibleChildrenOfType<SpectrumZoneControl>()
            .Where(b => b.KeyCode is > 0 and < 0x100);
}

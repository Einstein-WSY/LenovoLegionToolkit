using System;
using System.Windows.Input;
using System.Windows.Media;

namespace LenovoLegionToolkit.WPF.Controls;

/// <summary>
/// 多颜色选择器中的单个颜色项，转发底层 <see cref="ColorPickerControl"/> 的事件并支持删除。
/// </summary>
public partial class MultiColorPickerItemControl
{
    /// <summary>选中颜色。</summary>
    public Color SelectedColor
    {
        get => _picker.SelectedColor;
        set => _picker.SelectedColor = value;
    }

    /// <summary>颜色连续变化事件（转发自内部取色器）。</summary>
    public event EventHandler? ColorChangedContinuous
    {
        add => _picker.ColorChangedContinuous += value;
        remove => _picker.ColorChangedContinuous -= value;
    }

    /// <summary>颜色变化结束事件（转发自内部取色器）。</summary>
    public event EventHandler? ColorChangedDelayed
    {
        add => _picker.ColorChangedDelayed += value;
        remove => _picker.ColorChangedDelayed -= value;
    }

    /// <summary>删除项请求事件。</summary>
    public event EventHandler<MouseButtonEventArgs>? Delete;

    public MultiColorPickerItemControl() => InitializeComponent();

    // 将删除按钮点击转发为 Delete 事件，由父控件决定是否移除本项
    private void Delete_Click(object sender, MouseButtonEventArgs e) => Delete?.Invoke(this, e);
}

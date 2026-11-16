using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace LenovoLegionToolkit.WPF.Controls;

/// <summary>
/// 颜色选择控件，提供取色面板、RGB 数值输入与十六进制输入三种方式，
/// 并在颜色变化时触发连续与延迟两类事件。
/// </summary>
public partial class ColorPickerControl
{
    /// <summary>匹配合法的十六进制颜色文本（如 #RRGGBB）。</summary>
    [GeneratedRegex("^#(?:[0-9A-F]{3}){2}$", RegexOptions.IgnoreCase, "en-DK")]
    private static partial Regex HexTextRegex();

    // 编辑过程中抑制事件回环，且确保所有子控件已实例化
    private bool CanHandleEvent => !_isEditing && _colorPicker is not null && _redNumberBox is not null && _greenNumberBox is not null && _blueNumberBox is not null && _hexTextBox is not null;

    private bool _isEditing;

    /// <summary>当前选中的颜色。</summary>
    public Color SelectedColor
    {
        get => _colorPicker.SelectedColor;
        set => _colorPicker.SelectedColor = value;
    }

    /// <summary>颜色连续变化时触发（拖动取色过程中）。</summary>
    public event EventHandler? ColorChangedContinuous;
    /// <summary>颜色变化结束（或输入完成）时触发。</summary>
    public event EventHandler? ColorChangedDelayed;

    public ColorPickerControl()
    {
        InitializeComponent();

        // 默认选中青色
        SelectedColor = Colors.Aqua;
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {
        // 打开取色弹出层并标记事件已处理
        _popup.IsOpen = true;
        e.Handled = true;
    }

    private void ColorPicker_MouseUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ColorChangedDelayed?.Invoke(this, EventArgs.Empty);
    }

    private void ColorPicker_MouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    // 取色面板颜色变化：同步更新按钮、RGB 输入框与十六进制框
    private void ColorPicker_ColorChanged(object sender, RoutedEventArgs e)
    {
        if (!CanHandleEvent)
            return;

        _isEditing = true;

        var color = _colorPicker.SelectedColor;

        _button.Background = new SolidColorBrush(color);

        _redNumberBox.Text = color.R.ToString();
        _greenNumberBox.Text = color.G.ToString();
        _blueNumberBox.Text = color.B.ToString();

        _hexTextBox.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";

        // 拖动取色时持续触发连续事件
        ColorChangedContinuous?.Invoke(this, EventArgs.Empty);

        _isEditing = false;
    }


    // RGB 输入框变化：解析并应用颜色
    private void NumberBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!CanHandleEvent)
            return;

        _isEditing = true;

        var r = ToByte(_redNumberBox.Text);
        var g = ToByte(_greenNumberBox.Text);
        var b = ToByte(_blueNumberBox.Text);
        var color = Color.FromRgb(r, g, b);

        _button.Background = new SolidColorBrush(color);

        _hexTextBox.Text = $"#{r:X2}{g:X2}{b:X2}";

        // 鼠标未按下时才回写取色器并触发延迟事件，避免拖动时抖动
        if (Mouse.LeftButton != MouseButtonState.Pressed && Mouse.RightButton != MouseButtonState.Pressed)
        {
            _colorPicker.SelectedColor = color;

            ColorChangedDelayed?.Invoke(this, EventArgs.Empty);
        }

        _isEditing = false;
    }

    // 十六进制输入框变化：校验格式后解析并应用颜色
    private void HexTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!CanHandleEvent)
            return;

        if (!HexTextRegex().Match(_hexTextBox.Text).Success)
            return;

        _isEditing = true;

        try
        {
            var c = ColorTranslator.FromHtml(_hexTextBox.Text);
            var color = Color.FromRgb(c.R, c.G, c.B);

            _button.Background = new SolidColorBrush(color);

            _redNumberBox.Text = color.R.ToString();
            _greenNumberBox.Text = color.G.ToString();
            _blueNumberBox.Text = color.B.ToString();

            if (Mouse.LeftButton != MouseButtonState.Pressed && Mouse.RightButton != MouseButtonState.Pressed)
            {
                _colorPicker.SelectedColor = color;

                ColorChangedDelayed?.Invoke(this, EventArgs.Empty);
            }
        }
        catch {  /* Ignored. */ }

        _isEditing = false;
    }

    private void OK_Click(object sender, RoutedEventArgs e) => _popup.IsOpen = false;

    /// <summary>将文本安全解析为 0-255 的字节值，非法输入返回 0。</summary>
    /// <param name="s">输入文本。</param>
    /// <returns>颜色分量字节。</returns>
    private static byte ToByte(string s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return 0;

        if (!int.TryParse(s, out var userInput))
            return 0;

        return (byte)Math.Clamp(userInput, 0, 255);
    }
}

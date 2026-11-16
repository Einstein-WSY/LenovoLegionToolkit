using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace LenovoLegionToolkit.WPF.Controls;

/// <summary>
/// 多颜色选择控件，允许添加最多若干个颜色选择项，并聚合各子项的颜色变化事件。
/// </summary>
public partial class MultiColorPickerControl
{
    // 最多允许添加的颜色数量
    private const int MAX_ITEMS = 3;

    /// <summary>
    /// 已选颜色集合；赋值时会按顺序重建子选择项。
    /// </summary>
    public Color[] SelectedColors
    {
        get => _buttons.Children.OfType<MultiColorPickerItemControl>()
            .Select(c => c.SelectedColor)
            .ToArray();
        set
        {
            _buttons.Children.Clear();
            foreach (var color in value.Take(MAX_ITEMS))
            {
                var picker = CreateColorPicker();
                picker.SelectedColor = color;
                _buttons.Children.Add(picker);
            }

            Update();
        }
    }

    /// <summary>任一颜色连续变化时触发。</summary>
    public event EventHandler? ColorsChangedContinuous;
    /// <summary>任一颜色变化结束时触发。</summary>
    public event EventHandler? ColorsChangedDelayed;

    public MultiColorPickerControl() => InitializeComponent();

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedColors.Length >= MAX_ITEMS)
            return;

        var picker = CreateColorPicker();
        _buttons.Children.Add(picker);
        Update();
    }

    // 创建一个子颜色选择项，并把其事件与删除行为桥接到本控件
    private MultiColorPickerItemControl CreateColorPicker()
    {
        var picker = new MultiColorPickerItemControl
        {
            Margin = new(0, 0, 8, 0),
        };
        picker.ColorChangedContinuous += (_, _) => ColorsChangedContinuous?.Invoke(this, EventArgs.Empty);
        picker.ColorChangedDelayed += (_, _) => ColorsChangedDelayed?.Invoke(this, EventArgs.Empty);
        picker.Delete += (_, e) =>
        {
            _buttons.Children.Remove(picker);
            Update();
            e.Handled = true;
        };
        return picker;
    }

    // 根据当前数量控制“添加”按钮可用性，并广播整体颜色变更
    private void Update()
    {
        _addButton.IsEnabled = SelectedColors.Length < MAX_ITEMS;

        ColorsChangedContinuous?.Invoke(this, EventArgs.Empty);
        ColorsChangedDelayed?.Invoke(this, EventArgs.Empty);
    }
}

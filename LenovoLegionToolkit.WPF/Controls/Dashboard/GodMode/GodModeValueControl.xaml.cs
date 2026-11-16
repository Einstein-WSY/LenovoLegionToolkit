using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.WPF.Extensions;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard.GodMode;

/// <summary>
/// 极客模式下的单个可调参数卡片控件：根据参数定义在滑杆（连续取值）与下拉框（离散档位）之间切换，
/// 负责取/设值时的范围夹取、步进取整与就近匹配，并提供恢复默认值功能。
/// </summary>
public partial class GodModeValueControl
{
    /// <summary>参数的默认值，仅当参数定义提供默认值时才可用于“恢复默认”。</summary>
    private int? _defaultValue;

    /// <summary>
    /// 参数标题；设置时同步更新滑杆、下拉框与重置按钮的无障碍名称。
    /// </summary>
    public string Title
    {
        get => _cardControlHeader.Title;
        set
        {
            _cardControlHeader.Title = value;

            AutomationProperties.SetName(_slider, _cardControlHeader.Title);
            AutomationProperties.SetName(_comboBox, _cardControlHeader.Title);
            AutomationProperties.SetName(_resetToDefaultButton, _cardControlHeader.Title);
        }
    }

    /// <summary>
    /// 参数说明文字，显示在卡片副标题处。
    /// </summary>
    public string Description
    {
        get => _cardControlHeader.Subtitle;
        set => _cardControlHeader.Subtitle = value;
    }

    /// <summary>数值单位后缀（如 MHz、mV），用于拼装显示文本。</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>
    /// 获取或设置当前参数值；读取时按当前可见的输入端（滑杆或下拉框）取值。
    /// </summary>
    public int Value
    {
        get
        {
            if (_slider.Visibility == Visibility.Visible)
                return (int)_slider.Value;

            if (_comboBox.Visibility == Visibility.Visible && _comboBox.TryGetSelectedItem(out int value))
                return value;

            throw new InvalidOperationException("Unable to get Value");
        }
        set
        {
            if (_slider.Visibility == Visibility.Visible)
            {
                double newValue = value;

                // 越界值回退到默认值，避免硬件拒绝非法参数。
                if (_defaultValue.HasValue && (newValue < _slider.Minimum || newValue > _slider.Maximum))
                    newValue = _defaultValue.Value;

                // 按步进频率就近取整，再夹取到量程范围，确保写入值合法。
                newValue = Math.Clamp(MathExtensions.RoundNearest((int)newValue, (int)_slider.TickFrequency), _slider.Minimum, _slider.Maximum);
                _slider.Value = newValue;
                return;
            }

            if (_comboBox.Visibility == Visibility.Visible)
            {
                var newValue = value;
                var items = _comboBox.GetItems<int>().ToArray();
                // 离散档位中不存在该值时，选择数值最接近的一档。
                if (!items.Contains(newValue))
                {
                    var valueTemp = newValue;
                    newValue = items.MinBy(v => Math.Abs((long)v - valueTemp));
                }

                _comboBox.SelectItem(newValue);
                return;
            }

            throw new InvalidOperationException("Unable to set Value");
        }
    }

    /// <summary>
    /// 值变化事件，直接转发自滑杆的 ValueChanged（下拉框选择不会触发此事件）。
    /// </summary>
    public event RoutedPropertyChangedEventHandler<double> ValueChanged
    {
        add => _slider.ValueChanged += value;
        remove => _slider.ValueChanged -= value;
    }

    /// <summary>
    /// 初始化控件并加载对应的 XAML 视图。
    /// </summary>
    public GodModeValueControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 依据参数定义配置输入控件：有离散档位时使用下拉框，有正步进时使用滑杆，否则隐藏卡片。
    /// </summary>
    /// <param name="stepperValue">参数定义；为空表示该参数不可用。</param>
    public void Set(StepperValue? stepperValue)
    {
        if (!stepperValue.HasValue)
        {
            Visibility = Visibility.Collapsed;
            return;
        }

        var value = stepperValue.Value;

        // 存在离散档位：改用下拉框，并复位滑杆状态。
        if (value.Steps.Length > 0)
        {
            _slider.Visibility = Visibility.Collapsed;
            _sliderLabel.Visibility = Visibility.Collapsed;
            _comboBox.Visibility = Visibility.Visible;

            _slider.Minimum = 0;
            _slider.Maximum = 0;
            _slider.TickFrequency = 0;
            _slider.Value = 0;

            _sliderLabel.ContentStringFormat = null;

            _comboBox.SetItems(value.Steps, value.Value, v => $"{v} {Unit}");

            _defaultValue = value.DefaultValue;
            _resetToDefaultButton.Visibility = _defaultValue.HasValue ? Visibility.Visible : Visibility.Collapsed;

            Visibility = Visibility.Visible;
            return;
        }

        // 连续取值：使用滑杆，按最小/最大值与步进设置量程。
        if (value.Step > 0)
        {
            _slider.Visibility = Visibility.Visible;
            _sliderLabel.Visibility = Visibility.Visible;
            _comboBox.Visibility = Visibility.Collapsed;

            _slider.Minimum = value.Min;
            _slider.Maximum = value.Max;
            _slider.TickFrequency = value.Step;
            _slider.Value = value.Value;

            _sliderLabel.ContentStringFormat = $"{0} {Unit}";

            _comboBox.Items.Clear();
            _comboBox.SelectedItem = null;

            _defaultValue = value.DefaultValue;
            _resetToDefaultButton.Visibility = _defaultValue.HasValue ? Visibility.Visible : Visibility.Collapsed;

            Visibility = Visibility.Visible;
            return;
        }

        // 既无档位也无步进，说明该参数不可调整，隐藏卡片。
        Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// “恢复默认”按钮点击：按当前可见的输入端把值重置为参数默认值。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void ResetToDefaultButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_defaultValue.HasValue)
            return;

        if (_slider.Visibility == Visibility.Visible)
            _slider.Value = _defaultValue.Value;

        if (_comboBox.Visibility == Visibility.Visible)
            _comboBox.SelectItem(_defaultValue.Value);
    }
}

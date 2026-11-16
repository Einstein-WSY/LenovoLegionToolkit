using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LenovoLegionToolkit.WPF.Extensions;
using Wpf.Ui.Common;
using CardControl = LenovoLegionToolkit.WPF.Controls.Custom.CardControl;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.RGB;

/// <summary>
/// RGB 键盘背光设置卡片的通用基类：提供卡片图标/标题/副标题与一个下拉框，
/// 选择变化时通过 <see cref="OnChanged"/> 通知外部，禁用时会清空选项以阻止交互。
/// </summary>
/// <typeparam name="T">下拉框承载的选项类型。</typeparam>
public abstract class AbstractComboBoxRGBKeyboardCardControl<T> : UserControl
{
    /// <summary>卡片容器，承载图标与头部。</summary>
    private readonly CardControl _cardControl = new();

    /// <summary>卡片头部，承载标题、副标题与下拉框。</summary>
    private readonly CardHeaderControl _cardHeaderControl = new();

    /// <summary>选项下拉框。</summary>
    private readonly ComboBox _comboBox = new();

    /// <summary>
    /// 卡片图标。
    /// </summary>
    public SymbolRegular Icon
    {
        get => _cardControl.Icon;
        set => _cardControl.Icon = value;
    }

    /// <summary>
    /// 卡片标题；设置时同步更新下拉框的无障碍名称。
    /// </summary>
    public string Title
    {
        get => _cardHeaderControl.Title;
        set
        {
            _cardHeaderControl.Title = value;

            AutomationProperties.SetName(_comboBox, value);
        }
    }

    /// <summary>
    /// 卡片副标题（说明文字）。
    /// </summary>
    public string Subtitle
    {
        get => _cardHeaderControl.Subtitle;
        set => _cardHeaderControl.Subtitle = value;
    }

    /// <summary>
    /// 当前选中的选项；无有效选择时返回默认值。
    /// </summary>
    public T? SelectedItem
    {
        get
        {
            if (_comboBox.TryGetSelectedItem(out T? item))
                return item;
            return default;
        }
    }

    /// <summary>下拉框选择发生变化时触发。</summary>
    public event EventHandler? OnChanged;

    /// <summary>
    /// 初始化控件并构建卡片界面。
    /// </summary>
    public AbstractComboBoxRGBKeyboardCardControl() => InitializeComponent();

    /// <summary>
    /// 构建卡片界面：把下拉框放入头部附加区，并订阅选择变化与启用状态变化事件。
    /// </summary>
    private void InitializeComponent()
    {
        IsEnabledChanged += CardControl_IsEnabledChanged;

        _comboBox.SelectionChanged += ComboBox_SelectionChanged;
        _comboBox.MinWidth = 150;

        _cardHeaderControl.Accessory = _comboBox;
        _cardControl.Header = _cardHeaderControl;
        _cardControl.Margin = new(0, 0, 0, 8);

        Content = _cardControl;
    }

    /// <summary>
    /// 启用状态变化回调：控件被禁用时清空下拉选项，避免用户继续操作。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">依赖属性变化参数。</param>
    private void CardControl_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsEnabled)
            return;

        // 清空选项期间临时退订，避免触发 SelectionChanged 导致误上报变更。
        _comboBox.SelectionChanged -= ComboBox_SelectionChanged;
        _comboBox.ClearItems();
        _comboBox.SelectionChanged += ComboBox_SelectionChanged;
    }

    /// <summary>
    /// 下拉框选择变化回调，转发为 <see cref="OnChanged"/> 事件。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">选择变化参数。</param>
    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => OnChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// 批量设置下拉选项并选中指定项；填充期间临时退订事件，避免触发无谓的变更通知。
    /// </summary>
    /// <param name="items">全部可选项。</param>
    /// <param name="selectedItem">默认选中项。</param>
    /// <param name="displayValueConverter">可选的选项显示文本转换器。</param>
    public void SetItems(IEnumerable<T> items, T selectedItem, Func<T, string>? displayValueConverter)
    {
        _comboBox.SelectionChanged -= ComboBox_SelectionChanged;
        _comboBox.SetItems(items, selectedItem, displayValueConverter);
        _comboBox.SelectionChanged += ComboBox_SelectionChanged;
    }
}

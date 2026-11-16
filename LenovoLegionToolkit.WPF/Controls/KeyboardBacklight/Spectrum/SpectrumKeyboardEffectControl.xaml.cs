using System;
using System.Windows;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.WPF.Resources;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.Spectrum;

/// <summary>
/// Spectrum 灯效列表中的单个灯效卡片：显示灯效名称与作用区域说明，并提供点击、编辑、删除操作。
/// </summary>
public partial class SpectrumKeyboardEffectControl
{
    /// <summary>该卡片对应的灯效配置。</summary>
    public new SpectrumKeyboardBacklightEffect Effect { get; }

    /// <summary>点击卡片主体（切换为当前灯效）时触发。</summary>
    public event EventHandler? Click;
    /// <summary>点击编辑按钮时触发。</summary>
    public event EventHandler? Edit;
    /// <summary>点击删除按钮时触发。</summary>
    public event EventHandler? Delete;

    /// <summary>
    /// 使用灯效配置初始化控件：设置标题为灯效名称，副标题按作用范围生成说明。
    /// </summary>
    /// <param name="effect">要呈现的灯效配置。</param>
    public SpectrumKeyboardEffectControl(SpectrumKeyboardBacklightEffect effect)
    {
        Effect = effect;

        InitializeComponent();

        _cardHeaderControl.Title = effect.Type.GetDisplayName();

        // 全体灯效与按键级灯效的说明文案不同，后者需带按键数量。
        var subtitle = string.Empty;
        if (effect.Type.IsAllLightsEffect())
            subtitle += Resource.SpectrumKeyboardEffectControl_Description_AllZones;
        else
            subtitle += string.Format(Resource.SpectrumKeyboardEffectControl_Description_Zones, effect.Keys.Length);
        _cardHeaderControl.Subtitle = subtitle;
    }

    /// <summary>
    /// 卡片主体点击：转发为 <see cref="Click"/> 事件，并标记事件已处理避免冒泡重复触发。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
    {
        Click?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    /// <summary>
    /// 编辑按钮点击：转发为 <see cref="Edit"/> 事件，并标记事件已处理。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        Edit?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    /// <summary>
    /// 删除按钮点击：转发为 <see cref="Delete"/> 事件，并标记事件已处理。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        Delete?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}

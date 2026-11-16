using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Animation;
using Microsoft.Xaml.Behaviors;

namespace LenovoLegionToolkit.WPF.Behaviors;

/// <summary>
/// 为 <see cref="ProgressBar"/> 的数值变化添加平滑动画的 WPF 附加行为。
/// 在 XAML 中通过 <c>i:Interaction.Behaviors</c> 附加到进度条即可生效。
/// </summary>
public class ProgressBarAnimateBehavior : Behavior<ProgressBar>
{
    // 防止动画触发 ValueChanged 后再次进入造成重入
    private bool _isAnimating;

    /// <summary>附加行为时订阅进度条的数值变化事件。</summary>
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.ValueChanged += ProgressBar_ValueChanged;
    }

    /// <summary>移除行为时取消订阅，避免事件泄漏。</summary>
    protected override void OnDetaching()
    {
        base.OnDetaching();
        AssociatedObject.ValueChanged -= ProgressBar_ValueChanged;
    }

    private void ProgressBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not ProgressBar progressBar)
            return;

        if (_isAnimating)
            return;

        _isAnimating = true;

        // 从旧值过渡到新值，250ms 动画结束后停止保持（FillBehavior.Stop）
        var doubleAnimation = new DoubleAnimation(e.OldValue,
            e.NewValue,
            new Duration(TimeSpan.FromMilliseconds(250)),
            FillBehavior.Stop);
        doubleAnimation.Completed += Completed;

        // 对 RangeBase.ValueProperty 做动画，而非直接赋值，从而产生平滑过渡
        progressBar.BeginAnimation(RangeBase.ValueProperty, doubleAnimation);

        // 标记事件已处理，避免默认的即时跳变
        e.Handled = true;
    }

    private void Completed(object? sender, EventArgs e) => _isAnimating = false;
}

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Controls;

/// <summary>
/// 带加载状态的内容容器：加载中显示进度环，加载完成后展示实际内容。
/// </summary>
public class LoadableControl : UserControl
{
    private readonly ContentPresenter _contentPresenter = new();

    // 默认使用不确定进度的旋转环
    private readonly ProgressRing _progressRing = new()
    {
        IsIndeterminate = true,
        VerticalAlignment = VerticalAlignment.Top,
        HorizontalAlignment = HorizontalAlignment.Center,
        Width = 48,
        Height = 48,
    };

    private bool _isLoading = true;

    /// <summary>是否处于加载状态。</summary>
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            UpdateLoadingState();
        }
    }

    /// <summary>进度环是否为不确定进度模式。</summary>
    public bool IsIndeterminate
    {
        get => _progressRing.IsIndeterminate;
        set => _progressRing.IsIndeterminate = value;
    }

    /// <summary>确定进度模式下的进度值。</summary>
    public double Progress
    {
        get => _progressRing.Progress;
        set => _progressRing.Progress = value;
    }

    /// <summary>进度环宽度。</summary>
    public double IndicatorWidth
    {
        get => _progressRing.Width;
        set => _progressRing.Width = value;
    }

    /// <summary>进度环高度。</summary>
    public double IndicatorHeight
    {
        get => _progressRing.Height;
        set => _progressRing.Height = value;
    }

    /// <summary>进度环水平对齐方式。</summary>
    public HorizontalAlignment IndicatorHorizontalAlignment
    {
        get => _progressRing.HorizontalAlignment;
        set => _progressRing.HorizontalAlignment = value;
    }

    /// <summary>进度环垂直对齐方式。</summary>
    public VerticalAlignment IndicatorVerticalAlignment
    {
        get => _progressRing.VerticalAlignment;
        set => _progressRing.VerticalAlignment = value;
    }

    /// <summary>进度环外边距。</summary>
    public Thickness IndicatorMargin
    {
        get => _progressRing.Margin;
        set => _progressRing.Margin = value;
    }

    /// <summary>加载期间内容区的可见性。</summary>
    public Visibility ContentVisibilityWhileLoading { get; set; } = Visibility.Hidden;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        // 将原有内容移入 ContentPresenter，再用 Grid 叠加进度环
        _contentPresenter.Content = Content;

        // 通过旋转与镜像把进度环起点调整到顶部
        _progressRing.RenderTransformOrigin = new(0.5, 0.5);
        _progressRing.RenderTransform = new TransformGroup
        {
            Children =
            {
                new RotateTransform(-90),
                new ScaleTransform(-1, 1),
            }
        };

        var grid = new Grid();
        grid.Children.Add(_contentPresenter);
        grid.Children.Add(_progressRing);

        UpdateLoadingState();

        Content = grid;
    }

    // 根据加载状态切换内容与进度环的可见性
    private void UpdateLoadingState()
    {
        _contentPresenter.Visibility = IsLoading ? ContentVisibilityWhileLoading : Visibility.Visible;
        _progressRing.Visibility = IsLoading ? Visibility.Visible : Visibility.Hidden;
    }
}

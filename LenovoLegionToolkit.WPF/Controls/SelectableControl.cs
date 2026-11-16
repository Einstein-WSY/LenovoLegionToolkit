using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LenovoLegionToolkit.WPF.Controls;

/// <summary>
/// 支持鼠标框选的内容控件：拖动绘制选择矩形，并通过 <see cref="Selected"/> 事件
/// 提供判断元素中心是否落在选区内的方法。
/// </summary>
public class SelectableControl : UserControl
{
    /// <summary>
    /// 框选完成事件参数，携带一个用于判断元素中心是否位于选区内的委托。
    /// </summary>
    /// <param name="containsCenter">判断元素中心是否在选区内的委托。</param>
    public class SelectedEventArgs(Func<FrameworkElement, bool> containsCenter) : EventArgs
    {
        /// <summary>判断指定元素中心是否落在选区内。</summary>
        public Func<FrameworkElement, bool> ContainsCenter { get; } = containsCenter;
    }

    // 透明背景确保可以接收鼠标事件
    private readonly Grid _grid = new()
    {
        Background = new SolidColorBrush(Colors.Transparent)
    };

    private readonly ContentPresenter _contentPresenter = new();

    private readonly Canvas _canvas = new();

    // 框选时显示的矩形，默认隐藏
    private readonly Rectangle _selection = new()
    {
        StrokeThickness = 2,
        Visibility = Visibility.Collapsed
    };

    private bool _mouseDown;
    private Point _mouseDownPosition;

    /// <summary>选择框描边画刷。</summary>
    public Brush Stroke
    {
        get => _selection.Stroke;
        set => _selection.Stroke = value;
    }

    /// <summary>选择框填充画刷。</summary>
    public Brush Fill
    {
        get => _selection.Fill;
        set => _selection.Fill = value;
    }

    /// <summary>框选完成时触发。</summary>
    public event EventHandler<SelectedEventArgs>? Selected;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        // 将原内容放入 ContentPresenter，再叠加用于绘制选区的 Canvas
        _contentPresenter.Content = Content;

        _canvas.Children.Add(_selection);

        _grid.Children.Add(_contentPresenter);
        _grid.Children.Add(_canvas);

        _grid.MouseDown += Grid_OnMouseDown;
        _grid.MouseMove += Grid_OnMouseMove;
        _grid.MouseUp += Grid_OnMouseUp;

        Content = _grid;
    }

    private void Grid_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _mouseDown = true;

        // 捕获鼠标，保证拖出控件范围后仍能收到移动与抬起事件
        _grid.CaptureMouse();
        _mouseDownPosition = e.GetPosition(_grid);

        Canvas.SetLeft(_selection, _mouseDownPosition.X);
        Canvas.SetTop(_selection, _mouseDownPosition.Y);

        _selection.Width = 0;
        _selection.Height = 0;

        _selection.Visibility = Visibility.Visible;
    }

    private void Grid_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_mouseDown)
            return;

        var mousePosition = e.GetPosition(_grid);

        // 根据拖动方向调整矩形左上角与宽高，支持四个方向拖拽
        if (_mouseDownPosition.X < mousePosition.X)
        {
            Canvas.SetLeft(_selection, _mouseDownPosition.X);
            _selection.Width = mousePosition.X - _mouseDownPosition.X;
        }
        else
        {
            Canvas.SetLeft(_selection, mousePosition.X);
            _selection.Width = _mouseDownPosition.X - mousePosition.X;
        }

        if (_mouseDownPosition.Y < mousePosition.Y)
        {
            Canvas.SetTop(_selection, _mouseDownPosition.Y);
            _selection.Height = mousePosition.Y - _mouseDownPosition.Y;
        }
        else
        {
            Canvas.SetTop(_selection, mousePosition.Y);
            _selection.Height = _mouseDownPosition.Y - mousePosition.Y;
        }
    }

    private void Grid_OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_mouseDown)
            return;

        _mouseDown = false;
        _grid.ReleaseMouseCapture();
        _selection.Visibility = Visibility.Collapsed;

        var mouseUpPosition = e.GetPosition(_grid);

        var minX = Math.Min(_mouseDownPosition.X, mouseUpPosition.X);
        var minY = Math.Min(_mouseDownPosition.Y, mouseUpPosition.Y);
        var maxX = Math.Max(_mouseDownPosition.X, mouseUpPosition.X);
        var maxY = Math.Max(_mouseDownPosition.Y, mouseUpPosition.Y);

        var rectangle = new Rect(minX, minY, maxX - minX, maxY - minY);

        // 判断元素中心点是否落在框选矩形内（需转换到 Grid 坐标系）
        bool ContainsCenter(FrameworkElement element)
        {
            var elementRect = element.TransformToVisual(_grid)
                .TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));

            var elementCenterX = elementRect.X + elementRect.Width / 2;
            var elementCenterY = elementRect.Y + elementRect.Height / 2;

            return rectangle.Contains(elementCenterX, elementCenterY);
        }

        Selected?.Invoke(this, new SelectedEventArgs(ContainsCenter));
    }
}

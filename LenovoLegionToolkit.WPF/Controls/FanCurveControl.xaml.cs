using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.WPF.Resources;

namespace LenovoLegionToolkit.WPF.Controls;

/// <summary>
/// 风扇曲线编辑控件：以可拖动的垂直滑块表示各温度点的转速，并实时绘制曲线与填充区域。
/// </summary>
public partial class FanCurveControl
{
    private readonly List<Slider> _sliders = [];
    // 滑块悬停时显示温度与转速信息的自定义提示
    private readonly InfoTooltip _customToolTip = new();

    private FanTableData[]? _tableData;
    private FanTable? _minimumFanTable;

    public FanCurveControl()
    {
        InitializeComponent();

        MouseLeave += FanCurveControl_MouseLeave;
    }

    // 鼠标离开控件时关闭提示，避免提示残留
    private void FanCurveControl_MouseLeave(object sender, MouseEventArgs e)
    {
        _customToolTip.IsOpen = false;
    }

    /// <summary>布局完成后重新绘制曲线，确保滑块尺寸已确定。</summary>
    /// <param name="arrangeBounds">可用布局尺寸。</param>
    /// <returns>实际使用尺寸。</returns>
    protected override Size ArrangeOverride(Size arrangeBounds)
    {
        var size = base.ArrangeOverride(arrangeBounds);
        DrawGraph();
        return size;
    }

    /// <summary>
    /// 根据风扇表数据重建滑块，并记录最小转速表用于限制下限。
    /// </summary>
    /// <param name="fanTableInfo">风扇表信息。</param>
    /// <param name="minimumFanTable">最小风扇转速表。</param>
    public void SetFanTableInfo(FanTableInfo fanTableInfo, FanTable minimumFanTable)
    {
        _sliders.Clear();
        _slidersGrid.Children.Clear();

        var tableValues = fanTableInfo.Table.GetTable();

        // 为每个温度点创建一个 0-10 档的垂直滑块
        for (var i = 0; i < tableValues.Length; i++)
        {
            var slider = GenerateSlider(i, 0, 10);
            slider.Value = tableValues[i];
            _sliders.Add(slider);
            _slidersGrid.Children.Add(slider);
        }

        _tableData = fanTableInfo.Data;
        _minimumFanTable = minimumFanTable;

        // 延迟到渲染阶段重绘，保证布局与模板已就绪
        Dispatcher.InvokeAsync(DrawGraph, DispatcherPriority.Render);
    }

    /// <summary>
    /// 读取当前滑块值并构造风扇表；未设置数据时返回 <c>null</c>。
    /// </summary>
    /// <returns>当前风扇表信息。</returns>
    public FanTableInfo? GetFanTableInfo()
    {
        if (_tableData is null)
            return null;

        var fanTable = _sliders.Select(s => (ushort)s.Value).ToArray();
        return new(_tableData, new FanTable(fanTable));
    }

    private Slider GenerateSlider(int index, int minimum, int maximum)
    {
        var slider = new Slider
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Orientation = Orientation.Vertical,
            // 吸附到整数刻度，保证取值连续且可预测
            IsSnapToTickEnabled = true,
            TickFrequency = 1,
            Maximum = maximum,
            Minimum = minimum,
            Tag = index,
        };

        slider.MouseMove += Slider_MouseMove;
        slider.ValueChanged += Slider_OnValueChanged;

        // 第 0 列留给坐标轴，滑块从第 1 列开始
        Grid.SetColumn(slider, index + 1);

        return slider;
    }

    // 鼠标悬停在滑块拇指针上时，显示对应温度点的风扇转速提示
    private void Slider_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not Slider slider)
            return;

        // 从控件模板中查找 Track 部件以定位拇指针
        if (slider.Template.FindName("PART_Track", slider) is not Track track)
            return;

        if (!track.Thumb.IsMouseOver || _tableData is null)
        {
            _customToolTip.IsOpen = false;
            return;
        }

        _customToolTip.Update(_tableData, (int)slider.Tag, (int)slider.Value - 1);

        _customToolTip.Placement = PlacementMode.Custom;
        _customToolTip.PlacementTarget = track.Thumb;
        _customToolTip.CustomPopupPlacementCallback = ToolTipCustomPopupPlacementCallback;

        // 轻微偏移两次是为了在偏移变化时触发提示重新定位
        _customToolTip.HorizontalOffset += -0.1;
        _customToolTip.HorizontalOffset += +0.1;

        _customToolTip.IsOpen = true;
    }

    // 滑块值变化时限制最小值并校验曲线单调性，随后重绘
    private void Slider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // 初始化阶段（滑块未全部创建）忽略变化
        if (_sliders.Count < 10)
            return;

        if (sender is not Slider currentSlider)
            return;

        // 仅响应用户交互引起的变化，避免程序赋值形成回环
        if (currentSlider is { IsKeyboardFocusWithin: false, IsMouseCaptureWithin: false })
            return;

        if (_minimumFanTable.HasValue)
        {
            var index = (int)currentSlider.Tag;
            var minimum = _minimumFanTable.Value.GetTable();

            if (currentSlider.Value < minimum[index])
            {
                currentSlider.Value = minimum[index];
                return;
            }
        }

        VerifyValues(currentSlider);
        DrawGraph();
    }

    // 自定义提示位置：水平居中于拇指针，置于其上方
    private static CustomPopupPlacement[] ToolTipCustomPopupPlacementCallback(Size size, Size targetSize, Point _)
    {
        return
        [
            new(new((targetSize.Width - size.Width) * 0.5, -targetSize.Height -size.Height + 8), PopupPrimaryAxis.Vertical)
        ];
    }

    // 保证风扇曲线单调：左侧不大于当前值，右侧不小于当前值
    private void VerifyValues(Slider currentSlider)
    {
        var currentIndex = _sliders.IndexOf(currentSlider);
        if (currentIndex < 0)
            return;

        var currentValue = currentSlider.Value;
        var slidersBefore = _sliders.Take(currentIndex);
        var slidersAfter = _sliders.Skip(currentIndex + 1);

        foreach (var slider in slidersBefore)
        {
            if (slider.Value > currentValue)
                slider.Value = currentValue;
        }

        foreach (var slider in slidersAfter)
        {
            if (slider.Value < currentValue)
                slider.Value = currentValue;
        }
    }

    // 根据滑块位置绘制折线与下方填充区域
    private void DrawGraph()
    {
        var color = Application.Current.Resources["ControlFillColorDefaultBrush"] as SolidColorBrush;

        _canvas.Children.Clear();

        var points = _sliders
            .Select(GetThumbLocation)
            .Select(p => new Point(p.X, p.Y))
            .ToArray();

        if (points.IsEmpty())
            return;

        // Line

        var pathSegmentCollection = new PathSegmentCollection();
        foreach (var point in points.Skip(1))
            pathSegmentCollection.Add(new LineSegment { Point = point });
        var pathFigure = new PathFigure { StartPoint = points[0], Segments = pathSegmentCollection };

        var path = new Path
        {
            StrokeThickness = 2,
            Stroke = color,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Data = new PathGeometry { Figures = [pathFigure] },
        };
        _canvas.Children.Add(path);

        // Fill

        // 以画布底边闭合多边形，形成曲线下方的填充
        var pointCollection = new PointCollection { new(points[0].X, _canvas.ActualHeight - 1) };
        foreach (var point in points)
            pointCollection.Add(point);
        pointCollection.Add(new(points[^1].X, _canvas.ActualHeight - 1));

        var polygon = new Polygon
        {
            Fill = color,
            Points = pointCollection
        };
        _canvas.Children.Add(polygon);
    }

    // 计算滑块当前值对应在 Canvas 上的坐标
    private Point GetThumbLocation(Slider slider)
    {
        var ratio = slider.Value / (slider.Maximum - slider.Minimum);
        var y = slider.ActualHeight - (slider.ActualHeight * ratio);
        var x = slider.ActualWidth * 0.5;
        var point = slider.TranslatePoint(new(x, y), _canvas);
        return point;
    }

    /// <summary>
    /// 风扇曲线悬停提示，显示 CPU/CPU 传感器/GPU/GPU2 在各温度点对应的转速。
    /// </summary>
    private class InfoTooltip : ToolTip
    {
        private readonly Grid _grid = new()
        {
            ColumnDefinitions =
            {
                new() { Width = GridLength.Auto},
                new () { Width = GridLength.Auto}
            },
            RowDefinitions =
            {
                new() { Height = GridLength.Auto},
                new() { Height = GridLength.Auto},
                new() { Height = GridLength.Auto},
                new() { Height = GridLength.Auto}
            }
        };

        private readonly TextBlock _cpuDescription = new() { Text = Resource.FanCurveControl_CPU, FontWeight = FontWeights.Medium, Margin = new(0, 0, 8, 0) };
        private readonly TextBlock _cpuSensorDescription = new() { Text = Resource.FanCurveControl_CPUSensor, FontWeight = FontWeights.Medium, Margin = new(0, 0, 8, 0) };
        private readonly TextBlock _gpuDescription = new() { Text = Resource.FanCurveControl_GPU, FontWeight = FontWeights.Medium, Margin = new(0, 0, 8, 0) };
        private readonly TextBlock _gpu2Description = new() { Text = Resource.FanCurveControl_GPU2, FontWeight = FontWeights.Medium, Margin = new(0, 0, 8, 0) };

        private readonly TextBlock _cpuValue = new();
        private readonly TextBlock _cpuSensorValue = new();
        private readonly TextBlock _gpuValue = new();
        private readonly TextBlock _gpu2Value = new();

        public InfoTooltip()
        {
            InitializeComponent();
        }

        // 构建四行两列的网格内容（描述文本 + 数值）
        private void InitializeComponent()
        {
            SetResourceReference(StyleProperty, typeof(ToolTip));

            Grid.SetColumn(_cpuDescription, 0);
            Grid.SetColumn(_cpuSensorDescription, 0);
            Grid.SetColumn(_gpuDescription, 0);
            Grid.SetColumn(_gpu2Description, 0);
            Grid.SetColumn(_cpuValue, 1);
            Grid.SetColumn(_cpuSensorValue, 1);
            Grid.SetColumn(_gpuValue, 1);
            Grid.SetColumn(_gpu2Value, 1);

            Grid.SetRow(_cpuDescription, 0);
            Grid.SetRow(_cpuSensorDescription, 1);
            Grid.SetRow(_gpuDescription, 2);
            Grid.SetRow(_gpu2Description, 3);
            Grid.SetRow(_cpuValue, 0);
            Grid.SetRow(_cpuSensorValue, 1);
            Grid.SetRow(_gpuValue, 2);
            Grid.SetRow(_gpu2Value, 3);

            _grid.Children.Add(_cpuDescription);
            _grid.Children.Add(_cpuSensorDescription);
            _grid.Children.Add(_gpuDescription);
            _grid.Children.Add(_gpu2Description);
            _grid.Children.Add(_cpuValue);
            _grid.Children.Add(_cpuSensorValue);
            _grid.Children.Add(_gpuValue);
            _grid.Children.Add(_gpu2Value);

            Content = _grid;
        }

        /// <summary>
        /// 使用指定温度点索引与转速档位刷新四类风扇数据。
        /// </summary>
        /// <param name="tableData">风扇表数据。</param>
        /// <param name="index">温度点索引。</param>
        /// <param name="value">转速档位。</param>
        public void Update(FanTableData[] tableData, int index, int value)
        {
            Update(tableData, index, value, FanTableType.CPU, _cpuDescription, _cpuValue);
            Update(tableData, index, value, FanTableType.CPUSensor, _cpuSensorDescription, _cpuSensorValue);
            Update(tableData, index, value, FanTableType.GPU, _gpuDescription, _gpuValue);
            Update(tableData, index, value, FanTableType.GPU2, _gpu2Description, _gpu2Value);
        }

        // 查找指定类型的数据行并生成“温度 @ 转速”描述，缺失时隐藏整行
        private static void Update(FanTableData[] tableData, int index, int value, FanTableType type, TextBlock descriptionTextBlock, TextBlock valueTextBlock)
        {
            var text = tableData
                .Where(td => td.Type == type)
                .Select(td => GetDescription(td, index, value))
                .FirstOrDefault();

            var visibility = text is null ? Visibility.Collapsed : Visibility.Visible;

            valueTextBlock.Text = text ?? "-";
            valueTextBlock.Visibility = visibility;
            descriptionTextBlock.Visibility = visibility;
        }

        // 生成单行的“温度 @ 转速”文本；温度 127 视为无效占位
        private static string GetDescription(FanTableData tableData, int index, int value)
        {
            try
            {
                var temp = tableData.Temps[index];

                if (temp >= 127)
                    return "-";

                var rpm = value < 0 ? 0 : tableData.FanSpeeds[value];
                return $"{temp}{Resource.Celsius} @ {rpm} {Resource.RPM}";
            }
            catch
            {
                return "-";
            }
        }
    }
}

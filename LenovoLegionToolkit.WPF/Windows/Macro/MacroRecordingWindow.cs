using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Windows.Macro;

/// <summary>
/// 宏录制状态提示窗口，以轻量置顶小窗显示"准备中"或"录制中"状态。
/// </summary>
/// <remarks>该窗口不进入任务栏、不获取激活焦点，避免打断用户正在进行宏录制的目标程序。</remarks>
public class MacroRecordingWindow : UiWindow
{
    private readonly Grid _mainGrid = new()
    {
        RowDefinitions =
        {
            new() { Height = GridLength.Auto },
            new() { Height = GridLength.Auto },
        },
        ColumnDefinitions =
        {
            new() { Width = GridLength.Auto, },
            new() { Width = new(1, GridUnitType.Star) },
        },
        Margin = new(16, 16, 32, 16),
    };

    private readonly SymbolIcon _symbolIcon = new()
    {
        FontSize = 32,
        Margin = new(0, 0, 16, 0),
    };

    private readonly Label _titleLabel = new()
    {
        FontSize = 16,
        FontWeight = FontWeights.Medium,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    private readonly Label _subtitleLabel = new()
    {
        FontSize = 14,
        VerticalContentAlignment = VerticalAlignment.Center,
        Margin = new(0, 4, 0, 0),
    };

    /// <summary>
    /// 创建"准备录制"状态窗口。
    /// </summary>
    /// <returns>准备状态的提示窗口。</returns>
    public static MacroRecordingWindow CreatePreparing() => new(SymbolRegular.HourglassThreeQuarter24, Resource.MacroRecordingWindow_Preparing_Title, null);

    /// <summary>
    /// 创建"正在录制"状态窗口。
    /// </summary>
    /// <returns>录制状态的提示窗口。</returns>
    public static MacroRecordingWindow CreateRecording() => new(SymbolRegular.Record24, Resource.MacroRecordingWindow_Recording_Title, Resource.MacroRecordingWindow_Recording_Message);

    /// <summary>
    /// 初始化提示窗口，并按内容测量结果设置为紧凑的固定尺寸。
    /// </summary>
    private MacroRecordingWindow(SymbolRegular symbol, string title, string? subtitle)
    {
        InitializeStyle();
        InitializeContent(symbol, title, subtitle);

        // 先按不限宽度测量，再根据期望尺寸确定最终窗口大小（最小宽度 300）
        _mainGrid.Measure(new Size(double.PositiveInfinity, 80));

        Width = MaxWidth = MinWidth = Math.Max(_mainGrid.DesiredSize.Width, 300);
        Height = MaxHeight = MinHeight = _mainGrid.DesiredSize.Height;
    }

    /// <summary>
    /// 设置窗口样式：居中于父窗口、置顶、无激活、不显示在任务栏，并适配本地化方向。
    /// </summary>
    private void InitializeStyle()
    {
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        Focusable = false;
        Topmost = true;
        ExtendsContentIntoTitleBar = true;
        ShowInTaskbar = false;
        ShowActivated = false;

        _mainGrid.FlowDirection = LocalizationHelper.Direction;
        _titleLabel.Foreground = (SolidColorBrush)FindResource("TextFillColorPrimaryBrush");
    }

    /// <summary>
    /// 按图标、标题与副标题组装窗口内容；无副标题时标题占满两行。
    /// </summary>
    private void InitializeContent(SymbolRegular symbol, string title, string? subtitle)
    {
        _symbolIcon.Symbol = symbol;
        _titleLabel.Content = title;
        _subtitleLabel.Content = subtitle;


        Grid.SetRow(_symbolIcon, 0);
        Grid.SetRow(_titleLabel, 0);
        Grid.SetRow(_subtitleLabel, 1);

        Grid.SetColumn(_symbolIcon, 0);
        Grid.SetColumn(_titleLabel, 1);
        Grid.SetColumn(_subtitleLabel, 1);

        Grid.SetRowSpan(_symbolIcon, 2);

        if (subtitle is null)
            Grid.SetRowSpan(_titleLabel, 2);

        _mainGrid.Children.Add(_symbolIcon);
        _mainGrid.Children.Add(_titleLabel);
        _mainGrid.Children.Add(_subtitleLabel);

        Content = _mainGrid;
    }
}

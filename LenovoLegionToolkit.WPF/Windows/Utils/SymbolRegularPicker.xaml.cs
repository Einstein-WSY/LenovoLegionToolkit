using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib.Utils;
using Wpf.Ui.Common;
using Button = Wpf.Ui.Controls.Button;

namespace LenovoLegionToolkit.WPF.Windows.Utils;

/// <summary>
/// 图标选择窗口，以网格形式浏览并筛选 Fluent 图标（仅 24px 尺寸），用于让用户挑选图标。
/// </summary>
public partial class SymbolRegularPicker
{
    // 输入停止 500ms 后才真正执行筛选，避免频繁重绘图标列表
    private readonly ThrottleLastDispatcher _throttleDispatcher = new(TimeSpan.FromMilliseconds(500));

    private readonly TaskCompletionSource<SymbolRegular?> _tcs = new();

    /// <summary>
    /// 等待用户选择结果的任务；返回 <see langword="null"/> 表示选择恢复默认，取消表示未做选择。
    /// </summary>
    public Task<SymbolRegular?> SymbolRegularTask => _tcs.Task;

    /// <summary>
    /// 初始化窗口。
    /// </summary>
    public SymbolRegularPicker()
    {
        InitializeComponent();
    }

    private void SymbolRegularPicker_Loaded(object sender, RoutedEventArgs e) => Refresh();

    /// <summary>
    /// 窗口关闭时取消等待，避免调用方一直挂起。
    /// </summary>
    private void SymbolRegularPicker_Closing(object? sender, CancelEventArgs e) => _tcs.TrySetCanceled();

    /// <summary>
    /// 筛选文本变化时做节流处理，延迟刷新图标列表。
    /// </summary>
    private async void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e) => await _throttleDispatcher.DispatchAsync(() =>
    {
        Dispatcher.Invoke(Refresh);
        return Task.CompletedTask;
    });

    /// <summary>
    /// 点击某个图标时返回选中结果并关闭窗口。
    /// </summary>
    private void ItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        _tcs.TrySetResult(button.Icon);
        Close();
    }

    /// <summary>
    /// 选择"默认图标"，以 <see langword="null"/> 作为结果返回。
    /// </summary>
    private void DefaultButton_Click(object sender, RoutedEventArgs e)
    {
        _tcs.TrySetResult(null);
        Close();
    }

    /// <summary>
    /// 根据筛选文本重建图标网格，仅枚举以 "24" 结尾的图标以保证尺寸统一。
    /// </summary>
    private void Refresh()
    {
        _itemsControl.Items.Clear();

        var items = Enum.GetNames<SymbolRegular>()
                .Where(s => s.EndsWith("24", StringComparison.CurrentCultureIgnoreCase))
                .Where(s => s.Contains(_filterTextBox.Text, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(s => s)
                .ToArray();

        foreach (var item in items)
        {
            var button = new Button()
            {
                Icon = Enum.Parse<SymbolRegular>(item),
                FontSize = 32,
                Width = 80,
                Height = 80,
                Margin = new(0, 0, 4, 4)
            };
            button.Click += ItemButton_Click;
            _itemsControl.Items.Add(button);
        }
    }
}

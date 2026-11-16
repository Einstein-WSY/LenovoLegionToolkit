using System;
using System.Threading.Tasks;
using System.Windows;
using LenovoLegionToolkit.Lib;

namespace LenovoLegionToolkit.WPF.Windows.Utils;

/// <summary>
/// 不支持的设备提示窗口，展示机器信息并在倒计时结束后允许用户选择继续或退出。
/// </summary>
public partial class UnsupportedWindow
{
    private readonly TaskCompletionSource<bool> _taskCompletionSource = new();

    /// <summary>
    /// 等待用户选择结果的任务；<see langword="true"/> 表示继续启动。
    /// </summary>
    public Task<bool> ShouldContinue => _taskCompletionSource.Task;

    /// <summary>
    /// 初始化窗口并展示被识别到的机器信息。
    /// </summary>
    /// <param name="mi">检测到的机器信息。</param>
    public UnsupportedWindow(MachineInformation mi)
    {
        InitializeComponent();

        _vendorText.Text = mi.Vendor;
        _modelText.Text = mi.Model;
        _machineTypeText.Text = mi.MachineType;
    }

    /// <summary>
    /// 显示 5 秒倒计时，结束后启用"继续"按钮，避免用户误触继续启动。
    /// </summary>
    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        for (var i = 5; i > 0; i--)
        {
            _continueButton.Content = $"Continue ({i})";
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        _continueButton.Content = "Continue";
        _continueButton.IsEnabled = true;
    }

    /// <summary>
    /// 直接关闭窗口视为不继续启动。
    /// </summary>
    private void Window_Closed(object sender, EventArgs e)
    {
        _taskCompletionSource.TrySetResult(false);
    }

    /// <summary>
    /// 用户确认在不受支持的设备上继续运行。
    /// </summary>
    private void Continue_Click(object sender, RoutedEventArgs e)
    {
        _taskCompletionSource.TrySetResult(true);
        Close();
    }

    /// <summary>
    /// 用户选择退出应用。
    /// </summary>
    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        _taskCompletionSource.TrySetResult(false);
        Close();
    }
}

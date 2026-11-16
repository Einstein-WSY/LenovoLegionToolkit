using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Windows;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;

namespace LenovoLegionToolkit.WPF.Windows.Utils;

/// <summary>
/// 更新窗口，展示版本变更说明，并支持带进度下载更新安装包。
/// </summary>
public partial class UpdateWindow : IProgress<float>
{
    private readonly UpdateChecker _updateChecker = IoCContainer.Resolve<UpdateChecker>();

    private CancellationTokenSource? _downloadCancellationTokenSource;

    public UpdateWindow() => InitializeComponent();

    /// <summary>
    /// 加载更新说明并以 Markdown 形式渲染到窗口。
    /// </summary>
    private async void UpdateWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var updates = await _updateChecker.GetUpdatesAsync();

        var stringBuilder = new StringBuilder();
        foreach (var update in updates)
        {
            stringBuilder.AppendLine("**" + update.Title + "**   _(" + update.Date.ToString("D") + ")_")
                .AppendLine()
                .AppendLine(update.Description)
                .AppendLine();
        }

        _markdownViewer.Markdown = stringBuilder.ToString();

        _downloadButton.IsEnabled = true;
    }

    /// <summary>
    /// 关闭窗口时取消尚未完成的下载。
    /// </summary>
    private void UpdateWindow_Closing(object? sender, CancelEventArgs e) => _downloadCancellationTokenSource?.Cancel();

    /// <summary>
    /// 下载更新安装包，完成后以静默方式启动安装程序并退出当前应用。
    /// </summary>
    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_downloadCancellationTokenSource is not null)
                await _downloadCancellationTokenSource.CancelAsync();

            _downloadCancellationTokenSource = new();

            SetDownloading(true);

            var path = await _updateChecker.DownloadLatestUpdateAsync(this, _downloadCancellationTokenSource.Token);

            _downloadCancellationTokenSource = null;

            // 以静默模式运行安装程序，并传入与当前界面一致的安装语言
            Process.Start(path, $"/SILENT /RESTARTAPPLICATIONS /LANG={Resource.Culture.Name.Replace("-", string.Empty)}");
            await App.Current.ShutdownAsync();
        }
        catch (OperationCanceledException)
        {
            SetDownloading(false);
        }
        catch
        {
            SetDownloading(false);

            // 自动下载失败时回退到发布页面，让用户手动下载
            Constants.LatestReleaseUri.Open();
            Close();
        }
    }

    private void CancelDownloadButton_Click(object sender, RoutedEventArgs e) => _downloadCancellationTokenSource?.Cancel();

    /// <summary>
    /// 切换下载中/空闲两种界面状态。
    /// </summary>
    /// <param name="isDownloading">为 <see langword="true"/> 时显示进度条与取消按钮。</param>
    private void SetDownloading(bool isDownloading)
    {
        if (isDownloading)
        {
            _downloadProgressBar.Visibility = Visibility.Visible;

            _downloadButton.Visibility = Visibility.Collapsed;
            _downloadButton.IsEnabled = false;

            _cancelDownloadButton.Visibility = Visibility.Visible;
            _cancelDownloadButton.IsEnabled = true;
        }
        else
        {
            _downloadProgressBar.Value = 0;
            _downloadProgressBar.IsIndeterminate = true;
            _downloadProgressBar.Visibility = Visibility.Hidden;

            _downloadButton.Visibility = Visibility.Visible;
            _downloadButton.IsEnabled = true;

            _cancelDownloadButton.Visibility = Visibility.Collapsed;
            _cancelDownloadButton.IsEnabled = false;
        }
    }

    /// <summary>
    /// 接收下载进度并更新进度条；进度不大于 0 时显示为不确定状态。
    /// </summary>
    /// <param name="value">下载进度百分比（0-100）。</param>
    public void Report(float value) => Dispatcher.Invoke(() =>
    {
        _downloadProgressBar.IsIndeterminate = !(value > 0);
        _downloadProgressBar.Value = value;
    });
}

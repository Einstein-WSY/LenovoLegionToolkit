using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.PackageDownloader;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;

namespace LenovoLegionToolkit.WPF.Controls.Packages;

/// <summary>
/// 单个软件包/驱动条目的展示控件，支持查看说明、复制信息以及带进度的下载与取消。
/// </summary>
public partial class PackageControl : IProgress<float>
{
    private readonly IPackageDownloader _packageDownloader;
    private readonly Package _package;
    private readonly Func<string> _getDownloadPath;

    // 当前下载任务的取消源，取消或卸载时使用
    private CancellationTokenSource? _downloadPackageTokenSource;

    /// <summary>是否正在下载。</summary>
    public bool IsDownloading { get; private set; }

    /// <summary>
    /// 创建控件并依据软件包信息初始化各文本与可见性。
    /// </summary>
    /// <param name="packageDownloader">软件包下载器。</param>
    /// <param name="package">软件包信息。</param>
    /// <param name="getDownloadPath">获取下载目录的回调。</param>
    public PackageControl(IPackageDownloader packageDownloader, Package package, Func<string> getDownloadPath)
    {
        _packageDownloader = packageDownloader;
        _package = package;
        _getDownloadPath = getDownloadPath;

        InitializeComponent();

        Unloaded += PackageControl_Unloaded;

        // 填充界面文本，并根据数据决定各元素显示与否
        _dateTextBlock.Text = package.ReleaseDate.ToString(LocalizationHelper.ShortDateFormat);
        _titleTextBlock.Text = package.Title;
        _descriptionTextBlock.Text = package.Description;
        _descriptionTextBlock.Visibility = string.IsNullOrWhiteSpace(package.Description) ? Visibility.Collapsed : Visibility.Visible;
        _categoryTextBlock.Text = package.Category;
        _detailTextBlock.Text = $"{Resource.PackageControl_Version} {package.Version}  |  {package.FileSize}  |  {package.FileName}";

        _readmeButton.Visibility = package.Readme is null ? Visibility.Collapsed : Visibility.Visible;
        _updateRebootStackPanel.Visibility = _isUpdateStackPanel.Visibility = package.IsUpdate ? Visibility.Visible : Visibility.Collapsed;

        _rebootStackPanel.Visibility = package is { IsUpdate: true, Reboot: RebootType.Delayed or RebootType.Requested or RebootType.Forced or RebootType.ForcedPowerOff }
            ? Visibility.Visible
            : Visibility.Collapsed;
        // 根据重启类型显示不同的提示文本
        _rebootTextBlock.Text = package.Reboot switch
        {
            RebootType.Delayed or RebootType.Requested => Resource.PackageControl_RebootRecommended,
            RebootType.Forced => Resource.PackageControl_RebootRequired,
            RebootType.ForcedPowerOff => Resource.PackageControl_ShutdownRequired,
            _ => string.Empty
        };

        // 发布超过一年则提示版本可能过旧
        var showWarning = package.ReleaseDate < DateTime.UtcNow.AddYears(-1);
        _warningTextBlock.Visibility = showWarning ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PackageControl_Unloaded(object sender, RoutedEventArgs e) => _downloadPackageTokenSource?.Cancel();

    /// <summary>
    /// 下载进度回调，切回 UI 线程更新进度环与百分比文本。
    /// </summary>
    /// <param name="value">0-1 之间的进度值。</param>
    public void Report(float value) => Dispatcher.Invoke(() =>
    {
        _downloadProgressRing.IsIndeterminate = !(value > 0);
        _downloadProgressRing.Progress = value * 100;
        _downloadProgressLabel.Content = $"{value * 100:0}%";
    });

    // 右键点击文本块时复制其内容，并弹出提示
    private async void CopyToClipboard_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;

        if (sender is not TextBlock tb)
            return;

        var str = tb.Text;

        try
        {
            Clipboard.SetText(str);
            await SnackbarHelper.ShowAsync(Resource.CopiedToClipboard_Title, string.Format(Resource.CopiedToClipboard_Message_WithParam, str));
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't copy to clipboard", ex);
        }
    }

    // 用默认浏览器打开说明页面
    private void ReadmeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_package.Readme is null)
            return;

        new Uri(_package.Readme).Open();
    }

    // 下载流程：切换界面状态、带进度下载，并按异常类型给出不同提示
    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        IsDownloading = true;

        var result = false;

        try
        {
            _idleStackPanel.Visibility = Visibility.Collapsed;
            _downloadingStackPanel.Visibility = Visibility.Visible;

            if (_downloadPackageTokenSource is not null)
                await _downloadPackageTokenSource.CancelAsync();

            _downloadPackageTokenSource = new();

            var token = _downloadPackageTokenSource.Token;

            await _packageDownloader.DownloadPackageFileAsync(_package, _getDownloadPath(), this, token);

            result = true;
        }
        catch (OperationCanceledException) { }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Not found 404.", ex);

            await SnackbarHelper.ShowAsync(Resource.PackageControl_Http404Error_Title, Resource.PackageControl_Http404Error_Message, SnackbarType.Error);
        }
        catch (HttpRequestException ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Error occurred when downloading package file.", ex);

            await SnackbarHelper.ShowAsync(Resource.PackageControl_HttpGeneralError_Title, Resource.PackageControl_HttpGeneralError_Message, SnackbarType.Error);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Error occurred when downloading package file.", ex);

            await SnackbarHelper.ShowAsync(Resource.PackageControl_GeneralError_Title, ex.Message, SnackbarType.Error);
        }
        finally
        {
            _idleStackPanel.Visibility = Visibility.Visible;
            _downloadingStackPanel.Visibility = Visibility.Collapsed;
            _downloadProgressRing.Progress = 0;
            _downloadProgressLabel.Content = null;

            IsDownloading = false;
        }

        if (result)
            await SnackbarHelper.ShowAsync(Resource.PackageControl_DownloadComplete_Title, string.Format(Resource.PackageControl_DownloadComplete_Message, _package.FileName));
    }

    // 取消当前下载
    private void CancelDownloadButton_Click(object sender, RoutedEventArgs e) => _downloadPackageTokenSource?.Cancel();
}

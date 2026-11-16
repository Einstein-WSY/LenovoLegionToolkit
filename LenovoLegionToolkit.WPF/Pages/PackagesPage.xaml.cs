using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.PackageDownloader;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Controls.Packages;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using MenuItem = Wpf.Ui.Controls.MenuItem;

namespace LenovoLegionToolkit.WPF.Pages;

/// <summary>
/// “驱动下载”页面：按机型与操作系统查询官方驱动/软件包列表，支持筛选、排序、隐藏与逐个下载，
/// 并通过 <see cref="IProgress{T}"/> 反馈整体下载进度。
/// </summary>
public partial class PackagesPage : IProgress<float>
{
    /// <summary>驱动下载设置，保存下载目录、隐藏项与“仅显示更新”偏好。</summary>
    private readonly PackageDownloaderSettings _packageDownloaderSettings = IoCContainer.Resolve<PackageDownloaderSettings>();
    /// <summary>包下载器工厂，根据来源（Vantage / PC Support）创建下载器实例。</summary>
    private readonly PackageDownloaderFactory _packageDownloaderFactory = IoCContainer.Resolve<PackageDownloaderFactory>();

    /// <summary>当前使用的包下载器实例。</summary>
    private IPackageDownloader? _packageDownloader;

    /// <summary>查询包列表的取消令牌源（用于取消本次拉取）。</summary>
    private CancellationTokenSource? _getPackagesTokenSource;

    /// <summary>筛选输入防抖的取消令牌源（连续输入时取消上一次延时）。</summary>
    private CancellationTokenSource? _filterDebounceCancellationTokenSource;

    /// <summary>最近一次成功拉取的包列表缓存，用于筛选/排序时重建界面。</summary>
    private List<Package>? _packages;

    /// <summary>
    /// 初始化页面：先订阅初始化事件，再加载 XAML 视图。
    /// </summary>
    public PackagesPage()
    {
        Initialized += PackagesPage_Initialized;

        InitializeComponent();
    }

    /// <summary>
    /// 初始化完成回调：填入机型、可选操作系统与默认下载目录，并设置来源单选按钮的标签值。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数。</param>
    private async void PackagesPage_Initialized(object? sender, EventArgs e)
    {
        _machineTypeTextBox.Text = (await Compatibility.GetMachineInformationAsync()).MachineType;
        _osComboBox.SetItems(Enum.GetValues<OS>(), OSExtensions.GetCurrent(), os => os.GetDisplayName());

        // 未设置过下载目录时默认使用系统“下载”文件夹。
        var downloadsFolder = KnownFolders.GetPath(KnownFolder.Downloads);
        _downloadToText.PlaceholderText = downloadsFolder;
        _downloadToText.Text = Directory.Exists(_packageDownloaderSettings.Store.DownloadPath)
            ? _packageDownloaderSettings.Store.DownloadPath
            : downloadsFolder;

        _downloadPackagesButton.IsEnabled = true;
        _cancelDownloadPackagesButton.IsEnabled = true;

        _sourcePrimaryRadio.Tag = PackageDownloaderFactory.Type.Vantage;
        _sourceSecondaryRadio.Tag = PackageDownloaderFactory.Type.PCSupport;
    }

    /// <summary>
    /// 进度回调：负值表示进度未知（显示不确定进度条），否则按 0~1 显示确定进度。
    /// </summary>
    /// <param name="value">进度值。</param>
    public void Report(float value) => Dispatcher.Invoke(() =>
    {
        _loader.IsIndeterminate = value < 0;
        _loader.Progress = value;
    });

    /// <summary>
    /// 下载目录输入变化：目录存在时立即持久化为默认下载位置。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">文本变化参数。</param>
    private void DownloadToText_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        var location = _downloadToText.Text;

        if (!Directory.Exists(location))
            return;

        _packageDownloaderSettings.Store.DownloadPath = location;
        _packageDownloaderSettings.SynchronizeStore();
    }

    /// <summary>
    /// 打开下载目录；目录不存在时忽略。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void OpenDownloadToButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var location = GetDownloadLocation();

            if (!Directory.Exists(location))
                return;

            Process.Start("explorer", location);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to open download location.", ex);
        }
    }

    /// <summary>
    /// 选择下载目录：通过文件夹选择框更新输入框与设置。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void DownloadToButton_Click(object sender, RoutedEventArgs e)
    {
        using var ofd = new FolderBrowserDialog();
        ofd.InitialDirectory = _downloadToText.Text;

        if (ofd.ShowDialog() != DialogResult.OK)
            return;

        var selectedPath = ofd.SelectedPath;
        _downloadToText.Text = selectedPath;
        _packageDownloaderSettings.Store.DownloadPath = selectedPath;
        _packageDownloaderSettings.SynchronizeStore();
    }

    /// <summary>
    /// “下载”按钮点击：校验机型与系统后调用下载器拉取包列表，并处理各类失败提示。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private async void DownloadPackagesButton_Click(object sender, RoutedEventArgs e)
    {
        // 已有下载任务进行中时先征求用户是否中断。
        if (!await ShouldInterruptDownloadsIfRunning())
            return;

        var errorOccurred = false;
        try
        {
            _downloadPackagesButton.Visibility = Visibility.Collapsed;
            _cancelDownloadPackagesButton.Visibility = Visibility.Visible;
            _loader.Visibility = Visibility.Visible;
            _loader.IsLoading = true;
            _packages = null;

            _packagesStackPanel.Children.Clear();
            _scrollViewer.ScrollToHome();

            _filterTextBox.Text = string.Empty;
            // 默认按发布日期倒序排列。
            _sortingComboBox.SelectedIndex = 2;

            var machineType = _machineTypeTextBox.Text.Trim();

            // 机型为 4 位机器类型码，格式不合法时直接提示下载失败。
            if (string.IsNullOrWhiteSpace(machineType) || machineType.Length != 4 ||
                !_osComboBox.TryGetSelectedItem(out OS os))
            {
                await SnackbarHelper.ShowAsync(Resource.PackagesPage_DownloadFailed_Title,
                    Resource.PackagesPage_DownloadFailed_Message);
                return;
            }

            // 取消上一次仍在进行的拉取，避免两次查询互相干扰。
            if (_getPackagesTokenSource is not null)
                await _getPackagesTokenSource.CancelAsync();

            _getPackagesTokenSource = new();

            var token = _getPackagesTokenSource.Token;

            var packageDownloaderType = new[] { _sourcePrimaryRadio, _sourceSecondaryRadio }
                .Where(r => r.IsChecked == true)
                .Select(r => (PackageDownloaderFactory.Type)r.Tag)
                .First();

            // “仅显示更新”只对 Vantage 源有效，PC Support 源下隐藏该选项。
            switch (packageDownloaderType)
            {
                case PackageDownloaderFactory.Type.Vantage:
                    _onlyShowUpdatesCheckBox.Visibility = Visibility.Visible;
                    _onlyShowUpdatesCheckBox.IsChecked = _packageDownloaderSettings.Store.OnlyShowUpdates;
                    break;
                default:
                    _onlyShowUpdatesCheckBox.Visibility = Visibility.Hidden;
                    _onlyShowUpdatesCheckBox.IsChecked = false;
                    break;
            }

            _packageDownloader = _packageDownloaderFactory.GetInstance(packageDownloaderType);
            var packages = await _packageDownloader.GetPackagesAsync(machineType, os, this, token);

            _packages = packages;

            Reload();
        }
        catch (UpdateCatalogNotFoundException ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Update catalog not found.", ex);

            await SnackbarHelper.ShowAsync(Resource.PackagesPage_UpdateCatalogNotFound_Title, Resource.PackagesPage_UpdateCatalogNotFound_Message, SnackbarType.Info);

            errorOccurred = true;
        }
        catch (OperationCanceledException)
        {
            // 用户主动取消，不算错误但需要清理界面。
            errorOccurred = true;
        }
        catch (HttpRequestException ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Error occurred when downloading packages.", ex);

            await SnackbarHelper.ShowAsync(Resource.PackagesPage_Error_Title, Resource.PackagesPage_Error_CheckInternet_Message, SnackbarType.Error);

            errorOccurred = true;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Error occurred when downloading packages.", ex);

            await SnackbarHelper.ShowAsync(Resource.PackagesPage_Error_Title, ex.Message, SnackbarType.Error);

            errorOccurred = true;
        }
        finally
        {
            _downloadPackagesButton.Visibility = Visibility.Visible;
            _cancelDownloadPackagesButton.Visibility = Visibility.Collapsed;
            _loader.IsLoading = false;
            _loader.Progress = 0;
            _loader.IsIndeterminate = true;

            // 出错时清空列表并隐藏加载层，恢复为可再次点击的空状态。
            if (errorOccurred)
            {
                _packagesStackPanel.Children.Clear();
                _loader.Visibility = Visibility.Collapsed;
                _loader.IsLoading = true;
            }
        }
    }

    /// <summary>
    /// “取消”按钮点击：取消正在进行的包列表拉取。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void CancelDownloadPackagesButton_Click(object sender, RoutedEventArgs e) => _getPackagesTokenSource?.Cancel();

    /// <summary>
    /// 筛选文本变化：经 500ms 防抖后重建列表，连续输入时只执行最后一次。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">文本变化参数。</param>
    private async void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!await ShouldInterruptDownloadsIfRunning())
            return;

        try
        {
            if (_packages is null)
                return;

            // 取消上一次仍处于防抖等待中的刷新。
            if (_filterDebounceCancellationTokenSource is not null)
                await _filterDebounceCancellationTokenSource.CancelAsync();

            _filterDebounceCancellationTokenSource = new();

            await Task.Delay(500, _filterDebounceCancellationTokenSource.Token);

            _packagesStackPanel.Children.Clear();
            _scrollViewer.ScrollToHome();

            Reload();
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// “仅显示更新”勾选变化：持久化偏好并重建列表。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private async void OnlyShowUpdatesCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        if (!await ShouldInterruptDownloadsIfRunning())
            return;

        if (_packages is null)
            return;

        _packageDownloaderSettings.Store.OnlyShowUpdates = _onlyShowUpdatesCheckBox.IsChecked ?? false;
        _packageDownloaderSettings.SynchronizeStore();

        _packagesStackPanel.Children.Clear();
        _scrollViewer.ScrollToHome();

        Reload();
    }

    /// <summary>
    /// 排序方式变化：重建列表以应用新的排序规则。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">选择变化参数。</param>
    private async void SortingComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!await ShouldInterruptDownloadsIfRunning())
            return;

        if (_packages is null)
            return;

        _packagesStackPanel.Children.Clear();
        _scrollViewer.ScrollToHome();

        Reload();
    }

    /// <summary>
    /// 获取有效的下载目录：输入框路径不存在时回退到系统“下载”目录并写回设置。
    /// </summary>
    /// <returns>可用的下载目录路径。</returns>
    private string GetDownloadLocation()
    {
        var location = _downloadToText.Text.Trim();

        if (!Directory.Exists(location))
        {
            var downloads = KnownFolders.GetPath(KnownFolder.Downloads);
            location = downloads;
            _downloadToText.Text = downloads;
            _packageDownloaderSettings.Store.DownloadPath = downloads;
            _packageDownloaderSettings.SynchronizeStore();
        }

        return location;
    }

    /// <summary>
    /// 为单个包构建右键菜单：隐藏该项或隐藏当前列表中的全部项。
    /// </summary>
    /// <param name="package">目标包。</param>
    /// <param name="packages">当前列表中显示的包集合。</param>
    /// <returns>上下文菜单；该包已被隐藏时返回 null。</returns>
    private ContextMenu? GetContextMenu(Package package, IEnumerable<Package> packages)
    {
        if (_packageDownloaderSettings.Store.HiddenPackages.Contains(package.Id))
            return null;

        var hideMenuItem = new MenuItem
        {
            SymbolIcon = SymbolRegular.EyeOff24,
            Header = Resource.Hide,
        };
        hideMenuItem.Click += (_, _) =>
        {
            _packageDownloaderSettings.Store.HiddenPackages.Add(package.Id);
            _packageDownloaderSettings.SynchronizeStore();

            Reload();
        };

        var hideAllMenuItem = new MenuItem
        {
            SymbolIcon = SymbolRegular.EyeOff24,
            Header = Resource.HideAll,
        };
        hideAllMenuItem.Click += (_, _) =>
        {
            foreach (var id in packages.Select(p => p.Id))
                _packageDownloaderSettings.Store.HiddenPackages.Add(id);
            _packageDownloaderSettings.SynchronizeStore();

            Reload();
        };

        var cm = new ContextMenu();
        cm.Items.Add(hideMenuItem);
        cm.Items.Add(hideAllMenuItem);
        return cm;
    }

    /// <summary>
    /// 若存在正在下载的包，弹窗询问是否中断；无下载任务时直接允许继续。
    /// </summary>
    /// <returns>允许继续操作时返回 true。</returns>
    private async Task<bool> ShouldInterruptDownloadsIfRunning()
    {
        if (_packagesStackPanel?.Children is null)
            return true;

        if (_packagesStackPanel.Children.ToArray().OfType<PackageControl>().Where(pc => pc.IsDownloading).IsEmpty())
            return true;

        return await MessageBoxHelper.ShowAsync(this, Resource.PackagesPage_DownloadInProgress_Title, Resource.PackagesPage_DownloadInProgress_Message);
    }

    /// <summary>
    /// 按当前筛选与排序重建包列表；无匹配项时显示占位提示，并提供“显示已隐藏项”入口。
    /// </summary>
    private void Reload()
    {
        if (_packageDownloader is null)
            return;

        _packagesStackPanel.Children.Clear();

        if (_packages is null || _packages.Count == 0)
            return;

        var packages = SortAndFilter(_packages);

        foreach (var package in packages)
        {
            var control = new PackageControl(_packageDownloader, package, GetDownloadLocation)
            {
                ContextMenu = GetContextMenu(package, packages)
            };
            _packagesStackPanel.Children.Add(control);
        }

        // 过滤后没有结果时显示居中的提示文本。
        if (packages.IsEmpty())
        {
            var tb = new TextBlock
            {
                Text = Resource.PackagesPage_NoMatchingDownloads,
                Foreground = (SolidColorBrush)FindResource("TextFillColorSecondaryBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new(0, 32, 0, 32),
                Focusable = true
            };
            _packagesStackPanel.Children.Add(tb);
        }

        // 存在被隐藏的包时，提供一键清除隐藏状态的链接。
        if (_packageDownloaderSettings.Store.HiddenPackages.Count != 0)
        {
            var clearHidden = new Hyperlink
            {
                Icon = SymbolRegular.Eye24,
                Content = "Show hidden downloads",
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            clearHidden.Click += (_, _) =>
            {
                _packageDownloaderSettings.Store.HiddenPackages.Clear();
                _packageDownloaderSettings.SynchronizeStore();

                Reload();
            };
            _packagesStackPanel.Children.Add(clearHidden);
        }
    }

    /// <summary>
    /// 依据排序下拉框的顺序与筛选条件（隐藏项、仅更新、搜索文本）生成最终列表。
    /// </summary>
    /// <param name="packages">原始包列表。</param>
    /// <returns>排序并筛选后的包列表。</returns>
    private List<Package> SortAndFilter(List<Package> packages)
    {
        // 0=标题，1=分类，2=发布日期倒序，其他=保持原顺序。
        var result = _sortingComboBox.SelectedIndex switch
        {
            0 => packages.OrderBy(p => p.Title),
            1 => packages.OrderBy(p => p.Category),
            2 => packages.OrderByDescending(p => p.ReleaseDate),
            _ => packages.AsEnumerable(),
        };

        result = result.Where(p => !_packageDownloaderSettings.Store.HiddenPackages.Contains(p.Id));

        if (_onlyShowUpdatesCheckBox.IsChecked ?? false)
            result = result.Where(p => p.IsUpdate);

        // 搜索忽略大小写，范围覆盖包的索引字段（名称/描述等）。
        if (!string.IsNullOrWhiteSpace(_filterTextBox.Text))
            result = result.Where(p => p.Index.Contains(_filterTextBox.Text, StringComparison.InvariantCultureIgnoreCase));

        return result.ToList();
    }
}

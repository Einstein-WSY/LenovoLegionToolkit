using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.WPF.Controls;

/// <summary>
/// 支持异步刷新并可自动启停的控件基类：首次可见时加载数据，不可用时自动隐藏。
/// </summary>
public abstract class AbstractRefreshingControl : UserControl
{
    // 非 null 表示当前有刷新任务正在进行，用于防止重入
    private Task? _refreshTask;

    /// <summary>是否正在刷新。</summary>
    protected bool IsRefreshing => _refreshTask is not null;

    /// <summary>刷新期间是否禁用控件，默认启用。</summary>
    protected virtual bool DisablesWhileRefreshing => true;

    protected AbstractRefreshingControl()
    {
        // 默认禁用，待数据加载完成后再启用
        IsEnabled = false;

        Loaded += RefreshingControl_Loaded;
        IsVisibleChanged += RefreshingControl_IsVisibleChanged;
    }

    private void RefreshingControl_Loaded(object sender, RoutedEventArgs e)
    {
        OnFinishedLoading();
    }

    /// <summary>控件首次加载完成时调用，派生类可在此订阅消息等。</summary>
    protected abstract void OnFinishedLoading();

    private async void RefreshingControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // 每次变为可见时重新拉取数据
        if (IsVisible)
            await RefreshAsync();
    }

    /// <summary>
    /// 执行一次刷新；刷新失败或功能不受支持时隐藏控件，成功后恢复启用状态。
    /// </summary>
    protected async Task RefreshAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Refreshing control... [feature={GetType().Name}]");

        var exceptions = false;

        try
        {
            if (DisablesWhileRefreshing)
                IsEnabled = false;

            // 复用正在进行的刷新任务，避免并发刷新
            _refreshTask ??= OnRefreshAsync();
            await _refreshTask;
        }
        catch (NotSupportedException)
        {
            exceptions = true;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Unsupported. [feature={GetType().Name}]");
        }
        catch (Exception ex)
        {
            exceptions = true;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Exception when refreshing control. [feature={GetType().Name}]", ex);
        }
        finally
        {
            _refreshTask = null;

            if (exceptions)
                Visibility = Visibility.Collapsed;
            else
                IsEnabled = true;
        }
    }

    /// <summary>派生类实现的实际刷新逻辑。</summary>
    /// <returns>刷新任务。</returns>
    protected abstract Task OnRefreshAsync();
}

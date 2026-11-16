using System;
using System.Diagnostics.Eventing.Reader;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Listeners;

/// <summary>
/// Windows 事件日志监听器基类：基于 <see cref="EventLogWatcher"/> 订阅指定日志源与 XPath 查询，
/// 当有匹配的日志记录写入时通知派生类并触发 <see cref="Changed"/> 事件。
/// </summary>
public abstract class AbstractEventLogListener : IListener<EventArgs>
{
    private readonly EventLogWatcher _watcher;

    /// <summary>
    /// 初始化事件日志监听器。
    /// </summary>
    /// <param name="path">事件日志名称（如 "Application"）。</param>
    /// <param name="query">用于筛选事件的 XPath 查询表达式。</param>
    public AbstractEventLogListener(string path, string query)
    {
        var eventLogQuery = new EventLogQuery(path, PathType.LogName, query);
        _watcher = new EventLogWatcher(eventLogQuery);
        // 订阅日志写入回调；退订无需显式进行，_watcher 生命周期与监听器实例一致
        _watcher.EventRecordWritten += Watcher_EventRecordWritten;
    }

    /// <summary>
    /// 事件日志中写入匹配记录后触发。
    /// </summary>
    public event EventHandler<EventArgs>? Changed;

    /// <inheritdoc/>
    public Task StartAsync()
    {
        // 幂等启动：已在监听则跳过，避免重复订阅
        if (!_watcher.Enabled)
            _watcher.Enabled = true;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync()
    {
        _watcher.Enabled = false;
        return Task.CompletedTask;
    }

    /// <summary>
    /// 派生类处理匹配日志记录的逻辑，在 <see cref="Changed"/> 触发之前调用。
    /// </summary>
    /// <returns>表示处理完成的任务。</returns>
    protected abstract Task OnChangedAsync();

    // async void 仅用于事件回调签名匹配；异常统一在此吞掉并记录，避免进程崩溃
    private async void Watcher_EventRecordWritten(object? sender, EventRecordWrittenEventArgs e)
    {
        try
        {
            await OnChangedAsync().ConfigureAwait(false);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to handle event.  [listener={GetType().Name}]", ex);
        }
    }
}

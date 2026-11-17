using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.AutoListeners;

/// <summary>
/// 进程启动/结束监听器：把 WMI 的进程事件转换为携带程序路径的 <see cref="ProcessInfo"/> 事件，
/// 并过滤掉系统进程与噪音进程，供自动化规则匹配使用。
/// </summary>
public class ProcessAutoListener(
    InstanceStartedEventAutoAutoListener instanceStartedEventAutoAutoListener,
    InstanceStoppedEventAutoAutoListener instanceStoppedEventAutoAutoListener)
    : AbstractAutoListener<ProcessAutoListener.ChangedEventArgs>
{
    /// <summary>
    /// 进程事件参数。
    /// </summary>
    public class ChangedEventArgs(ProcessEventInfoType type, ProcessInfo processInfo) : EventArgs
    {
        public ProcessEventInfoType Type { get; } = type; // 启动或结束

        public ProcessInfo ProcessInfo { get; } = processInfo; // 相关进程信息
    }

    private static readonly object Lock = new(); // 保护 _processCache

    // ReSharper disable StringLiteralTypo

    // 系统/后台噪音进程白名单：这些进程频繁启停但与用户实际使用无关，忽略以降低负载
    private static readonly string[] IgnoredNames =
    [
        "backgroundTaskHost",
        "cmd",
        "CompPkgSrv",
        "conhost",
        "dllhost",
        "Lenovo Legion Toolkit",
        "msedge",
        "msedgewebview2",
        "NvOAWrapperCache",
        "SearchProtocolHost",
        "svchost",
        "taskhostw",
        "WmiApSrv",
        "WmiPrvSE"
    ];

    // ReSharper restore StringLiteralTypo

    // 位于系统目录下的进程一律忽略，避免系统组件干扰自动化规则
    private static readonly string[] IgnoredPaths =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.Windows),
    ];

    private readonly Dictionary<int, ProcessInfo> _processCache = []; // 进程 ID → 进程信息，保证结束时能报告路径

    protected override async Task StartAsync()
    {
        await instanceStartedEventAutoAutoListener.SubscribeChangedAsync(InstanceStartedEventAutoListener_Changed).ConfigureAwait(false);
        await instanceStoppedEventAutoAutoListener.SubscribeChangedAsync(InstanceStoppedEventAutoListener_Changed).ConfigureAwait(false);
    }

    protected override async Task StopAsync()
    {
        await instanceStartedEventAutoAutoListener.UnsubscribeChangedAsync(InstanceStartedEventAutoListener_Changed).ConfigureAwait(false);
        await instanceStoppedEventAutoAutoListener.UnsubscribeChangedAsync(InstanceStoppedEventAutoListener_Changed).ConfigureAwait(false);

        lock (Lock)
            _processCache.Clear();
    }

    private void InstanceStartedEventAutoListener_Changed(object? sender, InstanceStartedEventAutoAutoListener.ChangedEventArgs e)
    {
        lock (Lock)
        {
            if (e.ProcessId < 0)
                return;

            if (string.IsNullOrWhiteSpace(e.ProcessName))
                return;

            if (IgnoredNames.Contains(e.ProcessName, StringComparer.InvariantCultureIgnoreCase))
                return;

            string? processPath = null;
            try
            {
                processPath = Process.GetProcessById(e.ProcessId).GetFileName();
            }
            catch (ArgumentException)
            {
                // 进程在事件到达前已退出，属于正常竞态
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Process {e.ProcessName} isn't running, ignoring... [processId={e.ProcessId}]");
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Can't get process {e.ProcessName} details. [processId={e.ProcessId}]", ex);
            }

            if (!string.IsNullOrEmpty(processPath) && IgnoredPaths.Any(p => processPath.StartsWith(p, StringComparison.InvariantCultureIgnoreCase)))
                return;

            var processInfo = new ProcessInfo(e.ProcessName, processPath);
            _processCache[e.ProcessId] = processInfo;

            CleanUpCacheIfNecessary();

            RaiseChanged(new ChangedEventArgs(ProcessEventInfoType.Started, processInfo));
        }
    }

    private void InstanceStoppedEventAutoListener_Changed(object? sender, InstanceStoppedEventAutoAutoListener.ChangedEventArgs e)
    {
        lock (Lock)
        {
            CleanUpCacheIfNecessary();

            if (e.ProcessId < 0)
                return;

            if (string.IsNullOrWhiteSpace(e.ProcessName))
                return;

            if (IgnoredNames.Contains(e.ProcessName, StringComparer.InvariantCultureIgnoreCase))
                return;

            if (!_processCache.Remove(e.ProcessId, out var processInfo))
                return;

            RaiseChanged(new ChangedEventArgs(ProcessEventInfoType.Stopped, processInfo));
        }
    }

    private void CleanUpCacheIfNecessary()
    {
        // 仅当缓存超过阈值时才做一次全量清理，避免频繁遍历
        if (_processCache.Count < 250)
            return;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Cleaning up process cache. Current size: {_processCache.Count}.");

        // 遍历期间会移除元素，必须先快照，否则会抛 InvalidOperationException
        foreach (var (processId, _) in _processCache.ToArray())
        {
            try
            {
                _ = Process.GetProcessById(processId);
            }
            catch (ArgumentException)
            {
                _processCache.Remove(processId);
            }
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Cleaned up process cache. Current size: {_processCache.Count}.");
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.GameDetection;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.AutoListeners;

/// <summary>
/// 游戏运行状态监听器：综合"游戏列表 + 进程启动 + 游戏模式检测"判断当前是否有游戏在运行。
/// 三个来源任一发现游戏即可判定为运行中，全部结束后才判定为停止。
/// </summary>
public class GameAutoListener : AbstractAutoListener<GameAutoListener.ChangedEventArgs>
{
    /// <summary>
    /// 游戏运行状态变化的事件参数。
    /// </summary>
    public class ChangedEventArgs(bool running) : EventArgs
    {
        public bool Running { get; } = running; // true 表示有游戏正在运行
    }

    /// <summary>
    /// 按进程 ID 比较进程，便于 HashSet 去重（Process 对象本身不按 ID 重写相等性）。
    /// </summary>
    private class ProcessEqualityComparer : IEqualityComparer<Process>
    {
        public bool Equals(Process? x, Process? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null) return false;
            if (y is null) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.Id == y.Id;
        }

        public int GetHashCode(Process obj) => obj.Id;
    }

    private static readonly object Lock = new(); // 保护所有缓存与状态字段（同时供多个事件回调使用）

    private readonly InstanceStartedEventAutoAutoListener _instanceStartedEventAutoAutoListener;

    private readonly GameConfigStoreDetector _gameConfigStoreDetector;
    private readonly EffectiveGameModeDetector _effectiveGameModeDetector;

    private readonly HashSet<ProcessInfo> _detectedGamePathsCache = []; // 已知游戏（名称+路径）缓存
    private readonly HashSet<Process> _processCache = new(new ProcessEqualityComparer()); // 当前正在运行的游戏进程

    private bool _lastState; // 上一次上报的游戏运行状态，用于去重

    public GameAutoListener(InstanceStartedEventAutoAutoListener instanceStartedEventAutoAutoListener)
    {
        _instanceStartedEventAutoAutoListener = instanceStartedEventAutoAutoListener;

        _gameConfigStoreDetector = new GameConfigStoreDetector();
        _gameConfigStoreDetector.GamesDetected += GameConfigStoreDetectorGamesConfigStoreDetected;

        _effectiveGameModeDetector = new EffectiveGameModeDetector();
        _effectiveGameModeDetector.Changed += EffectiveGameModeDetectorChanged;
    }

    protected override async Task StartAsync()
    {
        // 先载入已知游戏列表，避免启动初期漏判
        lock (Lock)
        {
            foreach (var gamePath in GameConfigStoreDetector.GetDetectedGamePaths())
                _detectedGamePathsCache.Add(gamePath);
        }

        await _gameConfigStoreDetector.StartAsync().ConfigureAwait(false);
        await _effectiveGameModeDetector.StartAsync().ConfigureAwait(false);

        await _instanceStartedEventAutoAutoListener.SubscribeChangedAsync(InstanceStartedEventAutoAutoListener_Changed).ConfigureAwait(false);
    }

    protected override async Task StopAsync()
    {
        await _instanceStartedEventAutoAutoListener.UnsubscribeChangedAsync(InstanceStartedEventAutoAutoListener_Changed).ConfigureAwait(false);

        await _gameConfigStoreDetector.StopAsync().ConfigureAwait(false);
        await _effectiveGameModeDetector.StopAsync().ConfigureAwait(false);

        // 卸载对进程的事件挂钩，并清空缓存
        lock (Lock)
        {
            foreach (var process in _processCache)
                Detach(process);

            _processCache.Clear();
            _detectedGamePathsCache.Clear();
            _lastState = false;
        }
    }

    /// <summary>
    /// 返回当前是否检测到游戏正在运行。
    /// </summary>
    public bool AreGamesRunning()
    {
        lock (Lock)
        {
            return _lastState;
        }
    }

    // 游戏配置库变化（用户新增/删除游戏）时重建缓存，并检查这些游戏是否已在运行
    private void GameConfigStoreDetectorGamesConfigStoreDetected(object? sender, GameConfigStoreDetector.GameDetectedEventArgs e)
    {
        lock (Lock)
        {
            _detectedGamePathsCache.Clear();

            foreach (var game in e.Games)
            {
                _detectedGamePathsCache.Add(game);

                foreach (var process in Process.GetProcessesByName(game.Name))
                {
                    try
                    {
                        var processPath = process.MainModule?.FileName;
                        if (game.ExecutablePath is null || !game.ExecutablePath.Equals(processPath, StringComparison.CurrentCultureIgnoreCase))
                            continue;

                        if (!_processCache.Contains(process))
                        {
                            Attach(process);
                            _processCache.Add(process);
                        }

                        RaiseChangedIfNeeded(true);
                    }
                    catch (Exception)
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Can't get game \"{game}\" details.");
                    }
                }
            }
        }
    }

    // 系统"游戏模式"状态变化：仅在没有通过进程精确匹配到游戏时才作为兜底依据
    private void EffectiveGameModeDetectorChanged(object? sender, bool e)
    {
        lock (Lock)
        {
            if (_processCache.Count != 0)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Ignoring, process cache is not empty.");
                return;
            }

            RaiseChangedIfNeeded(e);
        }
    }

    // 有新进程启动：若其名称与路径命中已知游戏，则计入"游戏运行中"
    private void InstanceStartedEventAutoAutoListener_Changed(object? sender, InstanceStartedEventAutoAutoListener.ChangedEventArgs e)
    {
        lock (Lock)
        {
            if (e.ProcessId < 0)
                return;

            // 先按名称快速过滤，避免对所有新进程做昂贵的路径查询
            if (!_detectedGamePathsCache.Any(p => e.ProcessName.Equals(p.Name, StringComparison.CurrentCultureIgnoreCase)))
                return;

            try
            {
                var process = Process.GetProcessById(e.ProcessId);
                var processPath = process.GetFileName();

                if (string.IsNullOrEmpty(processPath))
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Can't get path for {e.ProcessName}. [processId={e.ProcessId}]");

                    return;
                }

                var processInfo = ProcessInfo.FromPath(processPath);
                if (!_detectedGamePathsCache.Contains(processInfo))
                    return;

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Game {processInfo} is running. [processId={e.ProcessId}, processPath={processPath}]");

                Attach(process);
                _processCache.Add(process);

                RaiseChangedIfNeeded(true);
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to attach to {e.ProcessName}. [processId={e.ProcessId}]", ex);
            }
        }
    }

    // 仅在状态真正改变时广播，避免重复通知
    private void RaiseChangedIfNeeded(bool newState)
    {
        lock (Lock)
        {
            if (newState == _lastState)
                return;

            _lastState = newState;

            RaiseChanged(new ChangedEventArgs(newState));
        }
    }

    // 开启进程退出事件通知，以便游戏结束后及时更新状态
    private void Attach(Process process)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Attaching to process {process.Id}...");

        process.EnableRaisingEvents = true;
        process.Exited += Process_Exited;
    }

    // 解除进程退出事件通知，防止 Process 对象被长期持有
    private void Detach(Process process)
    {
        process.EnableRaisingEvents = false;
        process.Exited -= Process_Exited;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Detached from process {process.Id}.");
    }

    // 某进程退出：清理已退出的进程，若全部结束则上报"无游戏运行"
    private void Process_Exited(object? o, EventArgs args)
    {
        lock (Lock)
        {
            if (o is not Process process)
                return;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Process {process.Id} exited.");

            // 顺带清理其他已退出的进程；访问 HasExited 可能抛异常（进程已释放），视为已退出
            var staleProcesses = _processCache.RemoveWhere(p =>
            {
                try { return p.HasExited; }
                catch { return true; }
            });

            if (staleProcesses > 1)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Removed {staleProcesses} stale processes.");
            }

            if (_processCache.Count != 0)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"More games are running...");

                return;
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"No more games are running.");

            RaiseChangedIfNeeded(false);
        }
    }
}

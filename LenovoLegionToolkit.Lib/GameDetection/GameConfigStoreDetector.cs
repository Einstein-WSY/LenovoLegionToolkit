using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.GameDetection;

/// <summary>
/// 通过监视注册表 <c>GameConfigStore</c> 来检测系统中被识别为游戏的程序。
/// <para>
/// Windows 会在用户启动游戏时把其路径记录到 <c>HKCU\System\GameConfigStore\Children</c> 下，
/// 本类监听该注册表键的变化，从而获知新增/移除的游戏。
/// </para>
/// </summary>
internal class GameConfigStoreDetector
{
    private const string GAME_CONFIG_STORE_HIVE = "HKEY_CURRENT_USER";
    private const string GAME_CONFIG_STORE_PATH = @"System\GameConfigStore\Children";
    private const string MATCHED_EXE_FULL_PATH_KEY_NAME = "MatchedExeFullPath";

    /// <summary>
    /// 游戏检测事件参数。
    /// </summary>
    /// <param name="games">当前检测到的游戏进程信息集合。</param>
    public class GameDetectedEventArgs(HashSet<ProcessInfo> games) : EventArgs
    {
        /// <summary>
        /// 当前检测到的游戏进程信息集合。
        /// </summary>
        public HashSet<ProcessInfo> Games { get; } = games;
    }

    /// <summary>
    /// 当检测到的游戏列表发生变化时触发。
    /// </summary>
    public event EventHandler<GameDetectedEventArgs>? GamesDetected;

    private IAsyncDisposable? _listener;

    /// <summary>
    /// 开始监视注册表，检测游戏列表变化。
    /// </summary>
    /// <returns>表示操作完成的任务。</returns>
    public Task StartAsync()
    {
        if (_listener is not null)
            return Task.CompletedTask;

        var lastPaths = GetDetectedGamePaths();

        _listener = Registry.ObserveKey(GAME_CONFIG_STORE_HIVE, GAME_CONFIG_STORE_PATH, true, () =>
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Registry changed...");

            var newPaths = GetDetectedGamePaths();

            // 使用集合比较（与顺序无关）判断游戏列表是否真正变化，避免无意义的通知
            if (!newPaths.SetEquals(lastPaths))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Detected games changed.");

                GamesDetected?.Invoke(this, new(newPaths));
            }

            lastPaths = newPaths;
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止监视注册表。
    /// </summary>
    /// <returns>表示操作完成的任务。</returns>
    public async Task StopAsync()
    {
        if (_listener is not null)
            await _listener.DisposeAsync().ConfigureAwait(false);
        _listener = null;
    }

    /// <summary>
    /// 读取注册表，获取当前被系统记录的所有游戏可执行文件路径。
    /// </summary>
    /// <returns>游戏进程信息集合。</returns>
    public static HashSet<ProcessInfo> GetDetectedGamePaths()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Detecting games...");

        var result = new HashSet<ProcessInfo>();

        foreach (var subKey in Registry.GetSubKeys(GAME_CONFIG_STORE_HIVE, GAME_CONFIG_STORE_PATH))
        {
            var exePath = Registry.GetValue(GAME_CONFIG_STORE_HIVE, subKey, MATCHED_EXE_FULL_PATH_KEY_NAME, string.Empty);
            if (string.IsNullOrEmpty(exePath))
                continue;

            result.Add(ProcessInfo.FromPath(exePath));
        }

        if (Log.Instance.IsTraceEnabled)
        {
            Log.Instance.Trace($"Detected games:");
            foreach (var r in result)
                Log.Instance.Trace($" - {r}");
        }

        return result;
    }
}

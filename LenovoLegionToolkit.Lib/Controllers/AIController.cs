using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.AutoListeners;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;
using NeoSmart.AsyncLock;

namespace LenovoLegionToolkit.Lib.Controllers;

/// <summary>
/// AI 模式控制器：在"接通电源 + 均衡模式 + 有游戏运行"时自动开启智能子模式，
/// 条件不再满足时自动关闭。用于根据当前运行的软件动态调整性能策略。
/// </summary>
public class AIController(
    PowerModeListener powerModeListener,
    PowerStateListener powerStateListener,
    GameAutoListener gameAutoListener,
    PowerModeFeature powerModeFeature,
    BalanceModeSettings settings)
{
    // 1 秒内多次触发只执行最后一次刷新，避免状态频繁变化时反复读写 WMI
    private readonly ThrottleLastDispatcher _dispatcher = new(TimeSpan.FromSeconds(1), nameof(AIController));

    private readonly AsyncLock _startStopLock = new(); // 串行化启动/停止/刷新

    /// <summary>
    /// AI 模式是否启用（持久化到设置）。
    /// </summary>
    public bool IsAIModeEnabled
    {
        get => settings.Store.AIModeEnabled;
        set
        {
            settings.Store.AIModeEnabled = value;
            settings.SynchronizeStore();
        }
    }

    /// <summary>
    /// 按当前配置决定是否启动监听：先停止已有监听，再在启用时重新挂接事件。
    /// </summary>
    public async Task StartIfNeededAsync()
    {
        if (!await IsSupportedAsync().ConfigureAwait(false))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Not supported.");

            return;
        }

        await StopAsync().ConfigureAwait(false);

        if (!IsAIModeEnabled)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"AI Mode is not enabled.");
            return;
        }

        using (await _startStopLock.LockAsync().ConfigureAwait(false))
        {
            powerModeListener.Changed += PowerModeListener_Changed;
            powerStateListener.Changed += PowerStateListener_Changed;

            await gameAutoListener.SubscribeChangedAsync(GameAutoListener_Changed).ConfigureAwait(false);

            await RefreshAsync().ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Started.");
        }
    }

    /// <summary>
    /// 停止监听，并视情况把智能子模式复位为关闭。
    /// </summary>
    public async Task StopAsync()
    {
        if (!await IsSupportedAsync().ConfigureAwait(false))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Not supported.");

            return;
        }

        using (await _startStopLock.LockAsync().ConfigureAwait(false))
        {
            powerModeListener.Changed -= PowerModeListener_Changed;
            powerStateListener.Changed -= PowerStateListener_Changed;

            await gameAutoListener.UnsubscribeChangedAsync(GameAutoListener_Changed).ConfigureAwait(false);

            if (await ShouldDisableAsync().ConfigureAwait(false))
                await DisableAsync().ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Stopped.");
        }
    }

    // 三类事件（电源模式变化、插拔电源、游戏启停）都可能改变判定条件，统一触发刷新
    private async void PowerModeListener_Changed(object? sender, PowerModeListener.ChangedEventArgs e) => await _dispatcher.DispatchAsync(RefreshAsync).ConfigureAwait(false);
    private async void PowerStateListener_Changed(object? sender, PowerStateListener.ChangedEventArgs e) => await _dispatcher.DispatchAsync(RefreshAsync).ConfigureAwait(false);
    private async void GameAutoListener_Changed(object? sender, GameAutoListener.ChangedEventArgs e) => await _dispatcher.DispatchAsync(RefreshAsync).ConfigureAwait(false);

    // 重新评估条件：先判断是否需要关闭（优先级高于开启），再判断是否需要开启
    private async Task RefreshAsync()
    {
        if (!await IsSupportedAsync().ConfigureAwait(false))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Not supported.");

            return;
        }

        using (await _startStopLock.LockAsync().ConfigureAwait(false))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Refreshing...");

            if (await ShouldDisableAsync().ConfigureAwait(false))
                await DisableAsync().ConfigureAwait(false);

            if (await ShouldEnableAsync().ConfigureAwait(false))
                await EnableAsync().ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Refreshed");
        }
    }

    // 仅机型具备 AI 能力时才工作
    private static async Task<bool> IsSupportedAsync()
    {
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        return mi.Properties.SupportsAIMode;
    }

    // 开启条件：接通电源 + 均衡模式 + 有游戏运行
    private async Task<bool> ShouldEnableAsync()
    {
        if (await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false) != PowerAdapterStatus.Connected)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Power adapter not connected.");

            return false;
        }

        if (await powerModeFeature.GetStateAsync().ConfigureAwait(false) != PowerModeState.Balance)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Not in balanced mode.");

            return false;
        }

        if (!gameAutoListener.AreGamesRunning())
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Games aren't running.");

            return false;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"All conditions met.");

        return true;
    }

    // 关闭条件：仍处于均衡模式，且当前智能子模式不为 0（即已被开启）
    private async Task<bool> ShouldDisableAsync()
    {
        if (await powerModeFeature.GetStateAsync().ConfigureAwait(false) != PowerModeState.Balance)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Not in balanced mode.");

            return false;
        }

        if (await WMI.LenovoGameZoneData.GetIntelligentSubModeAsync().ConfigureAwait(false) == 0)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Not needed.");

            return false;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"All conditions met.");

        return true;
    }

    // 开启：在"智能操作列表"中找到第一个正在运行的程序，按其预设的子模式开启；
    // 未匹配到任何程序时默认使用子模式 1
    private static async Task EnableAsync()
    {
        try
        {
            var targetSubMode = 1;

            var intelligentOpList = await WMI.LenovoIntelligentOPList.ReadAsync().ConfigureAwait(false);
            foreach (var (processName, subMode) in intelligentOpList)
            {
                var process = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(processName)).FirstOrDefault();
                if (process is null)
                    continue;

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Found running process {processName}. [processId={process.Id}, subMode={subMode}]");

                targetSubMode = subMode;
                break;
            }

            await WMI.LenovoGameZoneData.SetIntelligentSubModeAsync(targetSubMode).ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Initial sub mode set.");
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to start.", ex);
        }
    }

    // 关闭：子模式置 0 即关闭智能模式
    private static async Task DisableAsync()
    {
        try
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Stopping...");

            await WMI.LenovoGameZoneData.SetIntelligentSubModeAsync(0).ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Stopped");
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to stop.", ex);
        }
    }
}

using System;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Features.Hybrid.Notify;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features.Hybrid;

/// <summary>
/// 混合显卡模式特性（混合模式 / 独显直连 / iGPU 模式 / 自动模式）。
/// 由两个底层能力组合而成：
/// <list type="bullet">
/// <item><description>G-Sync 开关（<see cref="GSyncFeature"/>）：开启即代表独显直连（混合模式关闭）。</description></item>
/// <item><description>iGPU 模式（<see cref="IGPUModeFeature"/>）：控制核显独显的启用策略。</description></item>
/// </list>
/// 因此混合模式状态是这两个底层状态的"打包"结果，见 <see cref="Pack"/> 与 <see cref="Unpack"/>。
/// </summary>
public class HybridModeFeature(GSyncFeature gSyncFeature, IGPUModeFeature igpuModeFeature, DGPUNotify dgpuNotify) : IFeature<HybridModeState>
{
    // 用于取消后台定时"通知 dGPU 弹出"的任务
    private readonly CancellationTokenSource _ensureDGPUEjectedIfNeededCancellationTokenSource = new();

    /// <inheritdoc/>
    public async Task<bool> IsSupportedAsync()
    {
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        // 具备 G-Sync 或 iGPU 模式能力之一即视为支持混合模式切换
        return mi.Properties.SupportsGSync || mi.Properties.SupportsIGPUMode;
    }

    /// <inheritdoc/>
    public async Task<HybridModeState[]> GetAllStatesAsync()
    {
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);

        // 可用状态取决于机器同时具备哪些底层能力
        return (mi.Properties.SupportsGSync, mi.Properties.SupportsIGPUMode) switch
        {
            (true, true) => [HybridModeState.On, HybridModeState.OnIGPUOnly, HybridModeState.OnAuto, HybridModeState.Off],
            (false, true) => [HybridModeState.On, HybridModeState.OnIGPUOnly, HybridModeState.OnAuto],
            (true, false) => [HybridModeState.On, HybridModeState.Off],
            _ => []
        };
    }

    /// <inheritdoc/>
    public async Task<HybridModeState> GetStateAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting state...");

        var gSyncSupported = await gSyncFeature.IsSupportedAsync().ConfigureAwait(false);
        var igpuModeSupported = await igpuModeFeature.IsSupportedAsync().ConfigureAwait(false);

        // 不支持的能力使用安全的默认值参与打包
        var gSync = GSyncState.Off;
        var igpuMode = IGPUModeState.Default;

        if (gSyncSupported)
            gSync = await gSyncFeature.GetStateAsync().ConfigureAwait(false);

        if (igpuModeSupported)
            igpuMode = await igpuModeFeature.GetStateAsync().ConfigureAwait(false);

        var state = Pack(gSync, igpuMode);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State is {state} [gSync={gSync}, igpuMode={igpuMode}]");

        return state;
    }

    /// <inheritdoc/>
    public async Task SetStateAsync(HybridModeState state)
    {
        // 用户主动切换模式时，取消可能仍在进行的 dGPU 弹出重试任务
        await _ensureDGPUEjectedIfNeededCancellationTokenSource.CancelAsync().ConfigureAwait(false);

        var (gSync, igpuMode) = Unpack(state);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting state to {state}... [gSync={gSync}, igpuMode={igpuMode}]");

        var gSyncSupported = await gSyncFeature.IsSupportedAsync().ConfigureAwait(false);
        var igpuModeSupported = await igpuModeFeature.IsSupportedAsync().ConfigureAwait(false);

        var gSyncChanged = false;

        // 先切换 G-Sync：开启 G-Sync 需要独显直连，必须先于 iGPU 模式切换
        if (gSyncSupported && await gSyncFeature.GetStateAsync().ConfigureAwait(false) != gSync)
        {
            await gSyncFeature.SetStateAsync(gSync).ConfigureAwait(false);
            gSyncChanged = true;
        }

        // 再切换 iGPU 模式
        if (igpuModeSupported && await igpuModeFeature.GetStateAsync().ConfigureAwait(false) != igpuMode)
        {
            try
            {
                await igpuModeFeature.SetStateAsync(igpuMode).ConfigureAwait(false);
            }
            catch (IGPUModeChangeException)
            {
                // 若本次并未改动 G-Sync，则 iGPU 模式切换失败属于真正的错误，向上抛出；
                // 若刚改过 G-Sync，失败可能是切换过程中的正常过渡，吞掉异常继续
                if (!gSyncChanged)
                    throw;
            }
            finally
            {
                // 切回混合/自动模式后，dGPU 需要在稍后被"弹出"以省电
                if (!gSyncChanged && igpuMode is IGPUModeState.Default or IGPUModeState.Auto)
                    await dgpuNotify.NotifyLaterIfNeededAsync().ConfigureAwait(false);
            }
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State set to {state} [gSync={gSync}, igpuMode={igpuMode}]");
    }

    /// <summary>
    /// 在后台确保 dGPU 已被弹出（进入 iGPU-only 后独显应断电）。
    /// 由于固件/驱动状态更新有延迟，最多重试 5 次、每次间隔 5 秒；
    /// 期间若用户切换了模式或 dGPU 已不可用则提前结束。
    /// </summary>
    public async Task EnsureDGPUEjectedIfNeededAsync()
    {
        if (!await igpuModeFeature.IsSupportedAsync().ConfigureAwait(false) || !await dgpuNotify.IsSupportedAsync().ConfigureAwait(false))
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                const int maxRetries = 5;
                const int delay = 5 * 1000;

                var retry = 1;

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Will make sure that dGPU is ejected. [maxRetries={maxRetries}, delay={delay}ms]");

                while (retry <= maxRetries)
                {
                    await Task.Delay(delay).ConfigureAwait(false);

                    // 已被新一轮 SetStateAsync 取消
                    if (_ensureDGPUEjectedIfNeededCancellationTokenSource.IsCancellationRequested)
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Cancelled, aborting...");
                        break;
                    }

                    // 已不在纯核显模式，无需弹出 dGPU
                    if (await igpuModeFeature.GetStateAsync().ConfigureAwait(false) != IGPUModeState.IGPUOnly)
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Not in iGPU-only mode, aborting...");
                        break;
                    }

                    // dGPU 已不可用（已断电），任务完成
                    if (!await dgpuNotify.IsDGPUAvailableAsync().ConfigureAwait(false))
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"dGPU already unavailable, aborting...");
                        break;
                    }

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Notifying dGPU... [retry={retry}, maxRetries={maxRetries}]");

                    await dgpuNotify.NotifyAsync(false).ConfigureAwait(false);

                    retry++;
                }
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to ensure dGPU is ejected", ex);
            }
        });
    }

    // 混合模式状态 -> (G-Sync, iGPU 模式)
    private static (GSyncState, IGPUModeState) Unpack(HybridModeState state) => state switch
    {
        HybridModeState.On => (GSyncState.Off, IGPUModeState.Default),
        HybridModeState.OnIGPUOnly => (GSyncState.Off, IGPUModeState.IGPUOnly),
        HybridModeState.OnAuto => (GSyncState.Off, IGPUModeState.Auto),
        HybridModeState.Off => (GSyncState.On, IGPUModeState.Default),
        _ => throw new InvalidOperationException("Invalid state"),
    };

    // (G-Sync, iGPU 模式) -> 混合模式状态；G-Sync 开启优先视为直连模式
    private static HybridModeState Pack(GSyncState state1, IGPUModeState state2) => (state1, state2) switch
    {
        (GSyncState.Off, IGPUModeState.Default) => HybridModeState.On,
        (GSyncState.Off, IGPUModeState.IGPUOnly) => HybridModeState.OnIGPUOnly,
        (GSyncState.Off, IGPUModeState.Auto) => HybridModeState.OnAuto,
        (GSyncState.On, _) => HybridModeState.Off,
        _ => throw new InvalidOperationException("Invalid state"),
    };
}

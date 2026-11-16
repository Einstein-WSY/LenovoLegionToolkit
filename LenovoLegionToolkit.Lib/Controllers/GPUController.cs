using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Resources;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;
using NeoSmart.AsyncLock;

namespace LenovoLegionToolkit.Lib.Controllers;

/// <summary>
/// 独立显卡状态控制器：基于 NVAPI 周期刷新 GPU 状态（占用进程、性能状态），
/// 并提供重启设备、结束占用进程等操作。所有状态访问均由异步锁保护。
/// </summary>
public class GPUController
{
    private readonly AsyncLock _lock = new();

    private Task? _refreshTask; // 后台刷新循环任务
    private CancellationTokenSource? _refreshCancellationTokenSource; // 用于停止刷新循环

    private GPUState _state = GPUState.Unknown;
    private List<Process> _processes = [];
    private string? _gpuInstanceId; // 设备实例 ID，用于 pnputil 重启设备
    private string? _performanceState; // 性能状态展示文本（如 "Powered on, P0"）

    /// <summary>
    /// 每次刷新完成后触发，携带最新的 GPU 状态。
    /// </summary>
    public event EventHandler<GPUStatus>? Refreshed;
    public bool IsStarted { get => _refreshTask != null; }

    /// <summary>
    /// 检测本机是否存在可通过 NVAPI 访问的 NVIDIA 显卡。
    /// </summary>
    /// <returns>支持时为 true。</returns>
    public bool IsSupported()
    {
        try
        {
            NVAPI.Initialize();
            return NVAPI.GetGPU() is not null;
        }
        catch
        {
            return false;
        }
        finally
        {
            try
            {
                NVAPI.Unload();
            }
            catch { /* Ignored. */ }
        }
    }

    /// <summary>
    /// 读取最近一次刷新得到的 GPU 状态（不触发新的查询）。
    /// </summary>
    public async Task<GPUState> GetLastKnownStateAsync()
    {
        using (await _lock.LockAsync().ConfigureAwait(false))
            return _state;
    }

    /// <summary>
    /// 立即同步刷新一次状态并返回结果。
    /// </summary>
    public async Task<GPUStatus> RefreshNowAsync()
    {
        using (await _lock.LockAsync().ConfigureAwait(false))
        {
            // delay/interval 传 0 表示只执行一轮，不做循环等待
            await RefreshLoopAsync(0, 0, CancellationToken.None).ConfigureAwait(false);
            return new GPUStatus(_state, _performanceState, _processes);
        }
    }

    /// <summary>
    /// 启动后台刷新循环。
    /// </summary>
    /// <param name="delay">首次刷新前的延迟（毫秒），默认 1 秒。</param>
    /// <param name="interval">两次刷新之间的间隔（毫秒），默认 5 秒。</param>
    public Task StartAsync(int delay = 1_000, int interval = 5_000)
    {
        if (IsStarted)
            return Task.CompletedTask;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Starting... [delay={delay}, interval={interval}]");

        _refreshCancellationTokenSource = new CancellationTokenSource();
        var token = _refreshCancellationTokenSource.Token;
        _refreshTask = Task.Run(() => RefreshLoopAsync(delay, interval, token), token);
        return Task.CompletedTask;
    }

    /// <summary>
    /// 停止后台刷新循环。
    /// </summary>
    /// <param name="waitForFinish">为 true 时等待循环真正退出后再返回。</param>
    public async Task StopAsync(bool waitForFinish = false)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopping... [refreshTask.isNull={_refreshTask is null}, _refreshCancellationTokenSource.IsCancellationRequested={_refreshCancellationTokenSource?.IsCancellationRequested}]");

        if (_refreshCancellationTokenSource is not null)
            await _refreshCancellationTokenSource.CancelAsync().ConfigureAwait(false);

        if (waitForFinish)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Waiting to finish...");

            if (_refreshTask is not null)
            {
                try
                {
                    await _refreshTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException) { }
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Finished");
        }

        _refreshCancellationTokenSource = null;
        _refreshTask = null;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopped");
    }

    /// <summary>
    /// 通过 pnputil 重启独立显卡设备（相当于禁用后重新启用）。
    /// 仅在 GPU 处于 Active/Inactive 且已获得设备实例 ID 时执行。
    /// </summary>
    public async Task RestartGPUAsync()
    {
        using (await _lock.LockAsync().ConfigureAwait(false))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Deactivating... [state={_state}, gpuInstanceId={_gpuInstanceId}]");

            if (_state is not GPUState.Active and not GPUState.Inactive)
                return;

            if (string.IsNullOrEmpty(_gpuInstanceId))
                return;

            await CMD.RunAsync("pnputil", $"/restart-device \"{_gpuInstanceId}\"").ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Deactivating... [state= {_state}, gpuInstanceId={_gpuInstanceId}]");
        }
    }

    /// <summary>
    /// 结束所有正在占用独立显卡的进程（用于让独显进入休眠）。
    /// 单个进程失败仅记录日志，不影响其他进程。
    /// </summary>
    public async Task KillGPUProcessesAsync()
    {
        using (await _lock.LockAsync().ConfigureAwait(false))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Deactivating... [state= {_state}, gpuInstanceId={_gpuInstanceId}]");

            if (_state is not GPUState.Active)
                return;

            if (string.IsNullOrEmpty(_gpuInstanceId))
                return;

            foreach (var process in _processes)
            {
                try
                {
                    process.Kill(true);
                    await process.WaitForExitAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Couldn't kill process. [pid={process.Id}, name={process.ProcessName}]", ex);
                }
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Deactivating... [state=  {_state}, gpuInstanceId={_gpuInstanceId}]");
        }
    }

    // 刷新循环：初始化 NVAPI → 首次延迟 → 循环刷新（interval 为 0 时只执行一轮）
    private async Task RefreshLoopAsync(int delay, int interval, CancellationToken token)
    {
        try
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Initializing NVAPI...");

            NVAPI.Initialize();

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Initialized NVAPI");

            await Task.Delay(delay, token).ConfigureAwait(false);

            while (true)
            {
                token.ThrowIfCancellationRequested();

                using (await _lock.LockAsync(token).ConfigureAwait(false))
                {

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Will refresh...");

                    await RefreshStateAsync().ConfigureAwait(false);

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Refreshed");

                    Refreshed?.Invoke(this, new GPUStatus(_state, _performanceState, _processes));
                }

                if (interval > 0)
                    await Task.Delay(interval, token).ConfigureAwait(false);
                else
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Exception occurred", ex);

            throw;
        }
        finally
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Unloading NVAPI...");

            NVAPI.Unload();

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Unloaded NVAPI");
        }
    }

    private async Task RefreshStateAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Refresh in progress...");

        // 先复位所有字段，避免上次的残留数据在异常时造成误判
        _state = GPUState.Unknown;
        _processes = [];
        _gpuInstanceId = null;
        _performanceState = null;

        var gpu = NVAPI.GetGPU();
        if (gpu is null)
        {
            _state = GPUState.NvidiaGpuNotFound;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"GPU present [state={_state}, processes.Count={_processes.Count}, gpuInstanceId={_gpuInstanceId}]");

            return;
        }

        try
        {
            // 性能状态枚举名形如 "P0_3DPerformance"，截取第一个下划线前的部分作为展示标记
            var stateId = gpu.PerformanceStatesInfo.CurrentPerformanceState.StateId.ToString().GetUntilOrEmpty("_");
            _performanceState = Resource.GPUController_PoweredOn;
            if (!string.IsNullOrWhiteSpace(stateId))
                _performanceState += $", {stateId}";
        }
        catch (Exception ex) when (ex.Message == "NVAPI_GPU_NOT_POWERED")
        {
            // NVAPI 在独显断电时抛出该特定错误，可据此准确判定 GPU 已下电
            _state = GPUState.PoweredOff;
            _performanceState = Resource.GPUController_PoweredOff;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Powered off [state={_state}, processes.Count={_processes.Count}, gpuInstanceId={_gpuInstanceId}]");

            return;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"GPU status exception.", ex);

            _performanceState = "Unknown";
        }

        var pnpDeviceIdPart = NVAPI.GetGPUId(gpu);

        if (string.IsNullOrEmpty(pnpDeviceIdPart))
            throw new InvalidOperationException("pnpDeviceIdPart is null or empty");

        var gpuInstanceId = await WMI.Win32.PnpEntity.GetDeviceIDAsync(pnpDeviceIdPart).ConfigureAwait(false);
        var processNames = NVAPIExtensions.GetActiveProcesses(gpu);

        // 状态优先级：显示器直连独显 > 有进程占用 > 空闲
        if (NVAPI.IsDisplayConnected(gpu))
        {
            _processes = processNames;
            _state = GPUState.MonitorConnected;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace(
                    $"Monitor connected [state={_state}, processes.Count={_processes.Count}, gpuInstanceId={_gpuInstanceId}]");
        }
        else if (processNames.Count != 0)
        {
            _processes = processNames;
            _state = GPUState.Active;
            _gpuInstanceId = gpuInstanceId;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Active [state={_state}, processes.Count={_processes.Count}, gpuInstanceId={_gpuInstanceId}, pnpDeviceIdPart={pnpDeviceIdPart}]");
        }
        else
        {
            _state = GPUState.Inactive;
            _gpuInstanceId = gpuInstanceId;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Inactive [state={_state}, processes.Count={_processes.Count}, gpuInstanceId={_gpuInstanceId}]");
        }
    }
}

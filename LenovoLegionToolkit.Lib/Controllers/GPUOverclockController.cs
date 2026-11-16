using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;
using NvAPIWrapper.GPU;
using NvAPIWrapper.Native;
using NvAPIWrapper.Native.GPU;
using NvAPIWrapper.Native.GPU.Structures;

namespace LenovoLegionToolkit.Lib.Controllers;

/// <summary>
/// 显卡超频控制器：通过 NVAPI 的 P0_3D 性能状态写入核心/显存频率偏移。
/// 超频设置会被持久化，并在显卡重新上电（显示设备到达）后自动重新应用。
/// </summary>
public class GPUOverclockController
{
    private readonly GPUOverclockSettings _settings;
    private readonly VantageDisabler _vantageDisabler;
    private readonly LegionZoneDisabler _legionZoneDisabler;
    private readonly NativeWindowsMessageListener _nativeWindowsMessageListener;

    /// <summary>
    /// 超频状态发生变化时触发（应用成功、失败或参数被清除）。
    /// </summary>
    public event EventHandler? Changed;

    public GPUOverclockController(GPUOverclockSettings settings,
        VantageDisabler vantageDisabler,
        LegionZoneDisabler legionZoneDisabler,
        NativeWindowsMessageListener nativeWindowsMessageListener)
    {
        _settings = settings;
        _vantageDisabler = vantageDisabler;
        _legionZoneDisabler = legionZoneDisabler;
        _nativeWindowsMessageListener = nativeWindowsMessageListener;
        _nativeWindowsMessageListener.Changed += NativeWindowsMessageListenerOnChanged;
    }

    /// <summary>
    /// 核心频率偏移上限（MHz）。
    /// </summary>
    public static int GetMaxCoreDeltaMhz() => 500;

    /// <summary>
    /// 显存频率偏移上限（MHz），取决于显存颗粒厂商。
    /// </summary>
    public static int GetMaxMemoryDeltaMhz()
    {
        try
        {
            NVAPI.Initialize();
            return GetMaxMemoryDeltaMhz(NVAPI.GetGPU());
        }
        finally
        {
            try { NVAPI.Unload(); } catch { /* Ignored */ }
        }
    }

    /// <summary>
    /// 判断是否支持超频：需同时满足 NVAPI 可用与固件声明支持。
    /// 固件不支持时会顺带清空历史超频设置，避免残留配置影响后续。
    /// </summary>
    public async Task<bool> IsSupportedAsync()
    {
        bool isSupported;

        try
        {
            NVAPI.Initialize();
            isSupported = NVAPI.GetGPU() is not null;
        }
        catch
        {
            isSupported = false;
        }
        finally
        {
            try { NVAPI.Unload(); } catch { /* Ignored */ }
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"NVAPI status: {isSupported}.");

        if (!isSupported)
            return isSupported;

        try
        {
            isSupported = await WMI.LenovoGameZoneData.IsSupportGpuOCAsync().ConfigureAwait(false) > 0;

            if (!isSupported)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Clearing settings...");

                _settings.Store.Enabled = false;
                _settings.Store.Info = GPUOverclockInfo.Zero;
                _settings.SynchronizeStore();
            }
        }
        catch
        {
            isSupported = false;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Supports GPU OC status: {isSupported}");

        return isSupported;
    }

    /// <summary>
    /// 读取已保存的超频状态（是否启用与偏移量）。
    /// </summary>
    public (bool, GPUOverclockInfo) GetState() => (_settings.Store.Enabled, _settings.Store.Info);

    /// <summary>
    /// 保存超频状态到设置存储。
    /// </summary>
    /// <param name="enabled">是否启用超频。</param>
    /// <param name="info">频率偏移量。</param>
    public void SaveState(bool enabled, GPUOverclockInfo info)
    {
        _settings.Store.Enabled = enabled;
        _settings.Store.Info = info;
        _settings.SynchronizeStore();
    }

    /// <summary>
    /// 把当前保存的超频设置应用到显卡。
    /// </summary>
    /// <param name="force">为 true 时即使设置为禁用也会以当前偏移量强制应用。</param>
    public async Task ApplyStateAsync(bool force = false)
    {
        // 官方软件运行时会覆盖 NVAPI 设置，此时直接放弃并通知状态变化
        if (await _vantageDisabler.GetStatusAsync().ConfigureAwait(false) == SoftwareStatus.Enabled)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Can't correctly apply state when Vantage is running.");

            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (await _legionZoneDisabler.GetStatusAsync().ConfigureAwait(false) == SoftwareStatus.Enabled)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Can't correctly apply state when Legion Zone is running.");

            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        var enabled = _settings.Store.Enabled;
        var info = _settings.Store.Info;

        if (force)
        {
            info = enabled ? info : GPUOverclockInfo.Zero;
            enabled = true;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Forcing... [enabled=true, info={info}]");
        }

        if (!enabled)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Not enabled.");

            Changed?.Invoke(this, EventArgs.Empty);

            return;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Applying overclock: {info}.");

        try
        {
            NVAPI.Initialize();

            var gpu = NVAPI.GetGPU();
            if (gpu is null)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"dGPU not found.");

                Changed?.Invoke(this, EventArgs.Empty);

                return;
            }

            SetOverclockInfo(gpu, info);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Applied overclock: {info}, current: {GetOverclockInfo(gpu)}.");
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to apply overclock: {info}, clearing settings...", ex);

            _settings.Store.Enabled = false;
            _settings.Store.Info = GPUOverclockInfo.Zero;
            _settings.SynchronizeStore();
        }
        finally
        {
            Changed?.Invoke(this, EventArgs.Empty);

            try { NVAPI.Unload(); } catch { /* Ignored */ }
        }
    }

    /// <summary>
    /// 若超频处于启用状态则重新应用一次，返回是否执行了应用。
    /// </summary>
    public async Task<bool> EnsureOverclockIsAppliedAsync()
    {
        var (enabled, _) = GetState();
        if (!enabled)
            return false;

        await ApplyStateAsync().ConfigureAwait(false);
        return true;
    }

    // 显卡重新上电/显示设备到达后，NVAPI 的超频设置会丢失，需要重新写入
    private async void NativeWindowsMessageListenerOnChanged(object? sender, NativeWindowsMessageListener.ChangedEventArgs e)
    {
        if (e.Message != NativeWindowsMessage.OnDisplayDeviceArrival)
            return;

        if (await IsSupportedAsync().ConfigureAwait(false))
            await ApplyStateAsync().ConfigureAwait(false);
    }

    // 三星显存颗粒通常可承受更大偏移，其他厂商保守使用 750MHz
    private static int GetMaxMemoryDeltaMhz(PhysicalGPU? gpu) => gpu?.MemoryInformation.RAMMaker switch
    {
        GPUMemoryMaker.Samsung => 1500,
        _ => 750
    };

    // 仅设置 P0_3D（3D 性能状态）的时钟偏移；NVAPI 的偏移单位为 kHz，故乘以 1000
    private static void SetOverclockInfo(PhysicalGPU gpu, GPUOverclockInfo info)
    {
        var coreDelta = Math.Clamp(info.CoreDeltaMhz, 0, GetMaxCoreDeltaMhz());
        var memoryDelta = Math.Clamp(info.MemoryDeltaMhz, 0, GetMaxMemoryDeltaMhz(gpu));

        var clockEntries = new[]
        {
            new PerformanceStates20ClockEntryV1(PublicClockDomain.Graphics, new PerformanceStates20ParameterDelta(coreDelta * 1000)),
            new PerformanceStates20ClockEntryV1(PublicClockDomain.Memory, new PerformanceStates20ParameterDelta(memoryDelta * 1000))
        };
        var voltageEntries = Array.Empty<PerformanceStates20BaseVoltageEntryV1>();
        var performanceStateInfo = new[] { new PerformanceStates20InfoV1.PerformanceState20(PerformanceStateId.P0_3DPerformance, clockEntries, voltageEntries) };

        var overclock = new PerformanceStates20InfoV1(performanceStateInfo, 2, 0);
        GPUApi.SetPerformanceStates20(gpu.Handle, overclock);
    }

    // 读回当前偏移：索引 0 为核心时钟，索引 1 为显存时钟；kHz 换算为 MHz
    private static GPUOverclockInfo GetOverclockInfo(PhysicalGPU gpu)
    {
        var states = GPUApi.GetPerformanceStates20(gpu.Handle);
        var core = states.Clocks[PerformanceStateId.P0_3DPerformance][0].FrequencyDeltaInkHz.DeltaValue / 1000;
        var memory = states.Clocks[PerformanceStateId.P0_3DPerformance][1].FrequencyDeltaInkHz.DeltaValue / 1000;
        return new(core, memory);
    }
}

using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;
using NvAPIWrapper.Native;
using NvAPIWrapper.Native.GPU;
using Windows.Win32;
using Windows.Win32.System.Power;

namespace LenovoLegionToolkit.Lib.Controllers.Sensors;

/// <summary>
/// 传感器控制器基类：CPU 数据来自 Windows 性能计数器 + NT 电源信息，
/// GPU 数据来自 NVIDIA NVAPI；各机型差异化的温度/风扇读取由子类实现。
/// </summary>
public abstract class AbstractSensorsController(GPUController gpuController) : ISensorsController
{
    /// <summary>
    /// NVAPI 一次性读取的 GPU 指标集合。
    /// </summary>
    private readonly struct GPUInfo(
        int utilization,
        int coreClock,
        int maxCoreClock,
        int memoryClock,
        int maxMemoryClock,
        int temperature,
        int maxTemperature)
    {
        public static readonly GPUInfo Empty = new(-1, -1, -1, -1, -1, -1, -1); // 全部 -1，表示不可用

        public int Utilization { get; } = utilization; // 占用率（取 GPU 与视频引擎的较大值）
        public int CoreClock { get; } = coreClock; // 当前核心频率（MHz）
        public int MaxCoreClock { get; } = maxCoreClock; // 加速核心频率 + 超频偏移（MHz）
        public int MemoryClock { get; } = memoryClock; // 当前显存频率（MHz）
        public int MaxMemoryClock { get; } = maxMemoryClock; // 加速显存频率 + 超频偏移（MHz）
        public int Temperature { get; } = temperature; // 当前温度（摄氏度）
        public int MaxTemperature { get; } = maxTemperature; // 默认温度上限（摄氏度）
    }

    // Windows 性能计数器：用"处理器性能百分比"推算实时频率，用"处理器实用率"作为 CPU 占用率
    private readonly SafePerformanceCounter _percentProcessorPerformanceCounter = new("Processor Information", "% Processor Performance", "_Total");
    private readonly SafePerformanceCounter _percentProcessorUtilityCounter = new("Processor Information", "% Processor Utility", "_Total");

    // 以下为只读一次的静态/上限值缓存，避免每次刷新都调用 WMI
    private int? _cpuBaseClockCache;
    private int? _cpuMaxCoreClockCache;
    private int? _cpuMaxFanSpeedCache;
    private int? _gpuMaxFanSpeedCache;

    public abstract Task<bool> IsSupportedAsync();

    /// <summary>
    /// 重置性能计数器采样基线，保证第一次 NextValue 就有有效数据。
    /// </summary>
    public Task PrepareAsync()
    {
        _percentProcessorPerformanceCounter.Reset();
        _percentProcessorUtilityCounter.Reset();
        return Task.CompletedTask;
    }

    /// <summary>
    /// 汇总读取 CPU/GPU 全部指标；不可用的项以 -1 或通用上限填充。
    /// </summary>
    public async Task<SensorsData> GetDataAsync()
    {
        // 占用率与温度使用通用上限 100（不同机型无更精确的上限数据）
        const int genericMaxUtilization = 100;
        const int genericMaxTemperature = 100;

        var cpuUtilization = GetCpuUtilization(genericMaxUtilization);
        var cpuMaxCoreClock = _cpuMaxCoreClockCache ??= await GetCpuMaxCoreClockAsync().ConfigureAwait(false);
        var cpuCoreClock = GetCpuCoreClock();
        var cpuCurrentTemperature = await GetCpuCurrentTemperatureAsync().ConfigureAwait(false);
        var cpuCurrentFanSpeed = await GetCpuCurrentFanSpeedAsync().ConfigureAwait(false);
        var cpuMaxFanSpeed = _cpuMaxFanSpeedCache ??= await GetCpuMaxFanSpeedAsync().ConfigureAwait(false);

        var gpuInfo = await GetGPUInfoAsync().ConfigureAwait(false);
        // NVAPI 不可用时回退到 WMI 读取 GPU 温度
        var gpuCurrentTemperature = gpuInfo.Temperature >= 0 ? gpuInfo.Temperature : await GetGpuCurrentTemperatureAsync().ConfigureAwait(false);
        var gpuMaxTemperature = gpuInfo.MaxTemperature >= 0 ? gpuInfo.MaxTemperature : genericMaxTemperature;
        var gpuCurrentFanSpeed = await GetGpuCurrentFanSpeedAsync().ConfigureAwait(false);
        var gpuMaxFanSpeed = _gpuMaxFanSpeedCache ??= await GetGpuMaxFanSpeedAsync().ConfigureAwait(false);

        // CPU 无显存频率概念，对应两项固定为 -1
        var cpu = new SensorData(cpuUtilization,
            genericMaxUtilization,
            cpuCoreClock,
            cpuMaxCoreClock,
            -1,
            -1,
            cpuCurrentTemperature,
            genericMaxTemperature,
            cpuCurrentFanSpeed,
            cpuMaxFanSpeed);
        var gpu = new SensorData(gpuInfo.Utilization,
            genericMaxUtilization,
            gpuInfo.CoreClock,
            gpuInfo.MaxCoreClock,
            gpuInfo.MemoryClock,
            gpuInfo.MaxMemoryClock,
            gpuCurrentTemperature,
            gpuMaxTemperature,
            gpuCurrentFanSpeed,
            gpuMaxFanSpeed);
        var result = new SensorsData(cpu, gpu);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Current data: {result} [type={GetType().Name}]");

        return result;
    }

    /// <summary>
    /// 仅读取 CPU/GPU 风扇转速，用于风扇转速相关的逻辑（避免读取全部传感器）。
    /// </summary>
    public async Task<(int cpuFanSpeed, int gpuFanSpeed)> GetFanSpeedsAsync()
    {
        var cpuFanSpeed = await GetCpuCurrentFanSpeedAsync().ConfigureAwait(false);
        var gpuFanSpeed = await GetGpuCurrentFanSpeedAsync().ConfigureAwait(false);
        return (cpuFanSpeed, gpuFanSpeed);
    }

    /// <summary>
    /// 读取 CPU 当前温度（摄氏度），不可用时返回 -1。
    /// </summary>
    protected abstract Task<int> GetCpuCurrentTemperatureAsync();

    /// <summary>
    /// 读取 GPU 当前温度（摄氏度），不可用时返回 -1。
    /// </summary>
    protected abstract Task<int> GetGpuCurrentTemperatureAsync();

    /// <summary>
    /// 读取 CPU 当前风扇转速（RPM）。
    /// </summary>
    protected abstract Task<int> GetCpuCurrentFanSpeedAsync();

    /// <summary>
    /// 读取 GPU 当前风扇转速（RPM）。
    /// </summary>
    protected abstract Task<int> GetGpuCurrentFanSpeedAsync();

    /// <summary>
    /// 读取 CPU 风扇最大转速（RPM），用作百分比计算的分母。
    /// </summary>
    protected abstract Task<int> GetCpuMaxFanSpeedAsync();

    /// <summary>
    /// 读取 GPU 风扇最大转速（RPM），用作百分比计算的分母。
    /// </summary>
    protected abstract Task<int> GetGpuMaxFanSpeedAsync();

    // CPU 占用率：直接取性能计数器，负数（计数器异常）返回 -1，并钳制到上限
    private int GetCpuUtilization(int maxUtilization)
    {
        var result = (int)_percentProcessorUtilityCounter.NextValue();
        if (result < 0)
            return -1;
        return Math.Min(result, maxUtilization);
    }

    // 实时主频 ≈ 基准频率 × (性能百分比 / 100)
    private int GetCpuCoreClock()
    {
        var baseClock = _cpuBaseClockCache ??= GetCpuBaseClock();
        var clock = (int)(baseClock * (_percentProcessorPerformanceCounter.NextValue() / 100f));
        if (clock < 1)
            return -1;
        return clock;
    }

    // 通过 NtPowerInformation(ProcessorInformation) 读取各逻辑处理器的最大频率，取最大值作为基准频率
    private static unsafe int GetCpuBaseClock()
    {
        var ptr = IntPtr.Zero;
        try
        {
            PInvoke.GetSystemInfo(out var systemInfo);

            // 最多只查询 32 个处理器，既覆盖常见配置又限制缓冲区大小
            var numberOfProcessors = Math.Min(32, (int)systemInfo.dwNumberOfProcessors);
            var infoSize = Marshal.SizeOf<PROCESSOR_POWER_INFORMATION>();
            var infosSize = numberOfProcessors * infoSize;

            ptr = Marshal.AllocHGlobal(infosSize);

            var result = PInvoke.CallNtPowerInformation(POWER_INFORMATION_LEVEL.ProcessorInformation,
                null,
                0,
                ptr.ToPointer(),
                (uint)infosSize);
            if (result != 0)
                return 0;

            var infos = new PROCESSOR_POWER_INFORMATION[numberOfProcessors];

            for (var i = 0; i < infos.Length; i++)
                infos[i] = Marshal.PtrToStructure<PROCESSOR_POWER_INFORMATION>(IntPtr.Add(ptr, i * infoSize));

            return (int)infos.Select(p => p.MaxMhz).Max();
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static Task<int> GetCpuMaxCoreClockAsync() => WMI.LenovoGameZoneData.GetCPUFrequencyAsync();

    // 通过 NVAPI 读取 GPU 占用、频率、温度；独显断电或初始化失败时返回空数据
    private async Task<GPUInfo> GetGPUInfoAsync()
    {
        if (gpuController.IsSupported())
            await gpuController.StartAsync().ConfigureAwait(false);

        // 独显已断电时调用 NVAPI 无意义，直接返回空
        if (await gpuController.GetLastKnownStateAsync().ConfigureAwait(false) is GPUState.PoweredOff or GPUState.Unknown)
            return GPUInfo.Empty;

        try
        {
            NVAPI.Initialize();

            var gpu = NVAPI.GetGPU();
            if (gpu is null)
                return GPUInfo.Empty;

            // 占用率取 GPU 核心与视频引擎两者的较大值，避免只看核心导致偏低
            var utilization = Math.Min(100, Math.Max(gpu.UsageInformation.GPU.Percentage, gpu.UsageInformation.VideoEngine.Percentage));

            // NVAPI 频率单位为 kHz，此处换算为 MHz
            var currentCoreClock = (int)gpu.CurrentClockFrequencies.GraphicsClock.Frequency / 1000;
            var currentMemoryClock = (int)gpu.CurrentClockFrequencies.MemoryClock.Frequency / 1000;

            var maxCoreClock = (int)gpu.BoostClockFrequencies.GraphicsClock.Frequency / 1000;
            var maxMemoryClock = (int)gpu.BoostClockFrequencies.MemoryClock.Frequency / 1000;

            // P0_3D 性能状态下的频率偏移即用户超频设置，需叠加到加速频率上
            var states = GPUApi.GetPerformanceStates20(gpu.Handle);
            var maxCoreClockOffset = states.Clocks[PerformanceStateId.P0_3DPerformance][0].FrequencyDeltaInkHz.DeltaValue / 1000;
            var maxMemoryClockOffset = states.Clocks[PerformanceStateId.P0_3DPerformance][1].FrequencyDeltaInkHz.DeltaValue / 1000;

            var temperatureSensor = gpu.ThermalInformation.ThermalSensors.FirstOrDefault();
            var currentTemperature = temperatureSensor?.CurrentTemperature ?? -1;
            var maxTemperature = temperatureSensor?.DefaultMaximumTemperature ?? -1;

            return new(utilization,
                currentCoreClock,
                maxCoreClock + maxCoreClockOffset,
                currentMemoryClock,
                maxMemoryClock + maxMemoryClockOffset,
                currentTemperature,
                maxTemperature);
        }
        catch
        {
            return GPUInfo.Empty;
        }
        finally
        {
            try { NVAPI.Unload(); } catch { /* Ignored */ }
        }
    }
}

using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers.Sensors;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Integrations;

/// <summary>
/// 与 HWiNFO64 的集成：把风扇转速、电池温度等传感器数据写入 HWiNFO 的"自定义传感器"注册表项，
/// 以便在 HWiNFO 中像内置传感器一样展示这些数值。
/// </summary>
public class HWiNFOIntegration(SensorsController sensorController, IntegrationsSettings settings)
{
    private const string CUSTOM_SENSOR_HIVE = "HKEY_CURRENT_USER";
    private const string CUSTOM_SENSOR_PATH = @"Software\HWiNFO64\Sensors\Custom";
    // 自定义传感器分组名称，HWiNFO 会以此名称归类下面写入的传感器
    private const string CUSTOM_SENSOR_GROUP_NAME = "Lenovo Legion Toolkit";
    private const string SENSOR_TYPE_FAN = "Fan";
    private const string SENSOR_TYPE_TEMP = "Temp";
    private const string CPU_FAN_SENSOR_NAME = "CPU Fan";
    private const string GPU_FAN_SENSOR_NAME = "GPU Fan";
    private const string BATTERY_TEMP_SENSOR_NAME = "Battery Temperature";

    /// <summary>传感器数据的刷新间隔。</summary>
    private readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(1);

    private CancellationTokenSource? _cts;
    private Task? _refreshTask;

    /// <summary>
    /// 根据设置决定启动或停止集成：先停止旧任务，若设置未启用则直接返回，否则重新启动刷新循环。
    /// </summary>
    /// <returns>表示操作完成的任务。</returns>
    public async Task StartStopIfNeededAsync()
    {
        await StopAsync().ConfigureAwait(false);

        if (!settings.Store.HWiNFO)
            return;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Starting...");

        _cts = new();
        _refreshTask = RefreshLoopAsync(_cts.Token);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Started.");
    }

    /// <summary>
    /// 停止刷新循环并清理已写入的自定义传感器数据。
    /// </summary>
    /// <returns>表示操作完成的任务。</returns>
    public async Task StopAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopping...");

        if (_cts is not null)
            await _cts.CancelAsync().ConfigureAwait(false);

        if (_refreshTask is not null)
            await _refreshTask.ConfigureAwait(false);

        ClearValues();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopped.");
    }

    /// <summary>
    /// 刷新循环：先立即写入一次数据，之后按固定间隔持续刷新，直至取消。
    /// </summary>
    /// <param name="token">取消令牌。</param>
    /// <returns>循环结束的任务。</returns>
    private async Task RefreshLoopAsync(CancellationToken token)
    {
        try
        {
            await SetSensorValuesAsync().ConfigureAwait(false);

            while (true)
            {
                await Task.Delay(_refreshInterval, token).ConfigureAwait(false);
                await SetSensorValuesAsync(false).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to set values.", ex);
        }
    }

    /// <summary>
    /// 采集当前风扇转速与电池温度，并写入 HWiNFO 自定义传感器。
    /// </summary>
    /// <param name="firstRun">是否为首次写入；首次写入时需要同时创建传感器名称等元数据。</param>
    /// <returns>表示操作完成的任务。</returns>
    private async Task SetSensorValuesAsync(bool firstRun = true)
    {
        var (cpuFanSpeed, gpuFanSpeed) = await sensorController.GetFanSpeedsAsync().ConfigureAwait(false);
        var batteryTemp = Battery.GetBatteryTemperatureC();

        SetValue(SENSOR_TYPE_FAN, 0, CPU_FAN_SENSOR_NAME, cpuFanSpeed, firstRun);
        SetValue(SENSOR_TYPE_FAN, 1, GPU_FAN_SENSOR_NAME, gpuFanSpeed, firstRun);

        // HWiNFO 按固定的小数点格式解析数值，这里强制使用 "." 作为小数分隔符，
        // 避免在逗号小数点的区域设置下解析失败
        var batteryTempString = batteryTemp.HasValue
            ? batteryTemp.Value.ToString(new NumberFormatInfo { NumberDecimalSeparator = "." })
            : string.Empty;
        SetValue(SENSOR_TYPE_TEMP, 0, BATTERY_TEMP_SENSOR_NAME, batteryTempString, firstRun);
    }

    /// <summary>
    /// 将单个传感器值写入注册表；首次写入时还会写入传感器名称。
    /// </summary>
    /// <typeparam name="T">值类型。</typeparam>
    /// <param name="type">传感器类型（Fan/Temp）。</param>
    /// <param name="index">同类型传感器的序号。</param>
    /// <param name="name">传感器显示名称。</param>
    /// <param name="value">传感器数值。</param>
    /// <param name="firstRun">是否为首次写入，决定是否一并写入名称。</param>
    private static void SetValue<T>(string type, int index, string name, T value, bool firstRun) where T : notnull
    {
        Registry.SetValue(CUSTOM_SENSOR_HIVE,
            $@"{CUSTOM_SENSOR_PATH}\{CUSTOM_SENSOR_GROUP_NAME}\{type}{index}",
            "Value",
            value);

        if (!firstRun)
            return;

        Registry.SetValue(CUSTOM_SENSOR_HIVE,
            $@"{CUSTOM_SENSOR_PATH}\{CUSTOM_SENSOR_GROUP_NAME}\{type}{index}",
            "Name",
            name);
    }

    /// <summary>
    /// 删除本工具写入的整个自定义传感器分组。
    /// </summary>
    private static void ClearValues()
    {
        try
        {
            // 分组不存在或删除失败不应影响停止流程，因此异常仅记录日志
            Registry.Delete(CUSTOM_SENSOR_HIVE, $@"{CUSTOM_SENSOR_PATH}\{CUSTOM_SENSOR_GROUP_NAME}");
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to clear values.", ex);
        }
    }
}

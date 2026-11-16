using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;
using Windows.Win32;

namespace LenovoLegionToolkit.Lib.System;

/// <summary>
/// 电源状态查询与操作辅助类。
/// </summary>
public static class Power
{
    /// <summary>
    /// 判断电源适配器是否已连接，并区分"已连接但功率不足"的情况。
    /// </summary>
    /// <returns>适配器连接状态枚举。</returns>
    public static async Task<PowerAdapterStatus> IsPowerAdapterConnectedAsync()
    {
        // GetSystemPowerStatus 是 Win32 API；调用失败时保守地认为已连接，避免误判为断电而降频
        if (!PInvoke.GetSystemPowerStatus(out var sps))
            return PowerAdapterStatus.Connected;

        // ACLineStatus：0=离线（使用电池），1=在线（接电源），255=未知
        var adapterConnected = sps.ACLineStatus == 1;
        // 部分机型支持通过 WMI 判断适配器是否满足超频需求及是否正常充电；
        // 返回 null 表示该机型不支持查询，此时按 true 处理（不影响结果）
        var acFitForOc = await IsAcFitForOc().ConfigureAwait(false) ?? true;
        var chargingNormally = await IsChargingNormally().ConfigureAwait(false) ?? true;

        // 已连接电源但适配器功率不足/充电异常时，视为"低功率连接"，用于提示用户使用原装电源
        return (adapterConnected, acFitForOc && chargingNormally) switch
        {
            (true, false) => PowerAdapterStatus.ConnectedLowWattage,
            (true, _) => PowerAdapterStatus.Connected,
            (false, _) => PowerAdapterStatus.Disconnected,
        };
    }

    /// <summary>
    /// 判断系统"节电模式"（Battery Saver）是否开启。
    /// </summary>
    /// <returns>已开启返回 true；查询失败返回 false。</returns>
    public static bool IsBatterySaverEnabled()
    {
        if (!PInvoke.GetSystemPowerStatus(out var sps))
            return false;

        // SystemStatusFlag 为 1 表示节电模式处于激活状态
        return sps.SystemStatusFlag == 1;
    }

    /// <summary>
    /// 立即重启系统。
    /// </summary>
    public static async Task RestartAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Restarting...");

        // /r 重启，/t 0 表示无延迟
        await CMD.RunAsync("shutdown", "/r /t 0").ConfigureAwait(false);
    }

    /// <summary>
    /// 查询适配器是否满足超频（OC）供电要求。
    /// </summary>
    /// <returns>true=满足，false=不满足，null=机型不支持该 WMI 方法。</returns>
    private static async Task<bool?> IsAcFitForOc()
    {
        try
        {
            var result = await WMI.LenovoGameZoneData.IsACFitForOCAsync().ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Mode = {result}");

            return result == 1;
        }
        catch
        {
            // WMI 方法不存在（老机型/非 Legion 机型）时降级返回 null
            return null;
        }
    }

    /// <summary>
    /// 查询电池是否处于正常充电状态。
    /// </summary>
    /// <returns>true=正常，false=异常，null=机型不支持该 WMI 方法。</returns>
    private static async Task<bool?> IsChargingNormally()
    {
        try
        {
            var result = await WMI.LenovoGameZoneData.GetPowerChargeModeAsync().ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Mode = {result}");

            return result == 1;
        }
        catch
        {
            return null;
        }
    }
}

using System;
using System.Linq;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;
using ManagedNativeWifi;

namespace LenovoLegionToolkit.Lib.System;

/// <summary>
/// Wi-Fi 无线电控制辅助类。
/// </summary>
public static class WiFi
{
    /// <summary>
    /// 打开所有无线网卡的无线电（即启用 Wi-Fi）。
    /// </summary>
    public static void TurnOn()
    {
        try
        {
            // 枚举全部无线接口并逐个开启；部分机型可能没有无线网卡
            NativeWifi.EnumerateInterfaces()
            .ForEach(i => NativeWifi.TurnOnInterfaceRadio(i.Id));
        }
        catch (Exception ex)
        {
            // 无线服务未运行或无权限时静默降级，仅记录日志
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to turn on WiFi.", ex);
        }
    }

    /// <summary>
    /// 关闭所有无线网卡的无线电（即禁用 Wi-Fi）。
    /// </summary>
    public static void TurnOff()
    {
        try
        {
            NativeWifi.EnumerateInterfaces()
                .ForEach(i => NativeWifi.TurnOffInterfaceRadio(i.Id));
        }
        catch (Exception ex)
        {
            // 同 TurnOn：失败不影响主流程，仅记录日志
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to turn off WiFi.", ex);
        }
    }

    /// <summary>
    /// 获取当前已连接无线网络的 SSID。
    /// </summary>
    /// <returns>首个已连接网络的 SSID；未连接任何网络时返回 null。</returns>
    public static string? GetConnectedNetworkSsid()
    {
        return NativeWifi.EnumerateConnectedNetworkSsids()
            .Select(c => c.ToString())
            .FirstOrDefault();
    }
}

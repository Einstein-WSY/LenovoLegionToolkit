using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace LenovoLegionToolkit.Lib.PackageDownloader.Detectors.Rules;

/// <summary>
/// PnP 硬件 ID 匹配规则：本机驱动缓存中存在以该硬件 ID 开头的设备时成立。
/// </summary>
internal readonly struct PnPIdPackageRule : IPackageRule
{
    private string HardwareId { get; init; }

    /// <summary>
    /// 以节点文本作为硬件 ID 创建规则。
    /// </summary>
    /// <param name="node">_PnPID 规则节点。</param>
    /// <param name="value">创建出的规则实例。</param>
    /// <returns>节点为空时返回 false。</returns>
    public static bool TryCreate(XmlNode? node, out PnPIdPackageRule value)
    {
        var hardwareId = node?.InnerText;

        if (hardwareId is null)
        {
            value = default;
            return false;
        }

        value = new() { HardwareId = hardwareId };
        return true;
    }

    /// <summary>
    /// 依赖满足 = 驱动缓存中存在匹配该硬件 ID 的设备。
    /// </summary>
    public Task<bool> CheckDependenciesSatisfiedAsync(List<DriverInfo> driverInfoCache, HttpClient _1, CancellationToken _2)
    {
        var result = MatchingDriverInfoExists(HardwareId, driverInfoCache);
        return Task.FromResult(result);
    }

    /// <summary>
    /// 需要安装 = 驱动缓存中存在匹配该硬件 ID 的设备。
    /// </summary>
    public Task<bool> DetectInstallNeededAsync(List<DriverInfo> driverInfoCache, HttpClient _1, CancellationToken _2)
    {
        var result = MatchingDriverInfoExists(HardwareId, driverInfoCache);
        return Task.FromResult(result);
    }
    /// <summary>
    /// 在驱动缓存中查找 DeviceId 或 HardwareId 以指定硬件 ID 开头的设备。
    /// </summary>
    private static bool MatchingDriverInfoExists(string hardwareId, IEnumerable<DriverInfo> driverInfoCache)
    {
        return driverInfoCache.Any(di =>
        {
            var result = di.DeviceId.StartsWith(hardwareId, StringComparison.InvariantCultureIgnoreCase);
            result |= di.HardwareId.StartsWith(hardwareId, StringComparison.InvariantCultureIgnoreCase);
            return result;
        });
    }
}

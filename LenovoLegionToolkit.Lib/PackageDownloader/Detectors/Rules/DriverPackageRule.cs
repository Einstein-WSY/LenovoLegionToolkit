using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using LenovoLegionToolkit.Lib.Extensions;

namespace LenovoLegionToolkit.Lib.PackageDownloader.Detectors.Rules;

/// <summary>
/// 驱动匹配规则：依据硬件 ID 在本机驱动缓存中查找，并用日期/版本判断是否需要更新。
/// </summary>
internal readonly struct DriverPackageRule : IPackageRule
{
    private string[] HardwareIds { get; init; }
    private DateTime? Date { get; init; }
    private Version? Version { get; init; }

    /// <summary>
    /// 解析 HardwareID、Date 与 Version 节点创建规则。
    /// </summary>
    /// <param name="node">_Driver 规则节点。</param>
    /// <param name="value">创建出的规则实例。</param>
    /// <returns>未解析到任何硬件 ID 时返回 false。</returns>
    public static bool TryCreate(XmlNode? node, out DriverPackageRule value)
    {
        var hardwareIds = node?.SelectNodes("HardwareID")?
            .OfType<XmlNode>()
            .Select(n => n.InnerText)
            .ToArray() ?? [];
        var dateString = node?.SelectSingleNode("Date")?.InnerText;
        var versionString = node?.SelectSingleNode("Version")?.InnerText;

        DateTime? date = null;
        if (DateTime.TryParse(dateString, out var d))
            date = d;

        // 版本字符串可能带有厂商前缀，先剥离非数字字符再解析。
        Version? version = null;
        if (Version.TryParse(RemoveNonVersionCharacters(versionString), out var v))
            version = v;

        if (hardwareIds.IsEmpty())
        {
            value = default;
            return false;
        }

        value = new DriverPackageRule { HardwareIds = hardwareIds, Date = date, Version = version };
        return true;
    }

    /// <summary>
    /// 依赖满足 = 找到匹配驱动，且当前驱动尚未达到规则要求的版本。
    /// </summary>
    public Task<bool> CheckDependenciesSatisfiedAsync(List<DriverInfo> driverInfoCache, HttpClient _1, CancellationToken _2)
    {
        var driverInfo = FindMatchingDriverInfo(HardwareIds, driverInfoCache);

        if (string.IsNullOrEmpty(driverInfo.HardwareId))
            return Task.FromResult(false);

        var result = !VerifyByDateVersion(driverInfo);
        return Task.FromResult(result);
    }

    /// <summary>
    /// 需要安装 = 找到匹配驱动，且规则中的版本/日期高于本机已装驱动。
    /// </summary>
    public Task<bool> DetectInstallNeededAsync(List<DriverInfo> driverInfoCache, HttpClient _1, CancellationToken _2)
    {
        var driverInfo = FindMatchingDriverInfo(HardwareIds, driverInfoCache);

        if (string.IsNullOrEmpty(driverInfo.HardwareId))
            return Task.FromResult(false);

        var result = VerifyByDateVersion(driverInfo);
        return Task.FromResult(result);
    }

    /// <summary>
    /// 按日期与版本判断规则是否比本机驱动更新：日期更早则否，日期不早时再比较版本。
    /// </summary>
    private bool VerifyByDateVersion(DriverInfo driverInfo)
    {
        if (Date is not null && driverInfo.Date is not null && Version is not null && driverInfo.Version is not null)
        {
            if (Date < driverInfo.Date)
                return false;

            return Version > driverInfo.Version;
        }

        if (Version is not null && driverInfo.Version is not null)
        {
            var result = Version > driverInfo.Version;
            return result;
        }

        return false;
    }

    /// <summary>
    /// 仅保留版本字符串中的数字与点，便于 <see cref="Version.TryParse"/> 解析。
    /// </summary>
    private static string RemoveNonVersionCharacters(string? versionString)
    {
        var arr = versionString?.ToCharArray() ?? [];
        arr = Array.FindAll(arr, c => char.IsDigit(c) || c == '.');
        return new string(arr);
    }

    /// <summary>
    /// 在驱动缓存中查找 DeviceId 或 HardwareId 以任一硬件 ID 开头的驱动信息。
    /// </summary>
    private static DriverInfo FindMatchingDriverInfo(IEnumerable<string> hardwareIds, IEnumerable<DriverInfo> driverInfoCache)
    {
        return driverInfoCache.FirstOrDefault(di => hardwareIds.Any(hardwareId =>
        {
            var result = di.DeviceId.StartsWith(hardwareId, StringComparison.InvariantCultureIgnoreCase);
            result |= di.HardwareId.StartsWith(hardwareId, StringComparison.InvariantCultureIgnoreCase);
            return result;
        }));
    }
}

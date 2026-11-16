using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.PackageDownloader.Detectors.Rules;

/// <summary>
/// BIOS 版本匹配规则：对比本机 BIOS 的「前缀 + 版本号」与规则中的 Level 列表。
/// </summary>
internal readonly partial struct BiosPackageRule : IPackageRule
{
    // 与 Compatibility 中相同的解析方式：4 位前缀 + 2 位版本号。
    [GeneratedRegex("^[A-Z0-9]{4}")]
    private static partial Regex PrefixRegex();

    [GeneratedRegex("[0-9]{2}")]
    private static partial Regex VersionRegex();

    private string[] Levels { get; init; }

    /// <summary>
    /// 解析 Level 节点集合创建规则。
    /// </summary>
    /// <param name="node">_Bios 规则节点。</param>
    /// <param name="value">创建出的规则实例。</param>
    /// <returns>未解析到任何 Level 时返回 false。</returns>
    public static bool TryCreate(XmlNode? node, out BiosPackageRule value)
    {
        var levels = node?.SelectNodes("Level")?
            .OfType<XmlNode>()
            .Select(n => n.InnerText)
            .ToArray() ?? [];

        if (levels.IsEmpty())
        {
            value = default;
            return false;
        }

        value = new BiosPackageRule { Levels = levels };
        return true;
    }

    /// <summary>
    /// 依赖满足 = 本机 BIOS 与任一 Level 的前缀、版本号完全一致。
    /// </summary>
    public async Task<bool> CheckDependenciesSatisfiedAsync(List<DriverInfo> driverInfoCache, HttpClient httpClient, CancellationToken token)
    {
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        var currentBios = mi.BiosVersion;

        var result = Levels.Any((global::System.Func<string, bool>)(level =>
        {
            var levelPrefix = PrefixRegex().Match(level).Value;
            var levelVersion = int.Parse(VersionRegex().Match(level).Value);
            return currentBios.HasValue && levelPrefix == currentBios.Value.Prefix && levelVersion == currentBios.Value.Version;
        }));

        return result;
    }

    /// <summary>
    /// 需要安装 = 所有 Level 的前缀与本机 BIOS 相同，且其版本号高于本机 BIOS 版本。
    /// </summary>
    public async Task<bool> DetectInstallNeededAsync(List<DriverInfo> _1, HttpClient _2, CancellationToken _3)
    {
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        var currentBios = mi.BiosVersion;

        var result = Levels.All((global::System.Func<string, bool>)(level =>
        {
            var levelPrefix = PrefixRegex().Match(level).Value;
            var levelVersion = int.Parse(VersionRegex().Match(level).Value);
            return currentBios.HasValue && levelPrefix == currentBios.Value.Prefix && levelVersion > currentBios.Value.Version;
        }));

        return result;
    }
}

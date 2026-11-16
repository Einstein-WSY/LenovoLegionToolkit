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
/// 操作系统版本匹配规则：当当前系统命中规则中任一操作系统标识时成立。
/// </summary>
internal readonly struct OsPackageRule : IPackageRule
{
    private string[] Oses { get; init; }

    /// <summary>
    /// 解析 XML 中的 OS 节点集合创建规则。
    /// </summary>
    /// <param name="node">_OS 规则节点。</param>
    /// <param name="value">创建出的规则实例。</param>
    /// <returns>未解析到任何 OS 时返回 false。</returns>
    public static bool TryCreate(XmlNode? node, out OsPackageRule value)
    {
        var oses = node?.SelectNodes("OS")?
            .OfType<XmlNode>()
            .Select(n => n.InnerText)
            .ToArray() ?? [];

        if (oses.IsEmpty())
        {
            value = default;
            return false;
        }

        value = new OsPackageRule { Oses = oses };
        return true;
    }

    /// <summary>
    /// 依赖判定与安装判定均等同于系统版本匹配。
    /// </summary>
    public Task<bool> CheckDependenciesSatisfiedAsync(List<DriverInfo> _1, HttpClient _2, CancellationToken _3) => OsVersionMatch();

    /// <summary>
    /// 依赖判定与安装判定均等同于系统版本匹配。
    /// </summary>
    public Task<bool> DetectInstallNeededAsync(List<DriverInfo> _1, HttpClient _2, CancellationToken _3) => OsVersionMatch();

    /// <summary>
    /// 判断当前操作系统是否命中规则中的任一系统标识（前缀匹配，如 win11）。
    /// </summary>
    private Task<bool> OsVersionMatch()
    {
        var currentOs = OSExtensions.GetCurrent();

        var result = Oses.Any(os =>
        {
            switch (currentOs)
            {
                case OS.Windows11 when os.StartsWith("win11", StringComparison.InvariantCultureIgnoreCase):
                case OS.Windows10 when os.StartsWith("win10", StringComparison.InvariantCultureIgnoreCase):
                case OS.Windows8 when os.StartsWith("win8", StringComparison.InvariantCultureIgnoreCase):
                case OS.Windows7 when os.StartsWith("win7", StringComparison.InvariantCultureIgnoreCase):
                    return true;
                default:
                    return false;
            }
        });

        return Task.FromResult(result);
    }
}

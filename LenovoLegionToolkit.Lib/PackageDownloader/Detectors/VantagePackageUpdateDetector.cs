using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using LenovoLegionToolkit.Lib.PackageDownloader.Detectors.Rules;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.PackageDownloader.Detectors;

/// <summary>
/// Vantage 包更新检测器：依据包 XML 中的依赖与安装检测规则，结合本机驱动信息判断是否需要安装/更新。
/// </summary>
internal class VantagePackageUpdateDetector
{
    private readonly List<DriverInfo> _driverInfoCache = [];

    /// <summary>
    /// 预加载并缓存本机的 PnP 已签名驱动信息，供后续规则匹配复用，避免重复查询 WMI。
    /// </summary>
    public async Task BuildDriverInfoCache()
    {
        var driverInfo = await WMI.Win32.PnpSignedDriver.ReadAsync().ConfigureAwait(false);
        _driverInfoCache.Clear();
        _driverInfoCache.AddRange(driverInfo);
    }

    /// <summary>
    /// 判断给定包是否需要安装/更新：先确认依赖满足，再进行安装检测。
    /// </summary>
    /// <param name="httpClient">HTTP 客户端（部分规则需要联网或下载外部检测程序）。</param>
    /// <param name="document">包的 XML 文档。</param>
    /// <param name="baseLocation">包所在目录的 URL。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>需要安装时返回 true。</returns>
    public async Task<bool> DetectAsync(HttpClient httpClient, XmlDocument document, string baseLocation, CancellationToken token)
    {
        var dependenciesSatisfied = await CheckDependenciesSatisfiedAsync(httpClient, document, baseLocation, token).ConfigureAwait(false);
        if (!dependenciesSatisfied)
            return false;

        return await DetectInstallAsync(httpClient, document, baseLocation, token).ConfigureAwait(false);
    }

    /// <summary>
    /// 检查包的依赖条件是否满足；无依赖节点时视为不满足。
    /// </summary>
    private async Task<bool> CheckDependenciesSatisfiedAsync(HttpClient httpClient, XmlDocument document, string baseLocation, CancellationToken token)
    {
        var node = document.SelectSingleNode("/Package/Dependencies");
        if (node is null || !node.HasChildNodes)
            return false;

        var rules = CreateRules(node, document, baseLocation);
        if (!AndPackageRule.TryCreate(rules, out var rule))
            return false;

        var result = await rule.CheckDependenciesSatisfiedAsync(_driverInfoCache, httpClient, token).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// 执行包的安装检测规则；无检测节点时默认需要安装。
    /// </summary>
    private async Task<bool> DetectInstallAsync(HttpClient httpClient, XmlDocument document, string baseLocation, CancellationToken token)
    {
        var node = document.SelectSingleNode("/Package/DetectInstall");
        // 未定义安装检测规则时，默认认为需要安装。
        if (node is null || !node.HasChildNodes)
            return true;

        var rules = CreateRules(node, document, baseLocation);
        if (!AndPackageRule.TryCreate(rules, out var rule))
            return false;

        var result = await rule.DetectInstallNeededAsync(_driverInfoCache, httpClient, token).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// 将 XML 规则节点递归解析为对应的 <see cref="IPackageRule"/> 集合。
    /// </summary>
    /// <param name="node">规则父节点。</param>
    /// <param name="document">完整的包 XML 文档，供部分规则读取附加信息。</param>
    /// <param name="baseLocation">包所在目录的 URL。</param>
    /// <returns>解析得到的规则序列。</returns>
    private static IEnumerable<IPackageRule> CreateRules(XmlNode? node, XmlDocument document, string baseLocation)
    {
        if (node is null)
            yield break;

        for (var i = 0; i < node.ChildNodes.Count; i++)
        {
            var childNode = node.ChildNodes[i];
            if (childNode is null)
                continue;

            // 根据节点名称映射到对应规则；逻辑组合节点（And/Or/Not）会递归解析其子规则。
            switch (childNode.Name)
            {
                case "Or":
                    {
                        var rules = CreateRules(childNode, document, baseLocation);
                        if (OrPackageRule.TryCreate(rules, out var value))
                            yield return value;
                        break;
                    }
                case "And":
                    {
                        var rules = CreateRules(childNode, document, baseLocation);
                        if (AndPackageRule.TryCreate(rules, out var value))
                            yield return value;
                        break;
                    }
                case "Not":
                    {
                        var rules = CreateRules(childNode, document, baseLocation);
                        if (NotPackageRule.TryCreate(rules, out var value))
                            yield return value;
                        break;
                    }
                case "_OS":
                    {
                        if (OsPackageRule.TryCreate(childNode, out var value))
                            yield return value;
                        break;
                    }
                case "_WindowsBuildVersion":
                    {
                        if (WindowsBuildVersionPackageRule.TryCreate(childNode, out var value))
                            yield return value;
                        break;
                    }
                case "_CPUAddressWidth":
                    {
                        if (CpuAddressWidthPackageRule.TryCreate(childNode, out var value))
                            yield return value;
                        break;
                    }
                case "_ExternalDetection":
                    {
                        if (ExternalDetectionRule.TryCreate(childNode, document, baseLocation, out var value))
                            yield return value;
                        break;
                    }
                case "_Driver":
                    {
                        if (DriverPackageRule.TryCreate(childNode, out var value))
                            yield return value;
                        break;
                    }
                case "_PnPID":
                    {
                        if (PnPIdPackageRule.TryCreate(childNode, out var value))
                            yield return value;
                        break;
                    }
                case "_Bios":
                    {
                        if (BiosPackageRule.TryCreate(childNode, out var value))
                            yield return value;
                        break;
                    }
                case "_RegistryKey":
                    {
                        if (RegistryKeyPackageRule.TryCreate(childNode, out var value))
                            yield return value;
                        break;
                    }
                case "_RegistryKeyValue":
                    {
                        if (RegistryKeyValuePackageRule.TryCreate(childNode, out var value))
                            yield return value;
                        break;
                    }
                default:
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Unknown rule: {childNode.Name}.");
                        break;
                    }
            }
        }
    }
}

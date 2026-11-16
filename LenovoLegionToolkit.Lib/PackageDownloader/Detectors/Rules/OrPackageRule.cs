using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.PackageDownloader.Detectors.Rules;

/// <summary>
/// 逻辑「或」规则：任一子规则满足即判定为满足。
/// </summary>
internal readonly struct OrPackageRule : IPackageRule
{
    private IEnumerable<IPackageRule> Rules { get; init; }

    /// <summary>
    /// 从子规则集合创建「或」规则。
    /// </summary>
    /// <param name="rules">子规则集合。</param>
    /// <param name="value">创建出的规则实例。</param>
    /// <returns>始终返回 true（空集合也允许创建）。</returns>
    public static bool TryCreate(IEnumerable<IPackageRule> rules, out OrPackageRule value)
    {
        value = new OrPackageRule { Rules = rules };
        return true;
    }

    /// <summary>
    /// 任一子规则依赖满足即返回 true；遇到第一个满足即短路返回。
    /// </summary>
    public async Task<bool> CheckDependenciesSatisfiedAsync(List<DriverInfo> driverInfoCache, HttpClient httpClient, CancellationToken token)
    {
        foreach (var rule in Rules)
        {
            if (await rule.CheckDependenciesSatisfiedAsync(driverInfoCache, httpClient, token).ConfigureAwait(false))
                return true;
        }

        return false;
    }

    /// <summary>
    /// 任一子规则判定需要安装即返回 true；遇到第一个满足即短路返回。
    /// </summary>
    public async Task<bool> DetectInstallNeededAsync(List<DriverInfo> driverInfoCache, HttpClient httpClient, CancellationToken token)
    {
        foreach (var rule in Rules)
        {
            if (await rule.DetectInstallNeededAsync(driverInfoCache, httpClient, token).ConfigureAwait(false))
                return true;
        }

        return false;
    }
}

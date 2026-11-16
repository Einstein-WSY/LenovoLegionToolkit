using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.PackageDownloader.Detectors.Rules;

/// <summary>
/// 逻辑「与」规则：所有子规则均满足时才判定为满足。
/// </summary>
internal readonly struct AndPackageRule : IPackageRule
{
    private IEnumerable<IPackageRule> Rules { get; init; }

    /// <summary>
    /// 从子规则集合创建「与」规则。
    /// </summary>
    /// <param name="rules">子规则集合。</param>
    /// <param name="value">创建出的规则实例。</param>
    /// <returns>始终返回 true（空集合也允许创建）。</returns>
    public static bool TryCreate(IEnumerable<IPackageRule> rules, out AndPackageRule value)
    {
        value = new AndPackageRule { Rules = rules };
        return true;
    }

    /// <summary>
    /// 所有子规则的依赖都满足时才返回 true；遇到第一个不满足即短路返回 false。
    /// </summary>
    public async Task<bool> CheckDependenciesSatisfiedAsync(List<DriverInfo> driverInfoCache, HttpClient httpClient, CancellationToken token)
    {
        foreach (var rule in Rules)
        {
            if (!await rule.CheckDependenciesSatisfiedAsync(driverInfoCache, httpClient, token).ConfigureAwait(false))
                return false;
        }

        return true;
    }

    /// <summary>
    /// 所有子规则都判定需要安装时才返回 true；遇到第一个不需要即短路返回 false。
    /// </summary>
    public async Task<bool> DetectInstallNeededAsync(List<DriverInfo> driverInfoCache, HttpClient httpClient, CancellationToken token)
    {
        foreach (var rule in Rules)
        {
            if (!await rule.DetectInstallNeededAsync(driverInfoCache, httpClient, token).ConfigureAwait(false))
                return false;
        }

        return true;
    }
}

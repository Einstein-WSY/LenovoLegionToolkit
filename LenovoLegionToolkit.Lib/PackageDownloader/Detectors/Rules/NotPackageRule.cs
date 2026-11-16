using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.PackageDownloader.Detectors.Rules;

/// <summary>
/// 逻辑「非」规则：对单个子规则的判定结果取反。
/// </summary>
internal readonly struct NotPackageRule : IPackageRule
{
    private IPackageRule Rule { get; init; }

    /// <summary>
    /// 从子规则集合中取第一个规则创建「非」规则。
    /// </summary>
    /// <param name="rules">子规则集合。</param>
    /// <param name="value">创建出的规则实例。</param>
    /// <returns>集合为空时返回 false。</returns>
    public static bool TryCreate(IEnumerable<IPackageRule> rules, out NotPackageRule value)
    {
        var rule = rules.FirstOrDefault();
        if (rule is null)
        {
            value = default;
            return false;
        }

        value = new NotPackageRule { Rule = rule };
        return true;
    }

    /// <summary>
    /// 返回子规则依赖判定结果的反值。
    /// </summary>
    public async Task<bool> CheckDependenciesSatisfiedAsync(List<DriverInfo> driverInfoCache, HttpClient httpClient, CancellationToken token)
    {
        var result = !await Rule.CheckDependenciesSatisfiedAsync(driverInfoCache, httpClient, token).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// 返回子规则「是否需要安装」判定结果的反值。
    /// </summary>
    public async Task<bool> DetectInstallNeededAsync(List<DriverInfo> driverInfoCache, HttpClient httpClient, CancellationToken token)
    {
        var result = !await Rule.DetectInstallNeededAsync(driverInfoCache, httpClient, token).ConfigureAwait(false);
        return result;
    }
}

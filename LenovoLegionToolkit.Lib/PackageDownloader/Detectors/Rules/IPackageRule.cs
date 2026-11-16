using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.PackageDownloader.Detectors.Rules;

/// <summary>
/// 安装包检测规则：判定依赖是否满足，以及是否需要安装/更新。
/// </summary>
public interface IPackageRule
{
    /// <summary>
    /// 检查该规则的依赖条件是否已满足。
    /// </summary>
    /// <param name="driverInfoCache">本机已签名驱动信息缓存。</param>
    /// <param name="httpClient">HTTP 客户端（部分规则需要联网）。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>依赖满足时返回 true。</returns>
    Task<bool> CheckDependenciesSatisfiedAsync(List<DriverInfo> driverInfoCache, HttpClient httpClient, CancellationToken token);

    /// <summary>
    /// 判断是否需要执行安装/更新。
    /// </summary>
    /// <param name="driverInfoCache">本机已签名驱动信息缓存。</param>
    /// <param name="httpClient">HTTP 客户端（部分规则需要联网）。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>需要安装时返回 true。</returns>
    Task<bool> DetectInstallNeededAsync(List<DriverInfo> driverInfoCache, HttpClient httpClient, CancellationToken token);
}

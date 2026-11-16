using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.PackageDownloader;

/// <summary>
/// 安装包下载器抽象：定义获取安装包列表与下载单个安装包的能力。
/// </summary>
public interface IPackageDownloader
{
    /// <summary>
    /// 下载指定安装包到目标目录。
    /// </summary>
    /// <param name="package">待下载的安装包。</param>
    /// <param name="location">目标保存目录。</param>
    /// <param name="progress">下载进度回调（0-1）。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>已保存文件的完整路径。</returns>
    Task<string> DownloadPackageFileAsync(Package package, string location, IProgress<float>? progress = null, CancellationToken token = default);

    /// <summary>
    /// 获取指定机型与系统下的可用安装包列表。
    /// </summary>
    /// <param name="machineType">机型的 Machine Type 标识。</param>
    /// <param name="os">目标操作系统。</param>
    /// <param name="progress">进度回调（0-1）。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>安装包列表。</returns>
    Task<List<Package>> GetPackagesAsync(string machineType, OS os, IProgress<float>? progress = null, CancellationToken token = default);
}

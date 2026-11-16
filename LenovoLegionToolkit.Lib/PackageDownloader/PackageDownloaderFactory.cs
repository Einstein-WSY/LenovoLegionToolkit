using System;

namespace LenovoLegionToolkit.Lib.PackageDownloader;

/// <summary>
/// 安装包下载器工厂：根据来源类型返回对应的下载器实现。
/// </summary>
public class PackageDownloaderFactory(
    PCSupportPackageDownloader pcSupportPackageDownloader,
    VantagePackageDownloader vantagePackageDownloader)
{
    /// <summary>
    /// 下载源类型。
    /// </summary>
    public enum Type
    {
        /// <summary>Lenovo PCSupport 下载源。</summary>
        PCSupport,
        /// <summary>Lenovo Vantage 目录下载源。</summary>
        Vantage,
    }

    /// <summary>
    /// 获取指定类型的下载器实例。
    /// </summary>
    /// <param name="type">下载源类型。</param>
    /// <returns>对应的下载器实现。</returns>
    /// <exception cref="InvalidOperationException">类型不受支持时抛出。</exception>
    public IPackageDownloader GetInstance(Type type) => type switch
    {
        Type.PCSupport => pcSupportPackageDownloader,
        Type.Vantage => vantagePackageDownloader,
        _ => throw new InvalidOperationException(nameof(type)),
    };
}

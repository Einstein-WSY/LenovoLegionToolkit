using System.Collections.Generic;

namespace LenovoLegionToolkit.Lib.Settings;

/// <summary>
/// 驱动/软件下载器设置：持久化到 package_downloader.json，记录下载目录、过滤条件与隐藏的包。
/// </summary>
public class PackageDownloaderSettings()
    : AbstractSettings<PackageDownloaderSettings.PackageDownloaderSettingsStore>("package_downloader.json")
{
    /// <summary>
    /// package_downloader.json 的数据结构。
    /// </summary>
    public class PackageDownloaderSettingsStore
    {
        /// <summary>
        /// 安装包下载目录；为空时使用默认下载目录。
        /// </summary>
        public string? DownloadPath { get; set; }

        /// <summary>
        /// 是否只显示有更新的安装包。
        /// </summary>
        public bool OnlyShowUpdates { get; set; }

        /// <summary>
        /// 用户隐藏（不显示）的安装包标识集合。
        /// </summary>
        public HashSet<string> HiddenPackages { get; set; } = [];
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using LenovoLegionToolkit.Lib.PackageDownloader.Detectors;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.PackageDownloader;

/// <summary>
/// 从 Lenovo Vantage 更新目录（XML catalog）获取驱动的下载器。
/// </summary>
public class VantagePackageDownloader(HttpClientFactory httpClientFactory)
    : AbstractPackageDownloader(httpClientFactory)
{
    // 目录项：指向单个包定义 XML 的位置及其分类。
    private readonly struct PackageDefinition(string location, string category)
    {
        public string Location { get; } = location;
        public string Category { get; } = category;
    }

    private const string CATALOG_BASE_URL = "https://download.lenovo.com/catalog/";

    /// <summary>
    /// 获取指定机型在目标系统下的驱动安装包列表。
    /// </summary>
    /// <param name="machineType">机型 Machine Type。</param>
    /// <param name="os">目标操作系统。</param>
    /// <param name="progress">进度回调（百分比）。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>安装包列表。</returns>
    /// <exception cref="ArgumentOutOfRangeException">传入不支持的操作系统时抛出。</exception>
    /// <exception cref="UpdateCatalogNotFoundException">对应机型的目录文件不存在时抛出。</exception>
    public override async Task<List<Package>> GetPackagesAsync(string machineType, OS os, IProgress<float>? progress = null, CancellationToken token = default)
    {
        progress?.Report(0);

        // 将内部 OS 枚举映射为目录文件名中的系统后缀。
        var osString = os switch
        {
            OS.Windows11 => "win11",
            OS.Windows10 => "win10",
            OS.Windows8 => "win8",
            OS.Windows7 => "win7",
            _ => throw new ArgumentOutOfRangeException(nameof(os), os, null)
        };

        using var httpClient = HttpClientFactory.Create();

        // 目录文件命名规则：{machineType}_{osString}.xml。
        var packageDefinitions = await GetPackageDefinitionsAsync(httpClient, $"{CATALOG_BASE_URL}/{machineType}_{osString}.xml", token).ConfigureAwait(false);

        // 提前构建驱动信息缓存，用于后续逐个包判断是否需要安装。
        var updateDetector = new VantagePackageUpdateDetector();
        await updateDetector.BuildDriverInfoCache().ConfigureAwait(false);

        var count = 0;
        var totalCount = packageDefinitions.Count;

        var packages = new List<Package>();
        foreach (var packageDefinition in packageDefinitions)
        {
            var package = await GetPackage(httpClient, updateDetector, packageDefinition, token).ConfigureAwait(false);
            packages.Add(package);

            count++;

            progress?.Report((float)count / totalCount);
        }

        return packages;
    }

    /// <summary>
    /// 下载并解析目录 XML，返回其中的包定义列表。
    /// </summary>
    /// <param name="httpClient">HTTP 客户端。</param>
    /// <param name="location">目录 XML 的 URL。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>包定义列表；XML 无 package 节点时返回空列表。</returns>
    /// <exception cref="UpdateCatalogNotFoundException">目录返回 404 时抛出。</exception>
    private static async Task<List<PackageDefinition>> GetPackageDefinitionsAsync(HttpClient httpClient, string location, CancellationToken token)
    {
        string catalogString;

        try
        {
            catalogString = await httpClient.GetStringAsync(location, token).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // 404 表示该机型/系统没有对应目录，转换为更明确的异常类型。
            throw new UpdateCatalogNotFoundException(ex.Message, ex);
        }

        var document = new XmlDocument();
        document.LoadXml(catalogString);

        var packageNodes = document.SelectNodes("/packages/package");
        if (packageNodes is null)
            return [];

        var packageDefinitions = new List<PackageDefinition>();
        foreach (var packageNode in packageNodes.OfType<XmlElement>())
        {
            // XML 解析较耗时，循环中响应取消请求。
            token.ThrowIfCancellationRequested();

            var pLocation = packageNode.SelectSingleNode("location")?.InnerText;
            var pCategory = packageNode.SelectSingleNode("category")?.InnerText;

            // 缺少位置或分类的条目无法使用，直接跳过。
            if (string.IsNullOrWhiteSpace(pLocation) || string.IsNullOrWhiteSpace(pCategory))
                continue;

            packageDefinitions.Add(new(pLocation, pCategory));
        }

        return packageDefinitions;
    }

    /// <summary>
    /// 下载并解析单个包的 XML 定义，构造出完整的 <see cref="Package"/> 对象。
    /// </summary>
    /// <param name="httpClient">HTTP 客户端。</param>
    /// <param name="updateDetector">用于判定该包是否已有更新的检测器。</param>
    /// <param name="packageDefinition">包定义（位置与分类）。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>解析后的安装包对象。</returns>
    private static async Task<Package> GetPackage(HttpClient httpClient, VantagePackageUpdateDetector updateDetector, PackageDefinition packageDefinition, CancellationToken token)
    {
        var location = packageDefinition.Location;
        // 去掉最后一段文件名，得到包所在目录，用于拼接文件下载地址；location 不含 "/" 时保持原值
        var baseLocation = location.Contains("/")
            ? location.Remove(location.LastIndexOf("/", StringComparison.InvariantCultureIgnoreCase))
            : location;

        var packageString = await httpClient.GetStringAsync(location, token).ConfigureAwait(false);

        var document = new XmlDocument();
        document.LoadXml(packageString);

        var id = document.SelectSingleNode("/Package/@id")!.InnerText;
        var title = document.SelectSingleNode("/Package/Title/Desc")!.InnerText;
        var version = document.SelectSingleNode("/Package/@version")!.InnerText;
        var fileName = document.SelectSingleNode("/Package/Files/Installer/File/Name")!.InnerText;
        var fileCrc = document.SelectSingleNode("/Package/Files/Installer/File/CRC")?.InnerText;
        var fileSizeBytes = int.Parse(document.SelectSingleNode("/Package/Files/Installer/File/Size")!.InnerText);
        // 将字节转换为 MB，保留两位小数。
        var fileSize = $"{fileSizeBytes / 1024.0 / 1024.0:0.00} MB";
        var releaseDateString = document.SelectSingleNode("/Package/ReleaseDate")!.InnerText;
        var releaseDate = DateTime.Parse(releaseDateString);
        var readmeName = document.SelectSingleNode("/Package/Files/Readme/File/Name")?.InnerText;
        var readme = $"{baseLocation}/{readmeName}";
        var fileLocation = $"{baseLocation}/{fileName}";
        var rebootString = document.SelectSingleNode("/Package/Reboot/@type")!.InnerText;
        // Reboot 类型以字符串形式的整数给出，无法解析时按「无需重启」处理。
        var reboot = int.TryParse(rebootString, out var rebootInt) ? (RebootType)rebootInt : RebootType.NotRequired;

        var isUpdate = false;
        try
        {
            // 依赖/安装条件检测可能因网络或数据异常失败，此时退化为「不标记为更新」而不是中断整个列表。
            isUpdate = await updateDetector.DetectAsync(httpClient, document, baseLocation, token)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't detect update for package {id}. [title={title}, location={location}]",
                    ex);
        }

        return new()
        {
            Id = id,
            Title = title,
            Description = string.Empty,
            Version = version,
            Category = packageDefinition.Category,
            FileName = fileName,
            FileSize = fileSize,
            FileCrc = fileCrc,
            ReleaseDate = releaseDate,
            Readme = readme,
            FileLocation = fileLocation,
            IsUpdate = isUpdate,
            Reboot = reboot
        };
    }
}

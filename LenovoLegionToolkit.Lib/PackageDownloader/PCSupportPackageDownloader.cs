using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;

namespace LenovoLegionToolkit.Lib.PackageDownloader;

/// <summary>
/// 从 Lenovo PCSupport 下载驱动的下载器，解析其 JSON 目录接口获取安装包列表。
/// </summary>
public class PCSupportPackageDownloader(HttpClientFactory httpClientFactory)
    : AbstractPackageDownloader(httpClientFactory)
{
    private const string CATALOG_BASE_URL = "https://pcsupport.lenovo.com/us/en/api/v4/downloads/drivers?productId=";

    /// <summary>
    /// 获取指定机型在目标系统下的驱动安装包列表。
    /// </summary>
    /// <param name="machineType">机型 Machine Type，作为 productId 拼接进请求 URL。</param>
    /// <param name="os">目标操作系统。</param>
    /// <param name="progress">进度回调（0-1）。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>安装包列表；目录无数据时返回空列表。</returns>
    /// <exception cref="InvalidOperationException">传入不支持的操作系统时抛出。</exception>
    public override async Task<List<Package>> GetPackagesAsync(string machineType, OS os, IProgress<float>? progress = null, CancellationToken token = default)
    {
        // 将内部 OS 枚举映射为接口使用的系统名称。
        var osString = os switch
        {
            OS.Windows11 => "Windows 11",
            OS.Windows10 => "Windows 10",
            OS.Windows8 => "Windows 8",
            OS.Windows7 => "Windows 7",
            _ => throw new InvalidOperationException(nameof(os)),
        };

        using var httpClient = HttpClientFactory.Create();
        // 服务端会校验来源，需带上 PCSupport 站点作为 Referer。
        httpClient.DefaultRequestHeaders.Referrer = new Uri("https://pcsupport.lenovo.com/");

        progress?.Report(0);

        var catalogJson = await httpClient.GetStringAsync($"{CATALOG_BASE_URL}{machineType}", token).ConfigureAwait(false);
        var catalogJsonNode = JsonNode.Parse(catalogJson);
        var downloadsNode = catalogJsonNode?["body"]?["DownloadItems"]?.AsArray();

        // 目录结构缺失时视为无可用安装包。
        if (downloadsNode is null)
            return [];

        var packages = new List<Package>();
        foreach (var downloadNode in downloadsNode)
        {
            if (!IsCompatible(downloadNode, osString))
                continue;

            var package = ParsePackage(downloadNode!);
            if (package is null)
                continue;

            packages.Add(package.Value);
        }

        return packages;
    }

    /// <summary>
    /// 从单个下载项 JSON 中解析出安装包信息。
    /// </summary>
    /// <param name="downloadNode">下载项 JSON 节点。</param>
    /// <returns>解析成功返回安装包；缺少文件信息时返回 null。</returns>
    private static Package? ParsePackage(JsonNode downloadNode)
    {
        var id = downloadNode["ID"]!.ToJsonString();
        var category = downloadNode["Category"]!["Name"]!.ToString();
        var title = downloadNode["Title"]!.ToString();
        var description = downloadNode["Summary"]!.ToString();
        var version = downloadNode["SummaryInfo"]!["Version"]!.ToString();

        var filesNode = downloadNode["Files"]!.AsArray();
        // 优先选择 exe，其次 zip，最后回退到任意文件作为安装包主体。
        var mainFileNode = filesNode.FirstOrDefault(n => n!["TypeString"]!.ToString().Equals("exe", StringComparison.InvariantCultureIgnoreCase))
                           ?? filesNode.FirstOrDefault(n => n!["TypeString"]!.ToString().Equals("zip", StringComparison.InvariantCultureIgnoreCase))
                           ?? filesNode.FirstOrDefault();

        if (mainFileNode is null)
            return null;

        var fileLocation = mainFileNode["URL"]!.ToString();
        var fileName = new Uri(fileLocation).Segments.LastOrDefault("file");
        var fileSize = mainFileNode["Size"]!.ToString();
        var fileCrc = mainFileNode["SHA256"]?.ToString();
        var releaseDateUnix = long.Parse(mainFileNode["Date"]!["Unix"]!.ToString());
        var releaseDate = DateTimeOffset.FromUnixTimeMilliseconds(releaseDateUnix).DateTime;

        var readmeFileNode = filesNode.FirstOrDefault(n => n!["TypeString"]!.ToString().Equals("txt readme", StringComparison.InvariantCultureIgnoreCase))
                              ?? filesNode.FirstOrDefault(n => n!["TypeString"]!.ToString().Equals("html", StringComparison.InvariantCultureIgnoreCase));

        var readme = readmeFileNode?["URL"]?.ToString();

        return new()
        {
            Id = id,
            Title = title,
            Description = title == description ? string.Empty : description,
            Version = version,
            Category = category,
            FileName = fileName,
            FileSize = fileSize,
            FileCrc = fileCrc,
            ReleaseDate = releaseDate,
            Readme = readme,
            FileLocation = fileLocation,
        };
    }

    /// <summary>
    /// 判断下载项是否适用于目标操作系统；未声明系统列表时默认视为兼容。
    /// </summary>
    private static bool IsCompatible(JsonNode? downloadNode, string osString)
    {
        var operatingSystems = downloadNode?["OperatingSystemKeys"]?.AsArray();

        if (operatingSystems is null || operatingSystems.IsEmpty())
            return true;

        foreach (var operatingSystem in operatingSystems)
            if (operatingSystem is not null && operatingSystem.ToString().StartsWith(osString, StringComparison.CurrentCultureIgnoreCase))
                return true;

        return false;
    }
}

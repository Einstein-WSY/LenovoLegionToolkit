using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.PackageDownloader.Detectors.Rules;

/// <summary>
/// 外部检测规则：下载并执行包内附带的外置检测程序，以其退出码是否命中允许集合来判定。
/// </summary>
internal readonly struct ExternalDetectionRule : IPackageRule
{
    private const string TEMP_FOLDER_SUB_FOLDER = "external_package_detection";

    private int[] ReturnCodes { get; init; }
    private string Command { get; init; }
    private string Url { get; init; }
    private string FileName { get; init; }
    private string PackageName { get; init; }

    /// <summary>
    /// 解析检测命令、允许的返回码以及外置检测程序的文件信息并创建规则。
    /// </summary>
    /// <param name="node">_ExternalDetection 规则节点（文本为命令行，rc 属性为允许的退出码）。</param>
    /// <param name="document">完整的包 XML 文档。</param>
    /// <param name="baseLocation">包所在目录的 URL。</param>
    /// <param name="value">创建出的规则实例。</param>
    /// <returns>缺少命令、返回码、外置文件或包名时返回 false。</returns>
    public static bool TryCreate(XmlNode? node, XmlDocument document, string baseLocation, out ExternalDetectionRule value)
    {
        var command = node?.InnerText;
        // rc 属性可能包含多个以逗号分隔的退出码，过滤掉解析失败的项并去重。
        var returnCodes = node?.Attributes?.OfType<XmlAttribute>()
            .FirstOrDefault(a => a.Name == "rc")?
            .InnerText
            .Split(",")
            .Select(s => int.TryParse(s, out var result) ? result : -1)
            .Where(i => i >= 0)
            .Distinct()
            .ToArray() ?? [];
        var externalFile = document.SelectSingleNode("/Package/Files/External/File/Name")?.InnerText;
        var packageName = document.SelectSingleNode("/Package/@id")?.InnerText;

        if (command is null || returnCodes.IsEmpty() || externalFile is null || packageName is null)
        {
            value = default;
            return false;
        }

        value = new ExternalDetectionRule
        {
            Command = command,
            ReturnCodes = returnCodes,
            Url = $"{baseLocation}/{externalFile}",
            FileName = externalFile,
            PackageName = packageName
        };
        return true;
    }

    /// <summary>
    /// 依赖判定即为执行外置检测程序。
    /// </summary>
    public Task<bool> CheckDependenciesSatisfiedAsync(List<DriverInfo> _, HttpClient httpClient, CancellationToken token) => CheckExternalDependency(httpClient, token);

    /// <summary>
    /// 安装判定即为执行外置检测程序。
    /// </summary>
    public Task<bool> DetectInstallNeededAsync(List<DriverInfo> _, HttpClient httpClient, CancellationToken token) => CheckExternalDependency(httpClient, token);

    /// <summary>
    /// 确保检测程序已下载到临时目录，随后以指定参数运行并判断退出码是否在允许集合内。
    /// </summary>
    private async Task<bool> CheckExternalDependency(HttpClient httpClient, CancellationToken token)
    {
        // 检测程序缓存在临时目录下按包名区分的子目录中，已存在时无需重复下载。
        var packagePath = Path.Combine(Folders.Temp, TEMP_FOLDER_SUB_FOLDER, PackageName);
        var filePath = Path.Combine(packagePath, FileName);

        if (!Directory.Exists(packagePath))
            Directory.CreateDirectory(packagePath);

        if (!File.Exists(filePath))
        {
            await using var fileStream = File.OpenWrite(filePath);
            await httpClient.DownloadAsync(Url, fileStream, null, token).ConfigureAwait(false);
        }

        // 命令首段为可执行文件，其余段为参数。
        var executable = Command.Split(' ').FirstOrDefault();
        var arguments = string.Join(' ', Command.Split(' ').Skip(1));

        if (executable is null)
            return false;

        // 支持用 %PACKAGEPATH% 占位符引用检测程序所在目录。
        // ReSharper disable StringLiteralTypo
        if (executable.Contains("%PACKAGEPATH%"))
            executable = executable.Replace("%PACKAGEPATH%", packagePath);
        // ReSharper restore StringLiteralTypo

        // 未包含路径分隔符时，视为包目录内的相对可执行文件。
        if (!executable.Contains('\\'))
            executable = Path.Combine(packagePath, executable);

        var (exitCode, _) = await CMD.RunAsync(executable, arguments, token: token).ConfigureAwait(false);
        // 退出码命中允许集合即视为检测通过。
        var result = ReturnCodes.Contains(exitCode);
        return result;
    }
}

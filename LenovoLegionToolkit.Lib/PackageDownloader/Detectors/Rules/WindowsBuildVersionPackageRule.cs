using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using LenovoLegionToolkit.Lib.System.Management;

namespace LenovoLegionToolkit.Lib.PackageDownloader.Detectors.Rules;

/// <summary>
/// Windows 内部版本号匹配规则：要求本机系统 Build 号不低于规则值。
/// </summary>
internal readonly struct WindowsBuildVersionPackageRule : IPackageRule
{
    private int Version { get; init; }

    /// <summary>
    /// 解析版本号节点创建规则（仅保留数字与点后再取整数）。
    /// </summary>
    /// <param name="node">_WindowsBuildVersion 规则节点。</param>
    /// <param name="value">创建出的规则实例。</param>
    /// <returns>无法解析出整数版本时返回 false。</returns>
    public static bool TryCreate(XmlNode? node, out WindowsBuildVersionPackageRule value)
    {
        var versionString = node?.SelectSingleNode("AddressWidth")?.InnerText;

        if (versionString is null || !int.TryParse(RemoveNonVersionCharacters(versionString), out var version))
        {
            value = default;
            return false;
        }

        value = new WindowsBuildVersionPackageRule { Version = version };
        return true;
    }

    /// <summary>
    /// 依赖判定即为 Build 号匹配。
    /// </summary>
    public Task<bool> CheckDependenciesSatisfiedAsync(List<DriverInfo> _1, HttpClient _2, CancellationToken _3) => CheckBuildNumberAsync();

    /// <summary>
    /// 安装判定即为 Build 号匹配。
    /// </summary>
    public Task<bool> DetectInstallNeededAsync(List<DriverInfo> _1, HttpClient _2, CancellationToken _3) => CheckBuildNumberAsync();

    /// <summary>
    /// 通过 WMI 读取系统 Build 号并与规则值比较（规则值小于等于当前 Build 时成立）。
    /// </summary>
    private async Task<bool> CheckBuildNumberAsync()
    {
        var buildNumberString = await WMI.Win32.OperatingSystem.GetBuildNumberAsync().ConfigureAwait(false);
        // 解析失败时按 0 处理，从而仅当规则值也为 0 时才可能成立。
        var buildNumber = int.TryParse(buildNumberString, out var bn) ? bn : 0;
        var result = Version <= buildNumber;
        return result;
    }

    private static string RemoveNonVersionCharacters(string? versionString)
    {
        var arr = versionString?.ToCharArray() ?? [];
        arr = Array.FindAll(arr, c => char.IsDigit(c) || c == '.');
        return new string(arr);
    }
}

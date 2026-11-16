using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using LenovoLegionToolkit.Lib.System.Management;

namespace LenovoLegionToolkit.Lib.PackageDownloader.Detectors.Rules;

/// <summary>
/// CPU 地址宽度匹配规则：要求本机 CPU 地址宽度（如 64）与规则值一致。
/// </summary>
internal readonly struct CpuAddressWidthPackageRule : IPackageRule
{
    private int Value { get; init; }

    /// <summary>
    /// 解析 AddressWidth 节点创建规则。
    /// </summary>
    /// <param name="node">_CPUAddressWidth 规则节点。</param>
    /// <param name="value">创建出的规则实例。</param>
    /// <returns>无法解析出整数地址宽度时返回 false。</returns>
    public static bool TryCreate(XmlNode? node, out CpuAddressWidthPackageRule value)
    {
        var addressWidthString = node?.SelectSingleNode("AddressWidth")?.InnerText;

        if (addressWidthString is null || !int.TryParse(addressWidthString, out var addressWidth))
        {
            value = default;
            return false;
        }

        value = new CpuAddressWidthPackageRule { Value = addressWidth };
        return true;
    }

    /// <summary>
    /// 依赖判定即为 CPU 地址宽度匹配。
    /// </summary>
    public Task<bool> CheckDependenciesSatisfiedAsync(List<DriverInfo> _1, HttpClient _2, CancellationToken _3) => CheckCpuAddressWidthAsync();

    /// <summary>
    /// 安装判定即为 CPU 地址宽度匹配。
    /// </summary>
    public Task<bool> DetectInstallNeededAsync(List<DriverInfo> _1, HttpClient _2, CancellationToken _3) => CheckCpuAddressWidthAsync();

    /// <summary>
    /// 通过 WMI 读取本机 CPU 地址宽度并与规则值比较。
    /// </summary>
    private async Task<bool> CheckCpuAddressWidthAsync()
    {
        var addressWidth = await WMI.Win32.Processor.GetAddressWidthAsync().ConfigureAwait(false);
        var result = Value == addressWidth;
        return result;
    }
}

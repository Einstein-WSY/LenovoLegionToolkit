using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using LenovoLegionToolkit.Lib.System;

namespace LenovoLegionToolkit.Lib.PackageDownloader.Detectors.Rules;

/// <summary>
/// 注册表键存在性规则：指定注册表键存在时成立。
/// </summary>
internal readonly struct RegistryKeyPackageRule : IPackageRule
{
    private string Key { get; init; }

    /// <summary>
    /// 解析 Key 节点创建规则。
    /// </summary>
    /// <param name="node">_RegistryKey 规则节点。</param>
    /// <param name="value">创建出的规则实例。</param>
    /// <returns>缺少 Key 节点时返回 false。</returns>
    public static bool TryCreate(XmlNode? node, out RegistryKeyPackageRule value)
    {
        var key = node?.SelectSingleNode("Key")?.InnerText;

        if (key is null)
        {
            value = default;
            return false;
        }

        value = new RegistryKeyPackageRule { Key = key };
        return true;
    }

    /// <summary>
    /// 依赖满足 = 注册表键存在。
    /// </summary>
    public Task<bool> CheckDependenciesSatisfiedAsync(List<DriverInfo> _1, HttpClient _2, CancellationToken _3) => KeyExists();

    /// <summary>
    /// 需要安装 = 注册表键存在。
    /// </summary>
    public Task<bool> DetectInstallNeededAsync(List<DriverInfo> _1, HttpClient _2, CancellationToken _3) => KeyExists();

    /// <summary>
    /// 将完整键路径拆分为根键（hive）与子路径，并检查其是否存在。
    /// </summary>
    private Task<bool> KeyExists()
    {
        var hive = Key.Split('\\').FirstOrDefault();
        var path = string.Join('\\', Key.Split('\\').Skip(1));

        if (hive is null || string.IsNullOrEmpty(path))
            return Task.FromResult(false);

        var result = Registry.KeyExists(hive, path);
        return Task.FromResult(result);
    }
}

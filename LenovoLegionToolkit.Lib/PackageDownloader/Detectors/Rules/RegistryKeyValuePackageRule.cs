using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using LenovoLegionToolkit.Lib.System;

namespace LenovoLegionToolkit.Lib.PackageDownloader.Detectors.Rules;

/// <summary>
/// 注册表键值版本规则：读取指定注册表值作为版本号，并与规则版本比较。
/// </summary>
internal readonly struct RegistryKeyValuePackageRule : IPackageRule
{
    private string Key { get; init; }
    private string KeyName { get; init; }
    private Version Version { get; init; }

    /// <summary>
    /// 解析 Key、KeyName 与 Version 节点创建规则。
    /// </summary>
    /// <param name="node">_RegistryKeyValue 规则节点。</param>
    /// <param name="value">创建出的规则实例。</param>
    /// <returns>任一必需字段缺失或版本无法解析时返回 false。</returns>
    public static bool TryCreate(XmlNode? node, out RegistryKeyValuePackageRule value)
    {
        var key = node?.SelectSingleNode("Key")?.InnerText;
        var keyName = node?.SelectSingleNode("KeyName")?.InnerText;
        var versionString = node?.SelectSingleNode("Version")?.InnerText;


        Version? version = null;
        if (Version.TryParse(RemoveNonVersionCharacters(versionString), out var v))
            version = v;

        if (key is null || keyName is null || version is null)
        {
            value = default;
            return false;
        }

        value = new RegistryKeyValuePackageRule { Key = key, KeyName = keyName, Version = version };
        return true;
    }

    /// <summary>
    /// 依赖满足 = 键值不存在（可视为可安装），或已安装版本不高于规则版本。
    /// </summary>
    public Task<bool> CheckDependenciesSatisfiedAsync(List<DriverInfo> _1, HttpClient _2, CancellationToken _3)
    {
        var hive = Key.Split('\\').FirstOrDefault();
        var path = string.Join('\\', Key.Split('\\').Skip(1));

        if (hive is null || string.IsNullOrEmpty(path))
            return Task.FromResult(false);

        var keyExists = Registry.ValueExists(hive, path, KeyName);
        // 值不存在表示尚未安装，依赖视为满足。
        if (!keyExists)
            return Task.FromResult(true);

        var versionString = Registry.GetValue(hive, path, KeyName, string.Empty);

        if (!Version.TryParse(versionString, out var version))
            return Task.FromResult(false);

        // 已装版本不高于规则版本时，依赖条件成立。
        var result = Version <= version;
        return Task.FromResult(result);
    }

    /// <summary>
    /// 需要安装 = 键值存在（已安装），且已安装版本低于规则版本。
    /// </summary>
    public Task<bool> DetectInstallNeededAsync(List<DriverInfo> _1, HttpClient _2, CancellationToken _3)
    {
        var hive = Key.Split('\\').FirstOrDefault();
        var path = string.Join('\\', Key.Split('\\').Skip(1));

        if (hive is null || string.IsNullOrEmpty(path))
            return Task.FromResult(false);

        var keyExists = Registry.ValueExists(hive, path, KeyName);
        if (!keyExists)
            return Task.FromResult(false);

        var versionString = Registry.GetValue(hive, path, KeyName, string.Empty);

        // 某些注册表值带额外文本（如 "1.2.3 (Build 4)"），只取第一个空白前的部分。
        if (versionString.Any(char.IsWhiteSpace))
            versionString = versionString.Split(null).FirstOrDefault(versionString);

        if (!Version.TryParse(versionString, out var version))
            return Task.FromResult(false);

        var result = Version > version;
        return Task.FromResult(result);
    }

    /// <summary>
    /// 仅保留版本字符串中的数字与点，便于 <see cref="Version.TryParse"/> 解析。
    /// </summary>
    private static string RemoveNonVersionCharacters(string? versionString)
    {
        var arr = versionString?.ToCharArray() ?? [];
        arr = Array.FindAll(arr, c => char.IsDigit(c) || c == '.');
        return new string(arr);
    }
}

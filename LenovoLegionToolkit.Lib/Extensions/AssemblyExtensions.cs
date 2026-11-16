using System;
using System.Globalization;
using System.Reflection;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供程序集相关的扩展方法，主要用于从程序集信息中解析构建时间。
/// </summary>
public static class AssemblyExtensions
{
    /// <summary>
    /// 从程序集的 InformationalVersion 中解析构建日期时间。
    /// <para>
    /// InformationalVersion 的格式通常为 "版本号+buildyyyyMMddHHmmss"（由 CI 打包时追加），
    /// 本方法查找 "+build" 前缀并解析其后 14 位的日期时间戳。
    /// </para>
    /// </summary>
    /// <param name="assembly">目标程序集。</param>
    /// <returns>解析出的构建时间（UTC 语义由打包方决定）；若版本信息缺失或无法解析则返回 <c>null</c>。</returns>
    public static DateTime? GetBuildDateTime(this Assembly assembly)
    {
        // InformationalVersion 中标记构建时间戳的前缀，形如 "1.0.0+build20240101120000"
        const string buildVersionMetadataPrefix = "+build";

        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        if (attribute?.InformationalVersion is null)
            return null;

        var value = attribute.InformationalVersion;
        var index = value.IndexOf(buildVersionMetadataPrefix, StringComparison.InvariantCultureIgnoreCase);
        if (index <= 0)
            return null;

        value = value[(index + buildVersionMetadataPrefix.Length)..];

        // ReSharper disable once StringLiteralTypo
        if (DateTime.TryParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
            return result;

        return null;
    }

    /// <summary>
    /// 获取程序集的构建时间字符串，格式为 "yyyyMMddHHmmss"。
    /// </summary>
    /// <param name="assembly">目标程序集。</param>
    /// <returns>构建时间字符串；若无法解析则返回 <c>null</c>。</returns>
    public static string? GetBuildDateTimeString(this Assembly assembly)
    {
        // ReSharper disable once StringLiteralTypo
        return GetBuildDateTime(assembly)?.ToString("yyyyMMddHHmmss");
    }
}

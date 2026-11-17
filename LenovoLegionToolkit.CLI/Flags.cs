using System.Collections.Generic;
using System.Linq;

namespace LenovoLegionToolkit.CLI;

/// <summary>
/// 从启动参数中解析出的命令行标志，用于主程序以静默模式启动或直接执行快捷操作。
/// </summary>
public readonly struct Flags
{
    /// <summary>是否以静默模式启动（不显示主窗口）。</summary>
    public bool Silent { get; }

    /// <summary>启动后要执行的快捷操作名称；未指定时为 <see langword="null"/>。</summary>
    public string? QuickActionRunName { get; }

    /// <summary>是否请求显示帮助。</summary>
    public bool Help { get; }

    /// <summary>
    /// 安全地解析启动参数，任何解析异常都会退化为默认值（全部为 <see langword="false"/>/<see langword="null"/>）。
    /// </summary>
    /// <param name="startupArgs">进程启动参数。</param>
    /// <returns>解析得到的标志集合。</returns>
    public static Flags Create(IEnumerable<string> startupArgs)
    {
        try
        {
            return new(startupArgs);
        }
        catch
        {
            return default;
        }
    }

    /// <summary>
    /// 解析启动参数。字符串型参数既支持长形式也支持短形式别名。
    /// </summary>
    /// <param name="startupArgs">进程启动参数。</param>
    private Flags(IEnumerable<string> startupArgs)
    {
        var args = startupArgs.ToArray();

        QuickActionRunName = StringValue(args, "--quickAction") ?? StringValue(args, "-qa");
        Silent = BoolValue(args, "--silent") || BoolValue(args, "-s");
        Help = BoolValue(args, "--help") || BoolValue(args, "-h");
    }

    private static bool BoolValue(IEnumerable<string> values, string key) => values.Contains(key);

    /// <summary>
    /// 取出形如 <c>key=value</c> 的参数值。
    /// </summary>
    /// <param name="values">启动参数集合。</param>
    /// <param name="key">参数名（含前缀，如 <c>--quickAction</c>）。</param>
    /// <returns>等号后的内容；未找到匹配项时返回 <see langword="null"/>。</returns>
    private static string? StringValue(IEnumerable<string> values, string key)
    {
        // 要求 key 后紧跟 '='，避免 "-qa" 误匹配 "-qa2=..." 这类更长参数名；
        // 同时保证后续截取不会越界。
        var value = values.FirstOrDefault(s => s.StartsWith(key + '='));
        return value is null ? null : value[(key.Length + 1)..];
    }
}

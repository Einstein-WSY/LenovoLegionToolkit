using System;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供 <see cref="string"/> 的扩展方法。
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// 返回字符串中位于指定标记之前的部分；若标记不存在或位于开头则返回空字符串。
    /// </summary>
    /// <param name="text">源字符串。</param>
    /// <param name="stopAt">截断标记。</param>
    /// <returns>标记之前的子串，或空字符串。</returns>
    public static string GetUntilOrEmpty(this string text, string stopAt)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var charLocation = text.IndexOf(stopAt, StringComparison.Ordinal);
        // 仅当标记位置大于 0 时才截取（位置为 0 说明开头就是标记，结果无意义）
        if (charLocation > 0)
            return text[..charLocation];

        return string.Empty;
    }
}

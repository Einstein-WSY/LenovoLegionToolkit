using System;
using System.Collections.Generic;
using System.Linq;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供 <see cref="IEnumerable{T}"/> 的通用扩展方法。
/// </summary>
public static class EnumerableExtensions
{
    /// <summary>
    /// 判断序列是否为空。
    /// <para>
    /// 对 <see cref="ICollection{T}"/> 直接读取 <c>Count</c>，避免无谓的枚举；
    /// 其他类型则退化为 <c>Any()</c>（仅探测第一个元素）。
    /// </para>
    /// </summary>
    /// <typeparam name="T">元素类型。</typeparam>
    /// <param name="source">源序列。</param>
    /// <returns>序列为空时返回 <c>true</c>。</returns>
    public static bool IsEmpty<T>(this IEnumerable<T> source)
    {
        if (source is ICollection<T> collection)
            return collection.Count == 0;
        return !source.Any();
    }

    /// <summary>
    /// 对序列中的每个元素执行指定操作。
    /// </summary>
    /// <typeparam name="T">元素类型。</typeparam>
    /// <param name="enumeration">源序列。</param>
    /// <param name="action">要执行的操作。</param>
    public static void ForEach<T>(this IEnumerable<T> enumeration, Action<T> action)
    {
        foreach (var item in enumeration)
            action(item);
    }

    /// <summary>
    /// 将序列按指定大小切分为若干子序列（每段最多 <paramref name="size"/> 个元素）。
    /// <para>
    /// 实现方式为"每个起点取 size 个元素"的惰性投影，末尾不足一段的部分也会保留；空段会被过滤。
    /// 注意：这会对源序列做一次物化（<c>ToArray</c>），以减少重复枚举开销。
    /// </para>
    /// </summary>
    /// <typeparam name="T">元素类型。</typeparam>
    /// <param name="source">源序列。</param>
    /// <param name="size">每段的最大元素数量，必须大于 0。</param>
    /// <returns>由子序列构成的惰性序列。</returns>
    public static IEnumerable<IEnumerable<T>> Split<T>(this IEnumerable<T> source, int size)
    {
        // 若源本身已是数组则直接复用，否则先物化为数组，避免每次 Skip 都重新枚举
        var enumerable = source as T[] ?? source.ToArray();
        return enumerable
            // 对每个起点按 size 截取一段
            .Select((_, i) => enumerable.Skip(i * size).Take(size))
            // 过滤掉超出长度后产生的空段
            .Where(a => a.Any());
    }
}

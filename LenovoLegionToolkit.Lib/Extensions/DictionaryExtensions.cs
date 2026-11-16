using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供 <see cref="IDictionary{TKey,TValue}"/> 相关的扩展方法。
/// </summary>
public static class DictionaryExtensions
{
    /// <summary>
    /// 将字典包装为只读字典视图。注意包装是浅层的：底层字典变更仍会反映到只读视图中。
    /// </summary>
    /// <typeparam name="TKey">键类型。</typeparam>
    /// <typeparam name="TValue">值类型。</typeparam>
    /// <param name="source">源字典。</param>
    /// <returns>包装原字典的只读字典。</returns>
    public static ReadOnlyDictionary<TKey, TValue> AsReadOnlyDictionary<TKey, TValue>(this IDictionary<TKey, TValue> source) where TKey : notnull
    {
        return new ReadOnlyDictionary<TKey, TValue>(source);
    }

    /// <summary>
    /// 将另一个字典中的所有键值对追加到当前字典。
    /// </summary>
    /// <typeparam name="TKey">键类型。</typeparam>
    /// <typeparam name="TValue">值类型。</typeparam>
    /// <param name="source">目标字典。</param>
    /// <param name="items">要合并进来的源字典。</param>
    /// <exception cref="System.ArgumentException">目标字典中已存在相同的键。</exception>
    public static void AddRange<TKey, TValue>(this IDictionary<TKey, TValue> source, IDictionary<TKey, TValue> items)
    {
        foreach (var keyValuePair in items)
            source.Add(keyValuePair.Key, keyValuePair.Value);
    }

    /// <summary>
    /// 获取指定键的值；键不存在时返回 <c>null</c>。
    /// <para>
    /// 值类型为 <c>struct</c>，因此用可空值类型表达"未找到"，避免依赖默认值判断。
    /// </para>
    /// </summary>
    /// <typeparam name="TKey">键类型。</typeparam>
    /// <typeparam name="TValue">非可空的值类型。</typeparam>
    /// <param name="dictionary">只读字典。</param>
    /// <param name="key">要查找的键。</param>
    /// <returns>找到的值；未找到时返回 <c>null</c>。</returns>
    public static TValue? GetValueOrNull<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key) where TValue : struct
    {
        return !dictionary.TryGetValue(key, out var obj) ? null : obj;
    }
}

using System.Collections;
using System.Windows.Controls;

namespace LenovoLegionToolkit.WPF.Extensions;

/// <summary>
/// 提供 <see cref="ItemCollection"/> 批量操作的扩展方法。
/// </summary>
public static class ItemCollectionExtensions
{
    /// <summary>
    /// 将枚举中的所有项依次追加到集合末尾。
    /// </summary>
    /// <param name="itemCollection">目标集合。</param>
    /// <param name="enumerable">待追加的项集合。</param>
    public static void AddRange(this ItemCollection itemCollection, IEnumerable enumerable)
    {
        foreach (var item in enumerable)
            itemCollection.Add(item);
    }
}

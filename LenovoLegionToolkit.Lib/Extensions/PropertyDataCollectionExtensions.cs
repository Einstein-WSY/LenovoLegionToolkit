using System.Management;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供 WMI <see cref="PropertyDataCollection"/> 的扩展方法。
/// </summary>
public static class PropertyDataCollectionExtensions
{
    /// <summary>
    /// 判断属性集合中是否存在指定名称的属性。
    /// <para>
    /// WMI 属性集合没有按名称索引的便捷方法，这里通过枚举逐个比较属性名。
    /// </para>
    /// </summary>
    /// <param name="pdc">WMI 属性集合。</param>
    /// <param name="name">属性名称。</param>
    /// <returns>存在同名属性时返回 <c>true</c>。</returns>
    public static bool Contains(this PropertyDataCollection pdc, string name)
    {
        // ReSharper disable once NotDisposedResource
        var enumerator = pdc.GetEnumerator();

        while (enumerator.MoveNext())
            if (enumerator.Current.Name == name)
                return true;

        return false;
    }
}

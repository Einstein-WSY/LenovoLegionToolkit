using System.Collections;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供 <see cref="IList"/> 的扩展方法。
/// </summary>
public static class ListExtensions
{

    /// <summary>
    /// 将非泛型 <see cref="IList"/> 转换为 <see cref="object"/> 数组。
    /// <para>
    /// 用于与需要 object 数组的旧式 API（如某些 WMI 或反射调用）对接。
    /// </para>
    /// </summary>
    /// <param name="source">源列表。</param>
    /// <returns>包含所有元素的 object 数组。</returns>
    public static object[] ToArray(this IList source)
    {
        var array = new object[source.Count];
        source.CopyTo(array, 0);
        return array;
    }
}

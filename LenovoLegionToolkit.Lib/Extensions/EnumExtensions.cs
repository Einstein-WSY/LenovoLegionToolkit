using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供枚举相关的扩展方法，用于获取本地化的显示名称。
/// </summary>
public static class EnumExtensions
{
    /// <summary>
    /// 获取枚举值的显示名称。
    /// <para>
    /// 优先读取枚举成员上的 <see cref="DisplayAttribute"/>：
    /// 若指定了 <c>ResourceType</c>，则通过反射调用该资源类的同名静态属性以获取本地化文本；
    /// 否则直接返回 <c>Name</c>。没有特性时回退为枚举值的 <c>ToString()</c>。
    /// </para>
    /// </summary>
    /// <param name="enumValue">枚举值。</param>
    /// <returns>用于界面展示的名称。</returns>
    public static string GetDisplayName(this Enum enumValue)
    {
        // 通过反射找到该枚举值对应的成员，并取出其 DisplayAttribute
        var displayAttribute = enumValue.GetType()
            .GetMember(enumValue.ToString())
            .First()
            .GetCustomAttributes(false)
            .OfType<DisplayAttribute>()
            .FirstOrDefault();

        if (displayAttribute?.Name is null)
            return enumValue.ToString();

        // 资源型 Display：Name 实际是资源类中的属性名，通过静态属性反射取值以完成本地化
        if (displayAttribute.ResourceType?.GetProperty(displayAttribute.Name, BindingFlags.Static | BindingFlags.Public)?.GetValue(null) is string str)
            return str;

        return displayAttribute.Name;
    }

    /// <summary>
    /// 获取 <see cref="FlagsAttribute"/> 枚举中已置位成员的组合显示名称。
    /// </summary>
    /// <param name="enumValue">标志枚举值。</param>
    /// <param name="excluding">需要从结果中排除的成员（例如组合值本身或默认值）。</param>
    /// <returns>以逗号分隔的已置位成员显示名称。</returns>
    public static string GetFlagsDisplayName(this Enum enumValue, Enum? excluding = null)
    {
        var values = Enum.GetValues(enumValue.GetType()).Cast<Enum>();
        if (excluding is not null)
            values = values.Where(v => !v.Equals(excluding));
        // 利用 HasFlag 的委托签名筛选出所有已置位的成员（组合值本身通常也会命中，故用 excluding 排除）
        var names = values.Where(enumValue.HasFlag).Select(GetDisplayName);
        return string.Join(", ", names);
    }
}

using System;
using System.ComponentModel;
using System.Globalization;

namespace LenovoLegionToolkit.Lib.Macro.Utils.TypeConverters;

/// <summary>
/// <see cref="MacroIdentifier"/> 与字符串之间的转换器，用于把标识序列化为 JSON 对象的键。
/// 字符串格式为 <c>来源,键值</c>，例如 <c>Keyboard,96</c>。
/// </summary>
public class MacroIdentifierTypeConverter : TypeConverter
{
    /// <summary>
    /// 判断能否从指定类型转换（仅支持 <see cref="string"/>）。
    /// </summary>
    /// <param name="context">类型描述上下文。</param>
    /// <param name="sourceType">源类型。</param>
    /// <returns>支持转换时返回 <see langword="true"/>。</returns>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <summary>
    /// 判断能否转换为指定类型（仅支持 <see cref="string"/>）。
    /// </summary>
    /// <param name="context">类型描述上下文。</param>
    /// <param name="destinationType">目标类型。</param>
    /// <returns>支持转换时返回 <see langword="true"/>。</returns>
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    /// <summary>
    /// 把 <c>来源,键值</c> 格式的字符串解析为 <see cref="MacroIdentifier"/>。
    /// 解析失败时回退到基类实现（通常抛出异常）。
    /// </summary>
    /// <param name="context">类型描述上下文。</param>
    /// <param name="culture">区域信息。</param>
    /// <param name="value">待转换的值。</param>
    /// <returns>转换后的 <see cref="MacroIdentifier"/>。</returns>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is not string str)
            return base.ConvertFrom(context, culture, value);

        var split = str.Split(',');
        if (split.Length != 2)
            return base.ConvertFrom(context, culture, value);

        if (!Enum.TryParse<MacroSource>(split[0], out var source))
            return base.ConvertFrom(context, culture, value);

        if (!ulong.TryParse(split[1], out var key))
            return base.ConvertFrom(context, culture, value);

        return new MacroIdentifier(source, key);
    }

    /// <summary>
    /// 把 <see cref="MacroIdentifier"/> 转换为 <c>来源,键值</c> 格式的字符串。
    /// </summary>
    /// <param name="context">类型描述上下文。</param>
    /// <param name="culture">区域信息。</param>
    /// <param name="value">待转换的值。</param>
    /// <param name="destinationType">目标类型。</param>
    /// <returns>转换后的字符串。</returns>
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType != typeof(string) || value is not MacroIdentifier macroIdentifier)
            return base.ConvertTo(context, culture, value, destinationType);

        return $"{macroIdentifier.Source},{macroIdentifier.Key}";
    }
}

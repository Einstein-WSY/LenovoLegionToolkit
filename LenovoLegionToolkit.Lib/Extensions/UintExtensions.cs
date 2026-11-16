using System;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供 <see cref="uint"/> 的扩展方法，包含字节序转换与按位操作。
/// </summary>
public static class UintExtensions
{
    /// <summary>
    /// 反转 32 位整数的字节序（大小端互换）。
    /// </summary>
    /// <param name="state">原始数值。</param>
    /// <returns>字节序反转后的数值。</returns>
    public static uint ReverseEndianness(this uint state)
    {
        var bytes = BitConverter.GetBytes(state);
        Array.Reverse(bytes, 0, bytes.Length);
        return BitConverter.ToUInt32(bytes, 0);
    }

    /// <summary>
    /// 判断无符号整数的第 <paramref name="n"/> 位是否为 1。
    /// </summary>
    /// <param name="num">目标数值。</param>
    /// <param name="n">位号（0 起，0 为最低位）。</param>
    /// <returns>该位为 1 时返回 <c>true</c>。</returns>
    public static bool GetNthBit(this uint num, int n) => (num & (1 << n)) != 0;

    /// <summary>
    /// 设置无符号整数第 <paramref name="n"/> 位的值（置 1 或清 0），其他位保持不变。
    /// </summary>
    /// <param name="num">目标数值。</param>
    /// <param name="n">位号（0 起，0 为最低位）。</param>
    /// <param name="state">为 <c>true</c> 置 1，为 <c>false</c> 清 0。</param>
    /// <returns>设置后的数值。</returns>
    public static uint SetNthBit(this uint num, int n, bool state) => state ? num | (1U << n) : num & ~(1U << n);
}

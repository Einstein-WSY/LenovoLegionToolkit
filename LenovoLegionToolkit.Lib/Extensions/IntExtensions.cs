namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供 <see cref="int"/> 的扩展方法。
/// </summary>
public static class IntExtensions
{
    /// <summary>
    /// 判断整数的指定二进制位是否被置位（从最低位起，位号 0 为最低位）。
    /// </summary>
    /// <param name="value">目标整数。</param>
    /// <param name="position">位号（0 起）。</param>
    /// <returns>该位为 1 时返回 <c>true</c>。</returns>
    public static bool IsBitSet(this int value, int position) => (value & (1 << position)) != 0;
}

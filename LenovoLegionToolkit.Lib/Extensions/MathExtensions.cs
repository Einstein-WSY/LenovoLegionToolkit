using System;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供数学计算相关的扩展方法。
/// </summary>
public static class MathExtensions
{
    /// <summary>
    /// 将数值四舍五入到指定因子的最近整数倍。
    /// <para>
    /// 用于把功率、频率等数值对齐到硬件支持的步进（如风扇转速、充电功率的档位）。
    /// 采用 <see cref="MidpointRounding.AwayFromZero"/> 使 .5 的情况向远离零的方向取整。
    /// </para>
    /// </summary>
    /// <param name="value">原始数值。</param>
    /// <param name="factor">对齐因子（步进）。</param>
    /// <returns>对齐后的数值。</returns>
    public static int RoundNearest(int value, int factor)
    {
        return (int)Math.Round(value / (double)factor, MidpointRounding.AwayFromZero) * factor;
    }
}

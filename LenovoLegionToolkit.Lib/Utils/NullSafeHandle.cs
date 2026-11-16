using System;
using System.Runtime.InteropServices;

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// 表示一个永远有效、无需真正释放的空句柄，常用于在需要 <see cref="SafeHandle"/> 的 API 中占位。
/// </summary>
public sealed class NullSafeHandle() : SafeHandle(IntPtr.Zero, true)
{
    /// <summary>
    /// 单例的空句柄实例。
    /// </summary>
    public static readonly NullSafeHandle Null = new();

    /// <summary>
    /// 该句柄永远视为有效，因此始终返回 false。
    /// </summary>
    public override bool IsInvalid => false;

    /// <summary>
    /// 空句柄没有需要释放的原生资源，直接返回成功。
    /// </summary>
    protected override bool ReleaseHandle() => true;
}

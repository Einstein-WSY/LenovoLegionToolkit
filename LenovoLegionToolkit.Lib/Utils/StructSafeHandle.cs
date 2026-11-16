using System;
using System.Runtime.InteropServices;

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// 将非托管内存中的结构体指针包装为 <see cref="SafeHandle"/>，在释放时自动回收分配的内存。
/// </summary>
/// <typeparam name="T">需要封送到非托管内存的结构体类型。</typeparam>
public sealed class StructSafeHandle<T> : SafeHandle where T : struct
{
    private readonly IntPtr _ptr;

    /// <summary>
    /// 在非托管内存中分配并写入指定结构体，随后交由句柄管理其生命周期。
    /// </summary>
    /// <param name="str">需要封送的托管结构体实例。</param>
    public StructSafeHandle(T str) : base(IntPtr.Zero, true)
    {
        var size = Marshal.SizeOf(typeof(T));
        var ptr = Marshal.AllocHGlobal(size);
        Marshal.StructureToPtr(str, ptr, false);
        SetHandle(ptr);
        _ptr = ptr;
    }

    /// <summary>
    /// 句柄指针为空时视为无效。
    /// </summary>
    public override bool IsInvalid => handle == IntPtr.Zero;

    /// <summary>
    /// 释放此前分配的非托管内存。
    /// </summary>
    protected override bool ReleaseHandle()
    {
        Marshal.FreeHGlobal(_ptr);
        return true;
    }
}

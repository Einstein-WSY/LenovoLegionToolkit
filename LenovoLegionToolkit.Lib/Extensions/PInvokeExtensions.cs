using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;

namespace LenovoLegionToolkit.Lib.Extensions;

// ReSharper disable InconsistentNaming
// ReSharper disable IdentifierTypo

/// <summary>
/// 封装常用 Win32 API 调用的辅助方法（设备 IO 控制、错误码检查等）。
/// </summary>
public static class PInvokeExtensions
{
    /// <summary>
    /// 控制台（显示器）的电源显示状态。
    /// </summary>
    public enum CONSOLE_DISPLAY_STATE
    {
        /// <summary>显示关闭。</summary>
        Off = 0,
        /// <summary>显示开启。</summary>
        On = 1,
        /// <summary>显示变暗。</summary>
        Dimmed = 2
    }

    /// <summary>Win32 成功错误码。</summary>
    public const int ERROR_SUCCESS = 0;
    /// <summary>Win32 "没有更多条目" 错误码，常用于枚举结束判断。</summary>
    public const int ERROR_NO_MORE_ITEMS = 259;

    /// <summary>Known Folder 标志：使用默认行为。</summary>
    public const uint KF_FLAG_DEFAULT = 0;

    /// <summary>UEFI 变量属性：可被 BootService 访问。</summary>
    public const uint VARIABLE_ATTRIBUTE_BOOTSERVICE_ACCESS = 2;
    /// <summary>UEFI 变量属性：非易失性（掉电保留）。</summary>
    public const uint VARIABLE_ATTRIBUTE_NON_VOLATILE = 1;
    /// <summary>UEFI 变量属性：运行时可访问。</summary>
    public const uint VARIABLE_ATTRIBUTE_RUNTIME_ACCESS = 7;

    /// <summary>显示器亮度设置（GUID 视频输出技术）的固定 GUID。</summary>
    public static readonly Guid DISPLAY_BRIGTHNESS_SETTING_GUID = Guid.Parse("aded5e82-b909-4619-9949-f5d71dac0bcb");

    /// <summary>
    /// 调用 <c>DeviceIoControl</c>，以托管结构体的形式传入输入缓冲区并接收输出结构体。
    /// <para>
    /// 内部将结构体序列化到非托管内存、调用 API，再在成功时反序列化输出；
    /// 无论成功与否都会在 <c>finally</c> 中释放非托管内存，避免内存泄漏。
    /// </para>
    /// </summary>
    /// <typeparam name="TIn">输入数据结构体类型。</typeparam>
    /// <typeparam name="TOut">输出数据结构体类型。</typeparam>
    /// <param name="hDevice">设备句柄。</param>
    /// <param name="dwIoControlCode">IO 控制码。</param>
    /// <param name="inVal">输入数据。</param>
    /// <param name="outVal">输出数据；调用失败时为默认值。</param>
    /// <returns>调用成功返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    public static unsafe bool DeviceIoControl<TIn, TOut>(SafeFileHandle hDevice, uint dwIoControlCode, TIn inVal, out TOut outVal) where TIn : struct where TOut : struct
    {
        var lpInBuffer = IntPtr.Zero;
        var lpOutBuffer = IntPtr.Zero;

        try
        {
            var nInBufferSize = Marshal.SizeOf<TIn>();
            var nOutBufferSize = Marshal.SizeOf<TOut>();

            lpInBuffer = Marshal.AllocHGlobal(nInBufferSize);
            lpOutBuffer = Marshal.AllocHGlobal(nOutBufferSize);

            Marshal.StructureToPtr(inVal, lpInBuffer, false);

            var ret = PInvoke.DeviceIoControl(hDevice,
                dwIoControlCode,
                lpInBuffer.ToPointer(),
                (uint)nInBufferSize,
                lpOutBuffer.ToPointer(),
                (uint)nOutBufferSize,
                null,
                null);

            outVal = ret ? Marshal.PtrToStructure<TOut>(lpOutBuffer) : default;

            return ret;
        }
        finally
        {
            Marshal.FreeHGlobal(lpInBuffer);
            Marshal.FreeHGlobal(lpOutBuffer);
        }
    }

    /// <summary>
    /// 检查上一次 Win32 调用的错误码，若非 0 则抛出异常。
    /// </summary>
    /// <param name="description">用于异常信息的操作描述。</param>
    /// <exception cref="Win32Exception">存在 Win32 错误时抛出。</exception>
    public static void ThrowIfWin32Error(string description)
    {
        var errorCode = Marshal.GetLastWin32Error();
        ThrowIfWin32Error(errorCode, description);
    }

    /// <summary>
    /// 根据指定错误码判断是否失败，失败（错误码非 0）时抛出 <see cref="Win32Exception"/>。
    /// </summary>
    /// <param name="errorCode">Win32 错误码；为 0（成功）时不抛出。</param>
    /// <param name="description">用于异常信息的操作描述。</param>
    /// <exception cref="Win32Exception">错误码非 0 时抛出。</exception>
    public static void ThrowIfWin32Error(int errorCode, string description)
    {
        if (errorCode == 0)
            return;

        throw new Win32Exception(errorCode, $"{description} failed with Win32 error {errorCode}");
    }
}

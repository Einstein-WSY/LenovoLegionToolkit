using System.Diagnostics;
using Windows.Win32;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供 <see cref="Process"/> 的扩展方法。
/// </summary>
public static class ProcessExtensions
{
    /// <summary>
    /// 通过 <c>K32GetModuleFileNameEx</c> 获取进程主模块的完整文件路径。
    /// <para>
    /// 相比 <c>Process.MainModule.FileName</c>，此方式对 32/64 位进程混合场景更稳健，
    /// 且不需要访问可能受限的 <c>MainModule</c> 属性。
    /// </para>
    /// </summary>
    /// <param name="process">目标进程。</param>
    /// <param name="maxLength">路径缓冲区最大字符数。</param>
    /// <returns>进程的可执行文件完整路径；调用失败则返回 <c>null</c>。</returns>
    public static string? GetFileName(this Process process, int maxLength = 512)
    {
        var chars = new char[maxLength];
        return PInvoke.K32GetModuleFileNameEx(process.SafeHandle, null, chars) == 0
            ? null
            : chars.ToString();
    }
}

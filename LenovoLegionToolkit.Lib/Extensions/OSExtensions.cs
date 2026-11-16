using System;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供操作系统版本判定的扩展方法。
/// </summary>
public static class OSExtensions
{
    /// <summary>
    /// 根据当前系统版本号推断所处的 Windows 版本。
    /// <para>
    /// 由于 Windows 10/11 共享主版本号，Windows 11 需依据内部版本号 22000 及以上判断；
    /// 低于 Windows 7 的极老系统统一回退为 <see cref="OS.Windows11"/>（项目仅支持受支持的系统）。
    /// </para>
    /// </summary>
    /// <returns>当前系统对应的 <see cref="OS"/> 枚举值。</returns>
    public static OS GetCurrent()
    {
        var os = Environment.OSVersion;
        if (os.Version >= new Version(10, 0, 22000, 0)) // Windows 11
            return OS.Windows11;
        else if (os.Version >= new Version(10, 0, 0, 0)) // Windows 10
            return OS.Windows10;
        else if (os.Version >= new Version(6, 2, 0, 0)) // Windows 8
            return OS.Windows8;
        else if (os.Version >= new Version(6, 1, 0, 0)) // Windows 7
            return OS.Windows7;
        else
            return OS.Windows11;
    }
}

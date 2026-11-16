using System;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供 <see cref="Version"/> 的扩展方法。
/// </summary>
public static class VersionExtensions
{
    /// <summary>
    /// 判断版本号是否表示 Beta 构建。
    /// <para>
    /// 约定：<c>0.0.1.0</c> 为开发占位版本；构建号（Build）为 99 表示 Beta 通道。
    /// </para>
    /// </summary>
    /// <param name="version">待判断的版本号。</param>
    /// <returns>属于 Beta 版本时返回 <c>true</c>。</returns>
    public static bool IsBeta(this Version version) => version switch
    {
        { Major: 0, Minor: 0, Build: 1, Revision: 0 } => true,
        { Build: 99 } => true,
        _ => false
    };
}

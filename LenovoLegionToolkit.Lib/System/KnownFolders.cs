using System;
using System.Collections.Generic;
using LenovoLegionToolkit.Lib.Extensions;
using Windows.Win32;

namespace LenovoLegionToolkit.Lib.System;

/// <summary>
/// Windows 已知文件夹（Known Folder）路径解析辅助类。
/// </summary>
public static class KnownFolders
{
    /// <summary>
    /// 已知文件夹枚举到其 GUID 的映射表。
    /// 这些 GUID 为 Windows Shell 定义的固定值，用于 SHGetKnownFolderPath 查询实际路径。
    /// </summary>
    private static readonly Dictionary<KnownFolder, Guid> Folders = new()
    {
        [KnownFolder.Contacts] = new("56784854-C6CB-462B-8169-88E350ACB882"),
        [KnownFolder.Downloads] = new("374DE290-123F-4565-9164-39C4925E467B"),
        [KnownFolder.Favorites] = new("1777F761-68AD-4D8A-87BD-30B759FA33DD"),
        [KnownFolder.Links] = new("BFB9D5E0-C6A9-404C-B2B2-AE6DB6AF4968"),
        [KnownFolder.SavedGames] = new("4C5C32FF-BB9D-43B0-B5B4-2D72E54EAAA4"),
        [KnownFolder.SavedSearches] = new("7D1D3A04-DEBB-4115-95CF-2F29DA2920DA")
    };

    /// <summary>
    /// 获取指定已知文件夹在磁盘上的实际路径。
    /// </summary>
    /// <param name="knownFolder">要查询的已知文件夹枚举值。</param>
    /// <returns>文件夹的完整路径；查询失败时返回空字符串。</returns>
    public static string GetPath(KnownFolder knownFolder)
    {
        // 调用 Win32 SHGetKnownFolderPath，失败（如文件夹被重定向或不存在）时降级为空字符串
        if (!PInvoke.SHGetKnownFolderPath(Folders[knownFolder], PInvokeExtensions.KF_FLAG_DEFAULT, null, out var path).Succeeded)
            return string.Empty;
        return path.ToString() ?? string.Empty;
    }
}

using System;
using System.IO;

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// 提供应用使用的若干标准目录路径（程序目录、本地应用数据目录与临时目录）。
/// </summary>
public static class Folders
{
    /// <summary>
    /// 应用程序所在目录（即可执行文件的基础路径），无法获取时返回空字符串。
    /// </summary>
    public static string Program => AppDomain.CurrentDomain.SetupInformation.ApplicationBase ?? string.Empty;

    /// <summary>
    /// 用于持久化配置与日志的本地应用数据目录（%LOCALAPPDATA%\LenovoLegionToolkit）。
    /// 目录不存在时会被自动创建。
    /// </summary>
    public static string AppData
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folderPath = Path.Combine(appData, "LenovoLegionToolkit");
            Directory.CreateDirectory(folderPath);
            return folderPath;
        }
    }

    /// <summary>
    /// 应用专用的临时目录（系统临时目录下的 LenovoLegionToolkit 子目录）。
    /// 目录不存在时会被自动创建。
    /// </summary>
    public static string Temp
    {
        get
        {
            var appData = Path.GetTempPath();
            var folderPath = Path.Combine(appData, "LenovoLegionToolkit");
            Directory.CreateDirectory(folderPath);
            return folderPath;
        }
    }
}

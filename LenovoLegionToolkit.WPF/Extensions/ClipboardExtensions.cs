using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using LenovoLegionToolkit.Lib;

namespace LenovoLegionToolkit.WPF.Extensions;

/// <summary>
/// 提供进程列表与系统剪贴板之间互相转换的扩展方法。
/// </summary>
public static class ClipboardExtensions
{
    /// <summary>
    /// 将进程列表的可执行文件路径按行写入剪贴板。
    /// </summary>
    /// <param name="processes">待写入剪贴板的进程信息集合。</param>
    public static void SetProcesses(IEnumerable<ProcessInfo> processes)
    {
        var sb = new StringBuilder();
        foreach (var process in processes)
            sb.AppendLine(process.ExecutablePath);
        Clipboard.SetText(sb.ToString());
    }

    /// <summary>
    /// 从剪贴板按行读取可执行文件路径，过滤掉不存在的文件并去重。
    /// </summary>
    /// <returns>由剪贴板内容解析出的进程信息集合。</returns>
    public static IEnumerable<ProcessInfo> GetProcesses() => Clipboard.GetText()
        .Split(Environment.NewLine)
        .Select(l => l.Trim('"'))
        .Where(File.Exists)
        .Distinct()
        .Select(ProcessInfo.FromPath);
}

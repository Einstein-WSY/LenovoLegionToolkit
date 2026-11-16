using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// 简单的文件日志记录器，按启动时间创建独立日志文件，并提供异常报告落盘能力。
/// </summary>
public class Log
{
    private static Log? _instance;

    /// <summary>
    /// 全局单例日志实例。
    /// </summary>
    public static Log Instance
    {
        get
        {
            _instance ??= new Log();
            return _instance;
        }
    }

    private readonly object _lock = new();
    private readonly string _folderPath;
    private readonly string _logPath;

    /// <summary>
    /// 是否启用详细跟踪日志；关闭时 <see cref="Trace"/> 调用会被直接忽略。
    /// </summary>
    public bool IsTraceEnabled { get; set; }

    /// <summary>
    /// 当前日志文件的完整路径。
    /// </summary>
    public string LogPath => _logPath;

    private Log()
    {
        // 日志文件以 UTC 启动时间命名，避免多次运行相互覆盖。
        _folderPath = Path.Combine(Folders.AppData, "log");
        Directory.CreateDirectory(_folderPath);
        _logPath = Path.Combine(_folderPath, $"log_{DateTime.UtcNow:yyyy_MM_dd_HH_mm_ss}.txt");
    }

    /// <summary>
    /// 将异常信息与标题写入独立的错误报告文件（不依赖跟踪开关，始终记录）。
    /// </summary>
    /// <param name="header">错误标题/摘要。</param>
    /// <param name="ex">需要记录的异常。</param>
    public void ErrorReport(string header, Exception ex)
    {
        var errorReportPath = Path.Combine(_folderPath, $"error_{DateTime.UtcNow:yyyy_MM_dd_HH_mm_ss}.txt");
        File.AppendAllLines(errorReportPath, [header, Serialize(ex)]);
    }

    /// <summary>
    /// 写入一条跟踪日志（仅在 <see cref="IsTraceEnabled"/> 为 true 时生效）。
    /// 调用方文件、行号与成员名由编译器自动填充。
    /// </summary>
    /// <param name="message">格式化后的日志消息。</param>
    /// <param name="ex">可选的关联异常。</param>
    /// <param name="file">调用方源文件路径（自动填充）。</param>
    /// <param name="lineNumber">调用方行号（自动填充）。</param>
    /// <param name="caller">调用方成员名（自动填充）。</param>
    public void Trace(FormattableString message,
        Exception? ex = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int lineNumber = -1,
        [CallerMemberName] string? caller = null)
    {
        if (!IsTraceEnabled)
            return;

        LogInternal(_logPath, message, ex, file, lineNumber, caller);
    }

    private void LogInternal(string path,
        FormattableString message,
        Exception? ex,
        string? file,
        int lineNumber,
        string? caller)
    {
        lock (_lock)
        {
            var lines = new List<string>
            {
                $"[{DateTime.UtcNow:dd/MM/yyyy HH:mm:ss.fff}] [{Environment.CurrentManagedThreadId}] [{Path.GetFileName(file)}#{lineNumber}:{caller}] {message}"
            };
            if (ex is not null)
                lines.Add(Serialize(ex));

#if DEBUG
            foreach (var line in lines)
                Debug.WriteLine(line);
#endif

            // 加锁后整块追加，避免多线程并发写同一日志文件导致内容交错。
            File.AppendAllLines(path, lines);
        }
    }

    /// <summary>
    /// 同时输出原始异常信息与 Ben.Demystifier 还原后的调用栈，便于定位异步代码中的异常来源。
    /// </summary>
    private static string Serialize(Exception ex) => new StringBuilder()
        .AppendLine("=== Exception ===")
        .AppendLine(ex.ToString())
        .AppendLine()
        .AppendLine("=== Exception demystified ===")
        .AppendLine(ex.ToStringDemystified())
        .ToString();
}

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.System;

/// <summary>
/// 外部进程执行辅助类，用于以隐藏窗口方式启动命令行程序并获取输出。
/// </summary>
public static class CMD
{
    /// <summary>
    /// 异步运行指定可执行文件。
    /// </summary>
    /// <param name="file">可执行文件路径（如 powershell.exe 或 cmd）。</param>
    /// <param name="arguments">命令行参数。</param>
    /// <param name="createNoWindow">是否以隐藏窗口方式启动；为 true 时同时重定向标准输出与标准错误。</param>
    /// <param name="waitForExit">是否等待进程退出；为 false 时立即返回 (-1, 空字符串)。</param>
    /// <param name="environment">需要附加/覆盖的环境变量集合，可为 null。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>元组：退出码与实际标准输出（未重定向输出时为空字符串）。</returns>
    public static async Task<(int, string)> RunAsync(string file, string arguments, bool createNoWindow = true, bool waitForExit = true, Dictionary<string, string?>? environment = null, CancellationToken token = default)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Running... [file={file}, argument={arguments}, createNoWindow={createNoWindow}, waitForExit={waitForExit}, environment=[{(environment is null ? string.Empty : string.Join(",", environment))}]");

        var cmd = new Process();
        // UseShellExecute 必须为 false，否则无法重定向标准输出/错误，也无法注入环境变量
        cmd.StartInfo.UseShellExecute = false;
        cmd.StartInfo.CreateNoWindow = createNoWindow;
        cmd.StartInfo.RedirectStandardOutput = createNoWindow;
        cmd.StartInfo.RedirectStandardError = createNoWindow;
        cmd.StartInfo.WindowStyle = createNoWindow ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal;
        cmd.StartInfo.FileName = file;
        if (!string.IsNullOrWhiteSpace(arguments))
            cmd.StartInfo.Arguments = arguments;

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
                cmd.StartInfo.Environment[key] = value;
        }

        cmd.Start();

        if (!waitForExit)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Ran [file={file}, argument={arguments}, createNoWindow={createNoWindow}, waitForExit={waitForExit}, environment=[{(environment is null ? string.Empty : string.Join(",", environment))}]");

            // 不等待退出：返回哨兵退出码 -1，表示"已启动但结果未知"
            return (-1, string.Empty);
        }

        // 先启动两个读取任务持续排空管道，再等待进程退出，
        // 否则子进程写满输出/错误缓冲区后会与 WaitForExitAsync 互相等待形成死锁
        var outputTask = createNoWindow ? cmd.StandardOutput.ReadToEndAsync(token) : Task.FromResult(string.Empty);
        var errorTask = createNoWindow ? cmd.StandardError.ReadToEndAsync(token) : Task.FromResult(string.Empty);

        await cmd.WaitForExitAsync(token).ConfigureAwait(false);

        var exitCode = cmd.ExitCode;
        var output = await outputTask.ConfigureAwait(false);
        _ = await errorTask.ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Ran [file={file}, argument={arguments}, createNoWindow={createNoWindow}, waitForExit={waitForExit}, exitCode={exitCode} output={output}]");

        return (exitCode, output);
    }
}

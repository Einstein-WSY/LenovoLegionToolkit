using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：执行外部脚本或程序，并可通过上下文把标准输出传给后续步骤。
/// </summary>
/// <param name="scriptPath">脚本或可执行文件路径。</param>
/// <param name="scriptArguments">传给脚本的命令行参数。</param>
/// <param name="runSilently">是否隐藏窗口静默运行；<see langword="null"/> 时默认为 <see langword="true"/>。</param>
/// <param name="waitUntilFinished">是否等待脚本结束后再继续流水线；<see langword="null"/> 时默认为 <see langword="false"/>。</param>
[method: JsonConstructor]
public class RunAutomationStep(string? scriptPath, string? scriptArguments, bool? runSilently, bool? waitUntilFinished)
    : IAutomationStep
{
    /// <summary>脚本或可执行文件路径。</summary>
    public string? ScriptPath { get; } = scriptPath;

    /// <summary>传给脚本的命令行参数。</summary>
    public string? ScriptArguments { get; } = scriptArguments;

    /// <summary>是否静默运行（不显示窗口），默认开启。</summary>
    public bool RunSilently { get; } = runSilently ?? true;

    /// <summary>是否等待脚本执行结束，默认不等待。</summary>
    public bool WaitUntilFinished { get; } = waitUntilFinished ?? false;

    /// <summary>该步骤始终受支持。</summary>
    /// <returns>始终返回 <see langword="true"/>。</returns>
    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    /// <summary>
    /// 执行步骤：启动脚本，并把其标准输出（去除末尾换行）写入
    /// <see cref="AutomationContext.LastRunOutput"/> 供后续步骤引用。
    /// </summary>
    /// <param name="context">流水线执行上下文，用于保存脚本输出。</param>
    /// <param name="environment">作为子进程环境变量传入的字典。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>脚本启动/执行完成的任务。</returns>
    public async Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        // 未配置脚本路径时跳过，避免执行空命令
        if (string.IsNullOrWhiteSpace(ScriptPath))
            return;

        var (_, output) = await CMD.RunAsync(ScriptPath,
            ScriptArguments ?? string.Empty,
            RunSilently,
            WaitUntilFinished,
            environment.Dictionary,
            token).ConfigureAwait(false);
        // 去掉末尾换行，避免通知等文本出现多余空行
        context.LastRunOutput = output.TrimEnd();
    }

    /// <summary>创建该步骤的深拷贝，保留脚本路径、参数及运行选项。</summary>
    /// <returns>新的步骤实例。</returns>
    IAutomationStep IAutomationStep.DeepCopy() => new RunAutomationStep(ScriptPath, ScriptArguments, RunSilently, WaitUntilFinished);
}

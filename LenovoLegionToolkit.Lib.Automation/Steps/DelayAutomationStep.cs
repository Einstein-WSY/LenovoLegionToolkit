using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：在流水线中插入固定延时，用于等待前一步的硬件状态稳定。
/// </summary>
/// <param name="state">延时配置，包含秒数。</param>
[method: JsonConstructor]
public class DelayAutomationStep(Delay state)
    : IAutomationStep<Delay>
{
    /// <summary>延时配置。</summary>
    public Delay State { get; } = state;

    /// <summary>该步骤始终受支持。</summary>
    /// <returns>始终返回 <see langword="true"/>。</returns>
    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    /// <summary>
    /// 获取界面上可选的延时档位。
    /// </summary>
    /// <returns>固定为 1、2、3、5 秒的档位数组。</returns>
    public Task<Delay[]> GetAllStatesAsync() => Task.FromResult(new Delay[] {
        new(1),
        new(2),
        new(3),
        new(5)
    });

    /// <summary>创建该步骤的深拷贝，保留延时档位。</summary>
    /// <returns>新的步骤实例。</returns>
    public IAutomationStep DeepCopy() => new DelayAutomationStep(State);

    /// <summary>执行步骤：按配置的秒数等待，等待期间响应取消令牌。</summary>
    /// <param name="context">流水线执行上下文。</param>
    /// <param name="environment">环境变量集合。</param>
    /// <param name="token">取消令牌，流水线中止时立即结束等待。</param>
    /// <returns>延时结束的任务。</returns>
    public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token) => Task.Delay(TimeSpan.FromSeconds(State.DelaySeconds), token);
}

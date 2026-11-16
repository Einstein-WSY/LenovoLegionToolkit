using System;
using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：快捷操作引用步骤。执行流水线时，流水线的展开阶段会把
/// 所引用流水线的全部步骤插入当前执行序列；该步骤自身的 <see cref="RunAsync"/> 为空实现。
/// </summary>
/// <param name="pipelineId">被引用的流水线 Id；为 <see langword="null"/> 表示尚未指定。</param>
public class QuickActionAutomationStep(Guid? pipelineId)
    : IAutomationStep
{
    /// <summary>被引用（快捷执行）的流水线 Id。</summary>
    public Guid? PipelineId { get; } = pipelineId;

    /// <summary>该步骤始终受支持。</summary>
    /// <returns>始终返回 <see langword="true"/>。</returns>
    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    /// <summary>空实现：真正的动作由被引用流水线的各步骤完成。</summary>
    /// <param name="context">流水线执行上下文。</param>
    /// <param name="environment">环境变量集合。</param>
    /// <param name="token">取消令牌。</param>
    public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token) => Task.CompletedTask;

    /// <summary>创建该步骤的深拷贝，保留所引用的流水线 Id。</summary>
    /// <returns>新的步骤实例。</returns>
    IAutomationStep IAutomationStep.DeepCopy() => new QuickActionAutomationStep(PipelineId);
}

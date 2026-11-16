using System;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Macro;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：开启或关闭宏功能（按键宏的录制与回放）。
/// </summary>
/// <param name="state">目标状态：开启或关闭宏。</param>
public class MacroAutomationStep(MacroAutomationStepState state) : IAutomationStep<MacroAutomationStepState>
{
    private readonly MacroController _controller = IoCContainer.Resolve<MacroController>();

    /// <summary>目标状态（可写，便于界面直接修改）。</summary>
    public MacroAutomationStepState State { get; set; } = state;

    /// <summary>该步骤始终受支持。</summary>
    /// <returns>始终返回 <see langword="true"/>。</returns>
    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    /// <summary>获取全部可选状态。</summary>
    /// <returns>枚举中的所有状态值。</returns>
    public Task<MacroAutomationStepState[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<MacroAutomationStepState>());

    /// <summary>执行步骤：按目标状态启用或禁用宏控制器。</summary>
    /// <param name="context">流水线执行上下文。</param>
    /// <param name="environment">环境变量集合。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>表示操作已完成的任务。</returns>
    public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        // 只有目标状态为 On 时才启用，其余状态一律视为禁用
        _controller.SetEnabled(State is MacroAutomationStepState.On);
        return Task.CompletedTask;
    }

    /// <summary>创建该步骤的深拷贝，保留目标状态。</summary>
    /// <returns>新的步骤实例。</returns>
    public IAutomationStep DeepCopy() => new MacroAutomationStep(State);

}

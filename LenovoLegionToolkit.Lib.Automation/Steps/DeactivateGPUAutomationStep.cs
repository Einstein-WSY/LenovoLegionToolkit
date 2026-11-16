using System;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：处理独立显卡占用/异常，可结束占用 GPU 的进程或重启 GPU，
/// 用于在切换到集显场景时释放独显功耗，或恢复无响应的独显。
/// </summary>
/// <param name="state">操作类型：结束占用进程或重启 GPU。</param>
public class DeactivateGPUAutomationStep(DeactivateGPUAutomationStepState state)
    : IAutomationStep<DeactivateGPUAutomationStepState>
{
    private readonly GPUController _controller = IoCContainer.Resolve<GPUController>();

    /// <summary>操作类型。</summary>
    public DeactivateGPUAutomationStepState State { get; } = state;

    /// <summary>获取全部可选操作。</summary>
    /// <returns>枚举中的所有操作值。</returns>
    public Task<DeactivateGPUAutomationStepState[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<DeactivateGPUAutomationStepState>());

    /// <summary>判断当前设备是否支持 GPU 控制。</summary>
    /// <returns>支持时返回 <see langword="true"/>。</returns>
    public Task<bool> IsSupportedAsync() => Task.FromResult(_controller.IsSupported());

    /// <summary>
    /// 执行步骤：先刷新 GPU 状态，再按目标操作和当前状态决定实际执行的动作。
    /// </summary>
    /// <param name="context">流水线执行上下文。</param>
    /// <param name="environment">环境变量集合。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>操作完成的任务。</returns>
    public async Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        // 设备不支持 GPU 控制时跳过
        if (!_controller.IsSupported())
            return;

        // 先刷新状态，避免依据过期的缓存状态做判断
        var status = await _controller.RefreshNowAsync().ConfigureAwait(false);

        switch (State)
        {
            // 仅在 GPU 处于活动状态（有进程占用）时才需要结束进程
            case DeactivateGPUAutomationStepState.KillApps when status.State is GPUState.Active:
                await _controller.KillGPUProcessesAsync().ConfigureAwait(false);
                break;
            // GPU 处于活动或非活动状态时都可以通过重启来恢复
            case DeactivateGPUAutomationStepState.RestartGPU when status.State is GPUState.Active or GPUState.Inactive:
                await _controller.RestartGPUAsync().ConfigureAwait(false);
                break;
        }
    }

    /// <summary>创建该步骤的深拷贝，保留操作类型。</summary>
    /// <returns>新的步骤实例。</returns>
    IAutomationStep IAutomationStep.DeepCopy() => new DeactivateGPUAutomationStep(State);
}

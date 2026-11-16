using System;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：开启或关闭独立显卡超频，并立即应用新的配置。
/// </summary>
/// <param name="state">目标状态：开启或关闭独显超频。</param>
public class OverclockDiscreteGPUAutomationStep(OverclockDiscreteGPUAutomationStepState state)
    : IAutomationStep<OverclockDiscreteGPUAutomationStepState>
{
    private readonly GPUOverclockController _controller = IoCContainer.Resolve<GPUOverclockController>();

    /// <summary>目标状态。</summary>
    public OverclockDiscreteGPUAutomationStepState State { get; } = state;

    /// <summary>获取全部可选状态。</summary>
    /// <returns>枚举中的所有状态值。</returns>
    public Task<OverclockDiscreteGPUAutomationStepState[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<OverclockDiscreteGPUAutomationStepState>());

    /// <summary>判断当前设备是否支持独显超频。</summary>
    /// <returns>支持时返回 <see langword="true"/>。</returns>
    public Task<bool> IsSupportedAsync() => _controller.IsSupportedAsync();

    /// <summary>
    /// 执行步骤：保留当前超频参数，仅切换启用开关，然后强制重新应用到显卡。
    /// </summary>
    /// <param name="context">流水线执行上下文。</param>
    /// <param name="environment">环境变量集合。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>配置应用完成的任务。</returns>
    public async Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        // 设备不支持超频时跳过
        if (!await _controller.IsSupportedAsync().ConfigureAwait(false))
            return;

        // 读取现有的超频参数（频率/显存偏移），只改变是否启用
        var (_, info) = _controller.GetState();

        switch (State)
        {
            case OverclockDiscreteGPUAutomationStepState.On:
                _controller.SaveState(true, info);
                break;
            case OverclockDiscreteGPUAutomationStepState.Off:
                _controller.SaveState(false, info);
                break;
        }

        // force 为 true：忽略缓存状态，强制把配置重新写入显卡
        await _controller.ApplyStateAsync(true).ConfigureAwait(false);
    }

    /// <summary>创建该步骤的深拷贝，保留目标状态。</summary>
    /// <returns>新的步骤实例。</returns>
    IAutomationStep IAutomationStep.DeepCopy() => new OverclockDiscreteGPUAutomationStep(State);
}

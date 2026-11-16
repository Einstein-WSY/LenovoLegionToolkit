using System;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Utils;
using Newtonsoft.Json;
using WindowsDisplayAPI.Exceptions;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：切换屏幕刷新率。
/// 显示模式切换期间 Windows 可能瞬时失败并抛出 <see cref="ModeChangeException"/>，
/// 因此该步骤带有限次重试。
/// </summary>
/// <param name="state">目标刷新率。</param>
[method: JsonConstructor]
public class RefreshRateAutomationStep(RefreshRate state)
    : AbstractFeatureAutomationStep<RefreshRate>(state)
{
    /// <summary>
    /// 执行步骤：带重试地切换刷新率。仅对 <see cref="ModeChangeException"/> 重试，
    /// 最多尝试 5 次、每次间隔 1 秒，以等待显示模式切换完成。
    /// </summary>
    /// <param name="context">流水线执行上下文。</param>
    /// <param name="environment">环境变量集合。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>刷新率切换完成的任务。</returns>
    public override Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        // 最后一次参数为日志中显示的操作来源名称
        return RetryHelper.RetryAsync(() => base.RunAsync(context, environment, token),
            5,
            TimeSpan.FromSeconds(1),
            ex => ex is ModeChangeException,
            nameof(RefreshRateAutomationStep));
    }

    /// <summary>创建该步骤的深拷贝，保留目标刷新率。</summary>
    /// <returns>新的步骤实例。</returns>
    public override IAutomationStep DeepCopy() => new RefreshRateAutomationStep(State);
}

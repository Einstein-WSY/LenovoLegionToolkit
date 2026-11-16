using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：开启 Wi-Fi 无线网卡。
/// </summary>
public class TurnOnWiFiAutomationStep : IAutomationStep
{
    /// <summary>该步骤始终受支持。</summary>
    /// <returns>始终返回 <see langword="true"/>。</returns>
    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    /// <summary>执行步骤：调用系统接口启用 Wi-Fi。</summary>
    /// <param name="context">流水线执行上下文。</param>
    /// <param name="environment">环境变量集合。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>表示操作已完成的任务。</returns>
    public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        // 底层为同步操作，完成后直接返回已完成任务
        WiFi.TurnOn();
        return Task.CompletedTask;
    }

    /// <summary>创建该步骤的深拷贝（无状态，直接新建实例）。</summary>
    /// <returns>新的步骤实例。</returns>
    public IAutomationStep DeepCopy() => new TurnOnWiFiAutomationStep();
}

using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：把内屏亮度设置为指定百分比。
/// 直接调用亮度控制器写入，不经过特性状态缓存。
/// </summary>
/// <param name="brightness">目标亮度百分比。</param>
[method: JsonConstructor]
public class DisplayBrightnessAutomationStep(int brightness)
    : IAutomationStep
{
    private readonly DisplayBrightnessController _controller = IoCContainer.Resolve<DisplayBrightnessController>();
    /// <summary>目标亮度百分比（0-100）。</summary>
    public int Brightness { get; } = brightness;

    /// <summary>该步骤始终受支持。</summary>
    /// <returns>始终返回 <see langword="true"/>。</returns>
    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    /// <summary>执行步骤：把屏幕亮度调整为 <see cref="Brightness"/>。</summary>
    /// <param name="context">流水线执行上下文。</param>
    /// <param name="environment">环境变量集合。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>亮度设置完成的任务。</returns>
    public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        return _controller.SetBrightnessAsync(Brightness);
    }

    /// <summary>创建该步骤的深拷贝，保留目标亮度。</summary>
    /// <returns>新的步骤实例。</returns>
    IAutomationStep IAutomationStep.DeepCopy() => new DisplayBrightnessAutomationStep(Brightness);
}

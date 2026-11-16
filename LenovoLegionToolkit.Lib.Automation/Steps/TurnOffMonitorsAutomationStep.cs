using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Listeners;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：关闭全部显示器（相当于按显示器电源键），常用于“离开时自动息屏”。
/// 该步骤不依赖具体硬件型号，始终受支持。
/// </summary>
public class TurnOffMonitorsAutomationStep : IAutomationStep
{
    // 借助原生 Windows 消息监听器向系统发送关闭显示器的消息
    private readonly NativeWindowsMessageListener _nativeWindowsMessageListener = IoCContainer.Resolve<NativeWindowsMessageListener>();

    /// <summary>该步骤在所有设备上均可执行。</summary>
    /// <returns>始终返回 <see langword="true"/>。</returns>
    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    /// <summary>执行步骤：请求系统关闭显示器。</summary>
    /// <param name="context">流水线执行上下文。</param>
    /// <param name="environment">环境变量集合。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>显示器关闭请求完成的任务。</returns>
    public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token) => _nativeWindowsMessageListener.TurnOffMonitorAsync();

    /// <summary>创建该步骤的深拷贝（无状态，直接新建实例）。</summary>
    /// <returns>新的步骤实例。</returns>
    public IAutomationStep DeepCopy() => new TurnOffMonitorsAutomationStep();
}

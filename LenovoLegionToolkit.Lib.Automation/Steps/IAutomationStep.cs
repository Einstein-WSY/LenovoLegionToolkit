using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤的统一接口。流水线按顺序执行各步骤，每个步骤完成一个原子动作
/// （切换硬件特性、启动程序、延时等）。
/// </summary>
public interface IAutomationStep
{
    /// <summary>
    /// 判断当前设备是否支持该步骤；不支持的步骤会在界面中隐藏，或在执行时被跳过。
    /// </summary>
    /// <returns>支持时返回 <see langword="true"/>。</returns>
    Task<bool> IsSupportedAsync();

    /// <summary>
    /// 执行该步骤。
    /// </summary>
    /// <param name="context">流水线执行上下文，用于在步骤之间传递运行输出等数据。</param>
    /// <param name="environment">步骤可读取的环境变量集合。</param>
    /// <param name="token">用于取消执行的操作令牌。</param>
    Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token);

    /// <summary>
    /// 创建该步骤的深拷贝，避免编辑副本时影响流水线中已有步骤。
    /// </summary>
    /// <returns>新的步骤实例。</returns>
    IAutomationStep DeepCopy();
}

/// <summary>
/// 带目标状态的自动化步骤接口，<typeparamref name="T"/> 为步骤要切换到的状态类型。
/// </summary>
/// <typeparam name="T">步骤状态类型（通常为枚举）。</typeparam>
public interface IAutomationStep<T> : IAutomationStep where T : struct
{
    /// <summary>步骤要切换到的目标状态。</summary>
    T State { get; }

    /// <summary>
    /// 获取该步骤在界面上可选的全部状态。
    /// </summary>
    /// <returns>可选状态的数组。</returns>
    Task<T[]> GetAllStatesAsync();
}

using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 基于 <see cref="IFeature{T}"/> 的自动化步骤基类，封装了通用的
/// “读取当前状态 → 与目标不同则写入 → 广播状态变更消息”流程，子类只需提供目标状态。
/// </summary>
/// <typeparam name="T">特性状态类型。</typeparam>
/// <param name="state">步骤要切换到的目标状态。</param>
public abstract class AbstractFeatureAutomationStep<T>(T state)
    : IAutomationStep<T> where T : struct
{
    // 从 IoC 容器解析对应的特性实现（如电源模式、HDR、刷新率等）
    private readonly IFeature<T> _feature = IoCContainer.Resolve<IFeature<T>>();

    /// <summary>步骤要切换到的目标状态。</summary>
    public T State { get; } = state;

    /// <summary>判断当前设备是否支持该特性。</summary>
    /// <returns>支持时返回 <see langword="true"/>。</returns>
    public Task<bool> IsSupportedAsync() => _feature.IsSupportedAsync();

    /// <summary>
    /// 执行步骤：仅当目标状态与当前状态不一致时才调用设置接口，
    /// 随后广播特性状态变更消息，使界面和其他监听者同步刷新。
    /// </summary>
    /// <param name="context">流水线执行上下文。</param>
    /// <param name="environment">环境变量集合。</param>
    /// <param name="token">取消令牌。</param>
    public virtual async Task RunAsync(AutomationContext context, AutomationEnvironment environment,
        CancellationToken token)
    {
        var currentState = await _feature.GetStateAsync().ConfigureAwait(false);
        // 状态一致时跳过写入，避免不必要的硬件调用（部分写入会导致屏幕闪烁或明显延迟）
        if (!State.Equals(currentState))
            await _feature.SetStateAsync(State).ConfigureAwait(false);
        MessagingCenter.Publish(new FeatureStateMessage<T>(State));
    }

    /// <summary>获取该特性支持的全部状态。</summary>
    /// <returns>可选状态的数组。</returns>
    public Task<T[]> GetAllStatesAsync() => _feature.GetAllStatesAsync();

    /// <summary>创建该步骤的深拷贝，具体实例化由子类完成。</summary>
    /// <returns>新的步骤实例。</returns>
    public abstract IAutomationStep DeepCopy();
}

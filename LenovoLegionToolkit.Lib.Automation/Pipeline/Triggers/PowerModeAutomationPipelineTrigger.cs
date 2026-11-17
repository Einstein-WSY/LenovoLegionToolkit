using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;
using LenovoLegionToolkit.Lib.Features;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：性能模式切换到指定模式时执行。
/// 触发时机为性能模式变更事件，且切换后的模式与配置一致。
/// </summary>
/// <param name="powerModeState">关注的性能模式。</param>
[method: JsonConstructor]
public class PowerModeAutomationPipelineTrigger(PowerModeState powerModeState) : IPowerModeAutomationPipelineTrigger
{
    /// <summary>界面显示名称：当性能模式切换时。</summary>
    [JsonIgnore]
    public string DisplayName => Resource.PowerModeAutomationPipelineTrigger_DisplayName;

    /// <summary>关注的性能模式。</summary>
    public PowerModeState PowerModeState { get; } = powerModeState;

    /// <summary>
    /// 判断事件是否触发：事件必须是性能模式变更事件，且新模式与配置相同。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>切换到关注的模式时返回 true。</returns>
    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        if (automationEvent is not PowerModeAutomationEvent e)
            return Task.FromResult(false);

        var result = e.PowerModeState == PowerModeState;
        return Task.FromResult(result);
    }

    /// <summary>
    /// 查询当前状态：通过 PowerModeFeature 读取实际生效的性能模式并比较。
    /// </summary>
    /// <returns>当前模式与配置一致时返回 true。</returns>
    public async Task<bool> IsMatchingState()
    {
        var feature = IoCContainer.Resolve<PowerModeFeature>();
        return await feature.GetStateAsync().ConfigureAwait(false) == PowerModeState;
    }

    /// <summary>向执行环境写入性能模式。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment) => environment.PowerMode = PowerModeState;

    /// <summary>创建深拷贝，保留性能模式配置。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new PowerModeAutomationPipelineTrigger(PowerModeState);

    /// <summary>以新的性能模式创建副本（供界面编辑后重建触发器）。</summary>
    /// <param name="powerModeState">新的性能模式。</param>
    /// <returns>新的触发器实例。</returns>
    public IPowerModeAutomationPipelineTrigger DeepCopy(PowerModeState powerModeState) => new PowerModeAutomationPipelineTrigger(powerModeState);

    /// <summary>按性能模式比较是否等价。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>类型相同且模式一致时返回 true。</returns>
    public override bool Equals(object? obj)
    {
        return obj is PowerModeAutomationPipelineTrigger t && PowerModeState == t.PowerModeState;
    }

    /// <summary>基于性能模式生成哈希值。</summary>
    /// <returns>哈希值。</returns>
    public override int GetHashCode() => HashCode.Combine(PowerModeState);

    /// <summary>输出便于日志排查的文本表示。</summary>
    /// <returns>包含性能模式的字符串。</returns>
    public override string ToString() => $"{nameof(PowerModeState)}: {PowerModeState}";
}

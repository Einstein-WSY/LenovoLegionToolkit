using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;
using LenovoLegionToolkit.Lib.System;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：电源适配器断开时执行。
/// 触发时机为电源状态变化且由适配器插拔引起，或应用启动时（启动时补做一次状态判断）。
/// </summary>
public class ACAdapterDisconnectedAutomationPipelineTrigger : IPowerStateAutomationPipelineTrigger
{
    /// <summary>界面显示名称：当断开交流电源时。</summary>
    [JsonIgnore]
    public string DisplayName => Resource.ACAdapterDisconnectedAutomationPipelineTrigger_DisplayName;

    /// <summary>
    /// 判断事件是否触发：只接受由适配器状态变化引起的电源事件或启动事件，
    /// 并确认实时状态为“已断开”，以排除插拔瞬间的误判。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>适配器处于断开状态时返回 true。</returns>
    public async Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        if (automationEvent is not (PowerStateAutomationEvent { PowerStateEvent: PowerStateEvent.StatusChange, PowerAdapterStateChanged: true } or StartupAutomationEvent))
            return false;

        var status = await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false);
        return status == PowerAdapterStatus.Disconnected;
    }

    /// <summary>
    /// 查询当前电源适配器是否已断开，用于复合触发器校验其余子条件。
    /// </summary>
    /// <returns>已断开返回 true。</returns>
    public async Task<bool> IsMatchingState()
    {
        var status = await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false);
        return status == PowerAdapterStatus.Disconnected;
    }

    /// <summary>向执行环境写入“未接入电源适配器”。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment) => environment.AcAdapterConnected = false;

    /// <summary>创建深拷贝。该触发器无可变配置，直接新建实例即可。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new ACAdapterDisconnectedAutomationPipelineTrigger();

    /// <summary>同类触发器彼此等价，因此只比较类型。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>类型相同时返回 true。</returns>
    public override bool Equals(object? obj) => obj is ACAdapterDisconnectedAutomationPipelineTrigger;

    /// <summary>基于显示名称生成哈希值。</summary>
    /// <returns>哈希值。</returns>
    public override int GetHashCode() => HashCode.Combine(DisplayName);
}

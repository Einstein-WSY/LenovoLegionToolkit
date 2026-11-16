using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;
using LenovoLegionToolkit.Lib.System;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：电源适配器接入时执行。
/// 触发时机为电源状态变化且确实由适配器插拔引起，或应用启动时（启动时补做一次状态判断）。
/// </summary>
public class ACAdapterConnectedAutomationPipelineTrigger : IPowerStateAutomationPipelineTrigger
{
    /// <summary>界面显示名称：当接入交流电源时。</summary>
    [JsonIgnore]
    public string DisplayName => Resource.ACAdapterConnectedAutomationPipelineTrigger_DisplayName;

    /// <summary>
    /// 判断事件是否触发：只接受由适配器状态变化引起的电源事件或启动事件，
    /// 并再次查询实时状态为“已连接”，避免误判为断开瞬间的抖动。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>适配器处于已连接状态时返回 true。</returns>
    public async Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        if (automationEvent is not (PowerStateAutomationEvent { PowerStateEvent: PowerStateEvent.StatusChange, PowerAdapterStateChanged: true } or StartupAutomationEvent))
            return false;

        var status = await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false);
        return status == PowerAdapterStatus.Connected;
    }

    /// <summary>
    /// 查询当前电源适配器是否已连接，用于复合触发器校验其余子条件。
    /// </summary>
    /// <returns>已连接返回 true。</returns>
    public async Task<bool> IsMatchingState()
    {
        var status = await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false);
        return status == PowerAdapterStatus.Connected;
    }

    /// <summary>向执行环境写入“已接入电源适配器”。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment) => environment.AcAdapterConnected = true;

    /// <summary>创建深拷贝。该触发器无可变配置，直接新建实例即可。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new ACAdapterConnectedAutomationPipelineTrigger();

    /// <summary>同类触发器彼此等价，因此只比较类型。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>类型相同时返回 true。</returns>
    public override bool Equals(object? obj) => obj is ACAdapterConnectedAutomationPipelineTrigger;

    /// <summary>基于显示名称生成哈希值。</summary>
    /// <returns>哈希值。</returns>
    public override int GetHashCode() => HashCode.Combine(DisplayName);
}

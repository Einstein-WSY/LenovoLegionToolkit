using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;
using LenovoLegionToolkit.Lib.System;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：接入低功率电源适配器时执行。
/// 低功率适配器通常指功率不足以支撑满血性能的充电器（如小功率 USB-C 电源），
/// 触发时机为任意电源状态事件或应用启动时，并确认当前接入的适配器为低功率。
/// </summary>
public class LowWattageACAdapterConnectedAutomationPipelineTrigger : IPowerStateAutomationPipelineTrigger
{
    /// <summary>界面显示名称：当接入低功率交流电源时。</summary>
    [JsonIgnore]
    public string DisplayName => Resource.LowWattageACAdapterConnectedAutomationPipelineTrigger_DisplayName;

    /// <summary>
    /// 判断事件是否触发：接受所有电源状态事件（不限于适配器插拔），
    /// 随后确认当前适配器状态确为“已连接但功率较低”。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>当前接入的是低功率适配器时返回 true。</returns>
    public async Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        if (automationEvent is not (PowerStateAutomationEvent or StartupAutomationEvent))
            return false;

        var result = await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false);
        return result == PowerAdapterStatus.ConnectedLowWattage;
    }

    /// <summary>
    /// 查询当前接入的适配器是否为低功率。
    /// </summary>
    /// <returns>为低功率适配器时返回 true。</returns>
    public async Task<bool> IsMatchingState()
    {
        var result = await Power.IsPowerAdapterConnectedAsync().ConfigureAwait(false);
        return result == PowerAdapterStatus.ConnectedLowWattage;
    }

    /// <summary>
    /// 向执行环境写入“已接入电源适配器”且“适配器为低功率”。
    /// </summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment)
    {
        environment.AcAdapterConnected = true;
        environment.LowPowerAcAdapter = true;
    }

    /// <summary>创建深拷贝。该触发器无可变配置。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new LowWattageACAdapterConnectedAutomationPipelineTrigger();

    /// <summary>同类触发器彼此等价，只比较类型。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>类型相同时返回 true。</returns>
    public override bool Equals(object? obj) => obj is LowWattageACAdapterConnectedAutomationPipelineTrigger;

    /// <summary>基于显示名称生成哈希值。</summary>
    /// <returns>哈希值。</returns>
    public override int GetHashCode() => HashCode.Combine(DisplayName);
}

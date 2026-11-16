using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;
using LenovoLegionToolkit.Lib.System;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：Wi-Fi 断开连接时执行。
/// 触发时机为 Wi-Fi 监听器报告连接状态变为“未连接”。
/// </summary>
public class WiFiDisconnectedAutomationPipelineTrigger : IWiFiDisconnectedPipelineTrigger
{
    /// <summary>界面显示名称：当 Wi-Fi 断开时。</summary>
    public string DisplayName => Resource.WiFiDisconnectedAutomationPipelineTrigger_DisplayName;

    /// <summary>
    /// 判断事件是否触发：事件必须是 Wi-Fi 状态变化且为“未连接”。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>Wi-Fi 断开时返回 true。</returns>
    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        var result = automationEvent is WiFiAutomationEvent { IsConnected: false };
        return Task.FromResult(result);
    }

    /// <summary>
    /// 查询当前状态：取不到当前连接的 SSID 即视为未连接。
    /// </summary>
    /// <returns>当前未连接任何 Wi-Fi 时返回 true。</returns>
    public Task<bool> IsMatchingState()
    {
        var ssid = WiFi.GetConnectedNetworkSsid();
        return Task.FromResult(ssid is null);
    }

    /// <summary>向执行环境写入“Wi-Fi 未连接”。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment) => environment.WiFiConnected = false;

    /// <summary>创建深拷贝。该触发器无可变配置。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new WiFiDisconnectedAutomationPipelineTrigger();

    /// <summary>同类触发器彼此等价，只比较类型。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>类型相同时返回 true。</returns>
    public override bool Equals(object? obj) => obj is WiFiDisconnectedAutomationPipelineTrigger;

    /// <summary>基于显示名称生成哈希值。</summary>
    /// <returns>哈希值。</returns>
    public override int GetHashCode() => HashCode.Combine(DisplayName);
}

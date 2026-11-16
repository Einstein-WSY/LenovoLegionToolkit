using System;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：连接到指定 Wi-Fi 网络时执行。
/// SSID 列表为空表示“连接到任意网络”，非空时要求命中所列网络之一。
/// </summary>
/// <param name="ssids">关注的 SSID 列表；空数组表示任意网络。</param>
[method: JsonConstructor]
public class WiFiConnectedAutomationPipelineTrigger(string[] ssids) : IWiFiConnectedPipelineTrigger
{
    /// <summary>界面显示名称：当 Wi-Fi 连接时。</summary>
    public string DisplayName => Resource.WiFiConnectedAutomationPipelineTrigger_DisplayName;

    /// <summary>关注的 SSID 列表；为空表示任意网络。</summary>
    public string[] Ssids { get; } = ssids;

    /// <summary>
    /// 判断事件是否触发：事件必须是“已连接”，且未限制 SSID 或 SSID 命中列表。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>连接到关注的网络时返回 true。</returns>
    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        if (automationEvent is not WiFiAutomationEvent { IsConnected: true } e)
            return Task.FromResult(false);

        return Task.FromResult(Ssids.IsEmpty() || Ssids.Contains(e.Ssid));
    }

    /// <summary>
    /// 查询当前状态：已连接且 SSID 命中列表（列表为空时只要已连接即可）。
    /// </summary>
    /// <returns>当前连接的网络满足条件时返回 true。</returns>
    public Task<bool> IsMatchingState()
    {
        var ssid = WiFi.GetConnectedNetworkSsid();

        // 列表为空代表不限网络，只要能取到 SSID 即视为已连接。
        if (Ssids.IsEmpty() && ssid is not null)
            return Task.FromResult(true);

        return Task.FromResult(Ssids.Contains(ssid));
    }

    /// <summary>向执行环境写入 Wi-Fi 已连接及配置的 SSID 列表。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment)
    {
        environment.WiFiConnected = true;
        environment.WiFiSsid = string.Join(",", Ssids);
    }

    /// <summary>创建深拷贝，保留 SSID 列表。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new WiFiConnectedAutomationPipelineTrigger(Ssids);

    /// <summary>以新的 SSID 列表创建副本（供界面编辑后重建触发器）。</summary>
    /// <param name="ssids">新的 SSID 列表。</param>
    /// <returns>新的触发器实例。</returns>
    public IWiFiConnectedPipelineTrigger DeepCopy(string[] ssids) => new WiFiConnectedAutomationPipelineTrigger(ssids);

    /// <summary>按 SSID 序列比较是否等价。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>类型相同且 SSID 顺序一致时返回 true。</returns>
    public override bool Equals(object? obj) => obj is WiFiConnectedAutomationPipelineTrigger t && Ssids.SequenceEqual(t.Ssids);

    /// <summary>基于 SSID 数组生成哈希值。</summary>
    /// <returns>哈希值。</returns>
    public override int GetHashCode() => HashCode.Combine(Ssids);

    /// <summary>输出便于日志排查的文本表示。</summary>
    /// <returns>包含 SSID 列表的字符串。</returns>
    public override string ToString() => $"{nameof(Ssids)}: {string.Join(",", Ssids)}";
}

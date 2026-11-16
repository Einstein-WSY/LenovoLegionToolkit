using System;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：指定设备接入时执行。
/// 触发时机为系统上报设备连接消息，且设备实例 ID 命中配置的关注列表。
/// </summary>
/// <param name="instanceIds">关注的设备实例 ID 列表；为 null 时视为空列表（永不匹配）。</param>
[method: JsonConstructor]
public class DeviceConnectedAutomationPipelineTrigger(string[]? instanceIds) : IDeviceAutomationPipelineTrigger
{
    /// <summary>界面显示名称：当设备连接时。</summary>
    [JsonIgnore]
    public string DisplayName => Resource.DeviceConnectedAutomationPipelineTrigger_DisplayName;

    /// <summary>关注的设备实例 ID 列表。</summary>
    public string[] InstanceIds { get; } = instanceIds ?? [];

    /// <summary>
    /// 判断事件是否触发：事件必须是设备连接消息，且消息负载中的实例 ID 在关注列表内。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>命中关注设备时返回 true。</returns>
    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        if (automationEvent is not NativeWindowsMessageEvent { Message: NativeWindowsMessage.DeviceConnected, Data: string deviceInstanceId })
            return Task.FromResult(false);

        return Task.FromResult(InstanceIds.Contains(deviceInstanceId));
    }

    /// <summary>
    /// 查询当前状态：存在任一关注设备处于已连接状态即视为满足。
    /// </summary>
    /// <returns>至少一个关注设备已连接时返回 true。</returns>
    public Task<bool> IsMatchingState()
    {
        // 先过滤掉已断开的设备，再与关注列表取交集，避免残留记录造成误判。
        var result = Devices.GetAll()
            .Where(d => !d.IsDisconnected)
            .Select(d => d.DeviceInstanceId)
            .Intersect(InstanceIds)
            .Any();
        return Task.FromResult(result);
    }

    /// <summary>向执行环境写入设备已连接及其实例 ID 列表。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment)
    {
        environment.DeviceConnected = true;
        environment.DeviceInstanceIds = InstanceIds;
    }

    /// <summary>创建深拷贝，保留关注列表。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new DeviceConnectedAutomationPipelineTrigger(InstanceIds);

    /// <summary>以新的设备 ID 集合创建副本（供界面编辑后重建触发器）。</summary>
    /// <param name="instanceIds">新的设备实例 ID 列表。</param>
    /// <returns>新的触发器实例。</returns>
    public IDeviceAutomationPipelineTrigger DeepCopy(string[] instanceIds) => new DeviceConnectedAutomationPipelineTrigger(instanceIds);

    /// <summary>按设备 ID 序列比较是否等价。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>类型相同且 ID 列表元素顺序一致时返回 true。</returns>
    public override bool Equals(object? obj) => obj is DeviceConnectedAutomationPipelineTrigger t && InstanceIds.SequenceEqual(t.InstanceIds);

    /// <summary>逐个累加设备 ID 生成哈希值。</summary>
    /// <returns>哈希值。</returns>
    public override int GetHashCode()
    {
        var hc = new HashCode();
        InstanceIds.ForEach(id => hc.Add(id));
        return hc.ToHashCode();
    }

    /// <summary>输出便于日志排查的文本表示。</summary>
    /// <returns>包含设备 ID 列表的字符串。</returns>
    public override string ToString() => $"{nameof(InstanceIds)}: {string.Join(", ", InstanceIds)}";
}

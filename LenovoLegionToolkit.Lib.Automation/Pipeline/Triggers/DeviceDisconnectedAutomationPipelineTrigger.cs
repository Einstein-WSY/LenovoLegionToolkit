using System;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：指定设备断开时执行。
/// 触发时机为系统上报设备断开消息，且设备实例 ID 命中配置的关注列表。
/// </summary>
/// <param name="instanceIds">关注的设备实例 ID 列表；为 null 时视为空列表（永不匹配）。</param>
[method: JsonConstructor]
public class DeviceDisconnectedAutomationPipelineTrigger(string[]? instanceIds) : IDeviceAutomationPipelineTrigger
{
    /// <summary>界面显示名称：当设备断开时。</summary>
    [JsonIgnore]
    public string DisplayName => Resource.DeviceDisconnectedAutomationPipelineTrigger_DisplayName;

    /// <summary>关注的设备实例 ID 列表。</summary>
    public string[] InstanceIds { get; } = instanceIds ?? [];

    /// <summary>
    /// 判断事件是否触发：事件必须是设备断开消息，且消息负载中的实例 ID 在关注列表内。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>关注设备断开时返回 true。</returns>
    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        if (automationEvent is not NativeWindowsMessageEvent { Message: NativeWindowsMessage.DeviceDisconnected, Data: string deviceInstanceId })
            return Task.FromResult(false);

        return Task.FromResult(InstanceIds.Contains(deviceInstanceId));
    }

    /// <summary>
    /// 查询当前状态：关注设备中没有任何一个处于连接状态即视为满足。
    /// </summary>
    /// <returns>全部关注设备均未连接时返回 true。</returns>
    public Task<bool> IsMatchingState()
    {
        // 与“连接”触发器相反：已连接设备与关注列表的交集为空，才说明关注设备都不在线。
        var result = Devices.GetAll()
            .Where(d => !d.IsDisconnected)
            .Select(d => d.DeviceInstanceId)
            .Intersect(InstanceIds)
            .IsEmpty();
        return Task.FromResult(result);
    }

    /// <summary>向执行环境写入设备未连接及相关的实例 ID 列表。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment)
    {
        environment.DeviceConnected = false;
        environment.DeviceInstanceIds = InstanceIds;
    }

    /// <summary>创建深拷贝，保留关注列表。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new DeviceDisconnectedAutomationPipelineTrigger(InstanceIds);

    /// <summary>以新的设备 ID 集合创建副本（供界面编辑后重建触发器）。</summary>
    /// <param name="instanceIds">新的设备实例 ID 列表。</param>
    /// <returns>新的触发器实例。</returns>
    public IDeviceAutomationPipelineTrigger DeepCopy(string[] instanceIds) => new DeviceDisconnectedAutomationPipelineTrigger(instanceIds);

    /// <summary>按设备 ID 序列比较是否等价。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>类型相同且 ID 列表元素顺序一致时返回 true。</returns>
    public override bool Equals(object? obj) => obj is DeviceDisconnectedAutomationPipelineTrigger t && InstanceIds.SequenceEqual(t.InstanceIds);

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

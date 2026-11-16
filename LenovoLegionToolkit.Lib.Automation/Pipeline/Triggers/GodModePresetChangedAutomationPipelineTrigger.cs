using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;
using LenovoLegionToolkit.Lib.Controllers.GodMode;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：自定义模式（God Mode）切换到指定预设时执行。
/// 触发时机为预设变更事件，且变更后的预设 ID 与配置一致。
/// </summary>
/// <param name="presetId">关注的预设 ID。</param>
[method: JsonConstructor]
public class GodModePresetChangedAutomationPipelineTrigger(Guid presetId)
    : IGodModePresetChangedAutomationPipelineTrigger
{
    /// <summary>界面显示名称：当自定义模式预设切换时。</summary>
    [JsonIgnore]
    public string DisplayName => Resource.GodModePresetChangedAutomationPipelineTrigger_DisplayName;

    /// <summary>关注的预设 ID。</summary>
    public Guid PresetId { get; } = presetId;

    /// <summary>
    /// 判断事件是否触发：事件必须是预设变更事件，且预设 ID 与配置相同。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>切换到关注的预设时返回 true。</returns>
    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        if (automationEvent is not CustomModePresetAutomationEvent e)
            return Task.FromResult(false);

        return Task.FromResult(e.Id == PresetId);
    }

    /// <summary>
    /// 查询当前状态：向控制器请求当前激活的预设 ID 并比较。
    /// </summary>
    /// <returns>当前激活的预设与配置一致时返回 true。</returns>
    public async Task<bool> IsMatchingState()
    {
        var controller = IoCContainer.Resolve<GodModeController>();
        return PresetId == await controller.GetActivePresetIdAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 该触发器不产生对应的环境变量，因此不向执行环境写入任何值。
    /// </summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment) { /* Ignored */ }

    /// <summary>创建深拷贝，保留预设 ID。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new GodModePresetChangedAutomationPipelineTrigger(PresetId);

    /// <summary>以新的预设 ID 创建副本（供界面编辑后重建触发器）。</summary>
    /// <param name="presetId">新的预设 ID。</param>
    /// <returns>新的触发器实例。</returns>
    public IGodModePresetChangedAutomationPipelineTrigger DeepCopy(Guid presetId) => new GodModePresetChangedAutomationPipelineTrigger(presetId);

    /// <summary>按预设 ID 比较是否等价。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>类型相同且预设 ID 一致时返回 true。</returns>
    public override bool Equals(object? obj)
    {
        return obj is GodModePresetChangedAutomationPipelineTrigger t && PresetId == t.PresetId;
    }

    /// <summary>基于预设 ID 生成哈希值。</summary>
    /// <returns>哈希值。</returns>
    public override int GetHashCode() => HashCode.Combine(PresetId);

    /// <summary>输出便于日志排查的文本表示。</summary>
    /// <returns>包含预设 ID 的字符串。</returns>
    public override string ToString() => $"{nameof(PresetId)}: {PresetId}";
}

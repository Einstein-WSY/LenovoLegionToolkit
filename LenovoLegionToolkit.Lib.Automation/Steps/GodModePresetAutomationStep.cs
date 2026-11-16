using System;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers.GodMode;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Utils;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：切换极客模式（God Mode）的自定义预设。
/// 若当前电源模式已经是极客模式，还会立即重新应用预设使参数生效。
/// </summary>
/// <param name="presetId">目标预设 Id。</param>
[method: JsonConstructor]
public class GodModePresetAutomationStep(Guid presetId)
    : IAutomationStep
{
    private readonly PowerModeFeature _feature = IoCContainer.Resolve<PowerModeFeature>();
    private readonly GodModeController _controller = IoCContainer.Resolve<GodModeController>();

    /// <summary>目标极客模式预设 Id。</summary>
    public Guid PresetId { get; } = presetId;

    /// <summary>判断设备是否支持极客模式（依据机型能力信息）。</summary>
    /// <returns>支持时返回 <see langword="true"/>。</returns>
    public async Task<bool> IsSupportedAsync()
    {
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        return mi.Properties.SupportsGodMode;
    }

    /// <summary>读取当前极客模式配置（包含全部预设及当前激活预设）。</summary>
    /// <returns>极客模式状态。</returns>
    public Task<GodModeState> GetStateAsync() => _controller.GetStateAsync();

    /// <summary>
    /// 执行步骤：把指定预设设为激活预设；若当前已是极客模式则立即应用参数。
    /// </summary>
    /// <param name="context">流水线执行上下文。</param>
    /// <param name="environment">环境变量集合。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>预设切换完成的任务。</returns>
    public async Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        var state = await _controller.GetStateAsync().ConfigureAwait(false);
        // 预设不存在时跳过，避免写入无效的激活预设 Id
        if (!state.Presets.ContainsKey(PresetId))
            return;

        // 记录类型，用 with 表达式生成副本，仅替换激活的预设 Id
        var newState = state with { ActivePresetId = PresetId };

        await _controller.SetStateAsync(newState).ConfigureAwait(false);

        // 仅当已处于极客模式时才立即应用参数；其他模式会在切换时再应用
        if (await _feature.GetStateAsync().ConfigureAwait(false) == PowerModeState.GodMode)
            await _controller.ApplyStateAsync().ConfigureAwait(false);
    }

    /// <summary>创建该步骤的深拷贝，保留预设 Id。</summary>
    /// <returns>新的步骤实例。</returns>
    public IAutomationStep DeepCopy() => new GodModePresetAutomationStep(PresetId);
}

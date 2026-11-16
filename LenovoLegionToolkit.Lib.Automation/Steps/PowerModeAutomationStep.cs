using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：设置电源模式（安静、均衡、性能、自定义、极客模式等）。
/// 执行时把该特性切换到目标状态，并广播特性状态变更消息。
/// </summary>
/// <param name="state">目标电源模式。</param>
[method: JsonConstructor]
public class PowerModeAutomationStep(PowerModeState state)
    : AbstractFeatureAutomationStep<PowerModeState>(state)
{
    /// <summary>创建当前步骤的深拷贝，供流水线复制与编辑时使用。</summary>
    public override IAutomationStep DeepCopy() => new PowerModeAutomationStep(State);
}

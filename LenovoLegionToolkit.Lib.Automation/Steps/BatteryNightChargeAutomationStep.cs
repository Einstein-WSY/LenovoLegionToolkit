using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：设置电池夜间充电功能（夜间暂缓充电，避免长时间满电搁置）。
/// 执行时把该特性切换到目标状态，并广播特性状态变更消息。
/// </summary>
/// <param name="state">目标状态：开启或关闭夜间充电。</param>
[method: JsonConstructor]
public class BatteryNightChargeAutomationStep(BatteryNightChargeState state)
    : AbstractFeatureAutomationStep<BatteryNightChargeState>(state)
{
    /// <summary>创建当前步骤的深拷贝，供流水线复制与编辑时使用。</summary>
    public override IAutomationStep DeepCopy() => new BatteryNightChargeAutomationStep(State);
}

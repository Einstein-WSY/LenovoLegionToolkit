using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：设置显卡混合模式（核显/独显混合输出或独显直连）。
/// 执行时把该特性切换到目标状态，并广播特性状态变更消息。
/// </summary>
/// <param name="state">目标混合模式状态。</param>
[method: JsonConstructor]
public class HybridModeAutomationStep(HybridModeState state)
    : AbstractFeatureAutomationStep<HybridModeState>(state)
{
    /// <summary>创建当前步骤的深拷贝，供流水线复制与编辑时使用。</summary>
    public override IAutomationStep DeepCopy() => new HybridModeAutomationStep(State);
}

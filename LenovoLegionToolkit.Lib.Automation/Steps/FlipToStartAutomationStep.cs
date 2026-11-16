using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：设置开盖自动开机（Flip to Start）。
/// 执行时把该特性切换到目标状态，并广播特性状态变更消息。
/// </summary>
/// <param name="state">目标状态：开启或关闭开盖开机。</param>
[method: JsonConstructor]
public class FlipToStartAutomationStep(FlipToStartState state)
    : AbstractFeatureAutomationStep<FlipToStartState>(state)
{
    /// <summary>创建当前步骤的深拷贝，供流水线复制与编辑时使用。</summary>
    public override IAutomationStep DeepCopy() => new FlipToStartAutomationStep(State);
}

using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：设置 A 面 Logo 背光（顶盖徽标灯）的开关状态。
/// 执行时把该特性切换到目标状态，并广播特性状态变更消息。
/// </summary>
/// <param name="state">目标状态：开启或关闭 Logo 背光。</param>
[method: JsonConstructor]
public class PanelLogoBacklightAutomationStep(PanelLogoBacklightState state)
    : AbstractFeatureAutomationStep<PanelLogoBacklightState>(state)
{
    /// <summary>创建当前步骤的深拷贝，供流水线复制与编辑时使用。</summary>
    public override IAutomationStep DeepCopy() => new PanelLogoBacklightAutomationStep(State);
}

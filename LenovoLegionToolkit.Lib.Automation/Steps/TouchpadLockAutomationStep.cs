using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：设置触摸板锁定（锁定后外接鼠标时避免误触）。
/// 执行时把该特性切换到目标状态，并广播特性状态变更消息。
/// </summary>
/// <param name="state">目标状态：锁定或解锁触摸板。</param>
[method: JsonConstructor]
public class TouchpadLockAutomationStep(TouchpadLockState state)
    : AbstractFeatureAutomationStep<TouchpadLockState>(state)
{
    /// <summary>创建当前步骤的深拷贝，供流水线复制与编辑时使用。</summary>
    public override IAutomationStep DeepCopy() => new TouchpadLockAutomationStep(State);
}

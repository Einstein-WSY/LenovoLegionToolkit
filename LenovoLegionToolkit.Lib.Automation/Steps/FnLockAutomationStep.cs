using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：设置 Fn 锁定（FnLock），决定 F1-F12 键默认执行功能键还是多媒体键。
/// 执行时把该特性切换到目标状态，并广播特性状态变更消息。
/// </summary>
/// <param name="state">目标状态：Fn 锁定开启或关闭。</param>
[method: JsonConstructor]
public class FnLockAutomationStep(FnLockState state)
    : AbstractFeatureAutomationStep<FnLockState>(state)
{
    /// <summary>创建当前步骤的深拷贝，供流水线复制与编辑时使用。</summary>
    public override IAutomationStep DeepCopy() => new FnLockAutomationStep(State);
}

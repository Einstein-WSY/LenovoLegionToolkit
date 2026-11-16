using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：设置 Win 键锁定（游戏中防止误触 Win 键退回桌面）。
/// 执行时把该特性切换到目标状态，并广播特性状态变更消息。
/// </summary>
/// <param name="state">目标状态：锁定或解锁 Win 键。</param>
[method: JsonConstructor]
public class WinKeyAutomationStep(WinKeyState state)
    : AbstractFeatureAutomationStep<WinKeyState>(state)
{
    /// <summary>创建当前步骤的深拷贝，供流水线复制与编辑时使用。</summary>
    public override IAutomationStep DeepCopy() => new WinKeyAutomationStep(State);
}

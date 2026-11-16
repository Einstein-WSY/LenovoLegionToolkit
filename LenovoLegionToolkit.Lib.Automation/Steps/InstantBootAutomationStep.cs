using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：设置快速启动（Instant Boot）。
/// 执行时把该特性切换到目标状态，并广播特性状态变更消息。
/// </summary>
/// <param name="state">目标状态：开启或关闭快速启动。</param>
[method: JsonConstructor]
public class InstantBootAutomationStep(InstantBootState state)
    : AbstractFeatureAutomationStep<InstantBootState>(state)
{
    /// <summary>创建当前步骤的深拷贝，供流水线复制与编辑时使用。</summary>
    public override IAutomationStep DeepCopy() => new InstantBootAutomationStep(State);
}

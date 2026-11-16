namespace LenovoLegionToolkit.Lib.Features.FlipToStart;

/// <summary>
/// 开盖即开机（Flip To Start）组合特性。
/// 优先使用 Capability 实现，不支持时回退到 UEFI 变量实现。
/// </summary>
/// <param name="feature1">基于 Capability 的实现（优先级较高）。</param>
/// <param name="feature2">基于 UEFI 变量的实现（回退方案）。</param>
public class FlipToStartFeature(FlipToStartCapabilityFeature feature1, FlipToStartUEFIFeature feature2)
    : AbstractCompositeFeature<FlipToStartState>(feature1, feature2);

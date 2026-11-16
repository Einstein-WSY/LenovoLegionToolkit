namespace LenovoLegionToolkit.Lib.Features.InstantBoot;

/// <summary>
/// 即时开机（Instant Boot）组合特性。
/// 优先使用 Capability 实现，不支持时回退到 FeatureFlags 实现。
/// </summary>
/// <param name="feature1">基于 CapabilityData 的实现（优先级较高）。</param>
/// <param name="feature2">基于 FeatureFlags 的实现（回退方案）。</param>
public class InstantBootFeature(InstantBootCapabilityFeature feature1, InstantBootFeatureFlagsFeature feature2)
    : AbstractCompositeFeature<InstantBootState>(feature1, feature2);

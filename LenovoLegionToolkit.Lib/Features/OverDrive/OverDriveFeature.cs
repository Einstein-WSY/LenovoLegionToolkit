namespace LenovoLegionToolkit.Lib.Features.OverDrive;

/// <summary>
/// 屏幕超频（OverDrive）组合特性。
/// 优先使用 Capability 实现，不支持时回退到 GameZone 实现。
/// </summary>
/// <param name="feature1">基于 CapabilityData 的实现（优先级较高）。</param>
/// <param name="feature2">基于 GameZone WMI 的实现（回退方案）。</param>
public class OverDriveFeature(OverDriveCapabilityFeature feature1, OverDriveGameZoneFeature feature2)
    : AbstractCompositeFeature<OverDriveState>(feature1, feature2);

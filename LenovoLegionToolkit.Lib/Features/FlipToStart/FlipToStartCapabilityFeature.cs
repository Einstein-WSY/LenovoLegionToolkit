namespace LenovoLegionToolkit.Lib.Features.FlipToStart;

/// <summary>
/// 开盖即开机（Flip To Start）特性 —— 基于 Capability（能力数据）机制的实现。
/// 适用于支持 CapabilityData 的新机型，通过 WMI Lenovo_Other_Method 读写。
/// </summary>
public class FlipToStartCapabilityFeature() : AbstractCapabilityFeature<FlipToStartState>(CapabilityID.FlipToStart);


namespace LenovoLegionToolkit.Lib.Features.OverDrive;

/// <summary>
/// 屏幕超频（OverDrive，提高屏幕响应速度）特性 —— 基于 CapabilityData 机制的新机型实现。
/// 通过 WMI Lenovo_Other_Method 读写 CapabilityID.OverDrive。
/// </summary>
public class OverDriveCapabilityFeature() : AbstractCapabilityFeature<OverDriveState>(CapabilityID.OverDrive);

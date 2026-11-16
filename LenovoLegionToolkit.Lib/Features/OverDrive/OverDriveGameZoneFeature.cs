using LenovoLegionToolkit.Lib.System.Management;

namespace LenovoLegionToolkit.Lib.Features.OverDrive;

/// <summary>
/// 屏幕超频（OverDrive）特性 —— 基于 GameZone WMI 的实现（老机型使用）。
/// 通过 WMI Lenovo_GameZone_Data 的 ODStatus 接口读写，
/// 支持性由 IsSupportOD 方法判定。
/// </summary>
public class OverDriveGameZoneFeature()
    : AbstractWmiFeature<OverDriveState>(WMI.LenovoGameZoneData.GetODStatusAsync, WMI.LenovoGameZoneData.SetODStatusAsync, WMI.LenovoGameZoneData.IsSupportODAsync);

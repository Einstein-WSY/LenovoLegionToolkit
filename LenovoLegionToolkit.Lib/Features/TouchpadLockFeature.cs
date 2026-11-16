using LenovoLegionToolkit.Lib.System.Management;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// 触控板锁定特性。
/// 通过 WMI Lenovo_GameZone_Data 的 TPStatus 接口读写触控板开关状态，
/// 支持性由 IsSupportDisableTP 方法判定。
/// </summary>
public class TouchpadLockFeature()
    : AbstractWmiFeature<TouchpadLockState>(WMI.LenovoGameZoneData.GetTPStatusStatusAsync, WMI.LenovoGameZoneData.SetTPStatusAsync, WMI.LenovoGameZoneData.IsSupportDisableTPAsync);

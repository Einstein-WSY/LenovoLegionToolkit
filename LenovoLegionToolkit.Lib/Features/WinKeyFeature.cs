using LenovoLegionToolkit.Lib.System.Management;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// Windows 键锁定特性（游戏时禁用 Win 键以防误触）。
/// 通过 WMI Lenovo_GameZone_Data 的 WinKeyStatus 接口读写，
/// 支持性由 IsSupportDisableWinKey 方法判定。
/// </summary>
public class WinKeyFeature()
    : AbstractWmiFeature<WinKeyState>(WMI.LenovoGameZoneData.GetWinKeyStatusAsync, WMI.LenovoGameZoneData.SetWinKeyStatusAsync, WMI.LenovoGameZoneData.IsSupportDisableWinKeyAsync);

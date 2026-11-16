using LenovoLegionToolkit.Lib.System.Management;

namespace LenovoLegionToolkit.Lib.Features.Hybrid;

/// <summary>
/// G-Sync（NVIDIA 可变刷新率）特性。
/// 通过 WMI Lenovo_GameZone_Data 的 GSyncStatus 接口读写。
/// 注意：开启 G-Sync 需要独显直连（即关闭混合模式），因此与混合模式状态互斥。
/// </summary>
public class GSyncFeature()
    : AbstractWmiFeature<GSyncState>(WMI.LenovoGameZoneData.GetGSyncStatusAsync, WMI.LenovoGameZoneData.SetGSyncStatusAsync, WMI.LenovoGameZoneData.IsSupportGSyncAsync);

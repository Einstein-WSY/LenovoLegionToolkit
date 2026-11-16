using LenovoLegionToolkit.Lib.System.Management;

namespace LenovoLegionToolkit.Lib.Features.Hybrid;

/// <summary>
/// iGPU 模式特性 —— 基于 GameZone WMI 的实现（多数老机型使用）。
/// 通过 WMI Lenovo_GameZone_Data 的 IGPUModeStatus 接口读写，
/// 支持性由 IsSupportIGPUMode 方法判定。
/// </summary>
public class IGPUModeGamezoneFeature()
    : AbstractWmiFeature<IGPUModeState>(WMI.LenovoGameZoneData.GetIGPUModeStatusAsync, WMI.LenovoGameZoneData.SetIGPUModeStatusAsync, WMI.LenovoGameZoneData.IsSupportIGPUModeAsync);

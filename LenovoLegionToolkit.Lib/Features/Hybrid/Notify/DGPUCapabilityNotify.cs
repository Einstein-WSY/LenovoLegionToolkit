using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features.Hybrid.Notify;

/// <summary>
/// dGPU 状态通知 —— 基于 CapabilityData 机制的新机型实现。
/// 通过 CapabilityID.GPUStatus 写入独显可用状态，
/// 通过 CapabilityID.GPUDidVid 读取独显的 VID/DID。
/// </summary>
public class DGPUCapabilityNotify : AbstractDGPUNotify
{
    /// <inheritdoc/>
    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            return mi is { Features.Source: MachineInformation.FeatureData.SourceType.CapabilityData, Properties.SupportsIGPUMode: true };
        }
        catch
        {
            return false;
        }
    }

    // 写入 Capability 的 GPUStatus：1 表示可用，0 表示已弹出
    protected override Task NotifyDGPUStatusAsync(bool state) => WMI.LenovoOtherMethod.SetFeatureValueAsync(CapabilityID.GPUStatus, state ? 1 : 0);

    /// <inheritdoc/>
    protected override async Task<HardwareId> GetDGPUHardwareIdAsync()
    {
        try
        {
            var value = await WMI.LenovoOtherMethod.GetFeatureValueAsync(CapabilityID.GPUDidVid).ConfigureAwait(false);
            // 低 16 位为厂商 ID（VID），高 16 位为设备 ID（DID）
            var vendorId = value & 0xFFFF;
            var deviceId = value >> 16;
            return new HardwareId($"{vendorId:X}", $"{deviceId:X}");
        }
        catch (Exception)
        {
            // 读取失败时返回空 ID，调用方据此判定独显不可用
            return HardwareId.Empty;
        }
    }
}

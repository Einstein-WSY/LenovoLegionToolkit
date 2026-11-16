using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features.Hybrid.Notify;

/// <summary>
/// dGPU 状态通知 —— 基于 FeatureFlags 机制的实验性实现。
/// 通过 WMI Lenovo_Other_Method 的 SetDGPUDeviceStatus / GetDGPUDeviceDIDVID 接口读写。
/// </summary>
public class DGPUFeatureFlagsNotify : AbstractDGPUNotify
{
    /// <inheritdoc/>
    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            return mi is { Features.Source: MachineInformation.FeatureData.SourceType.Flags, Properties.SupportsIGPUMode: true };
        }
        catch
        {
            return false;
        }
    }

    // 通过 FeatureFlags 接口设置 dGPU 设备状态
    protected override Task NotifyDGPUStatusAsync(bool state) => WMI.LenovoOtherMethod.SetDGPUDeviceStatusAsync(state);

    /// <inheritdoc/>
    protected override async Task<HardwareId> GetDGPUHardwareIdAsync()
    {
        try
        {
            return await WMI.LenovoOtherMethod.GetDGPUDeviceDIDVIDAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // 读取失败时返回空 ID，调用方据此判定独显不可用
            return HardwareId.Empty;
        }
    }
}

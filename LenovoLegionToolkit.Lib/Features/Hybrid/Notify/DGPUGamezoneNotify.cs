using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features.Hybrid.Notify;

/// <summary>
/// dGPU 状态通知 —— 基于 GameZone WMI 的稳定实现（多数机型使用）。
/// 通过 WMI Lenovo_GameZone_Data 的 NotifyDGPUStatus / GetDGPUHWId 接口读写。
/// </summary>
public class DGPUGamezoneNotify : AbstractDGPUNotify
{
    /// <inheritdoc/>
    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            // GameZone 实现只要求机型支持 iGPU 模式即可
            return mi is { Properties.SupportsIGPUMode: true };
        }
        catch
        {
            return false;
        }
    }

    // 通过 GameZone 接口通知 dGPU 状态：1 表示可用，0 表示已弹出
    protected override Task NotifyDGPUStatusAsync(bool state) => WMI.LenovoGameZoneData.NotifyDGPUStatusAsync(state ? 1 : 0);

    /// <inheritdoc/>
    protected override async Task<HardwareId> GetDGPUHardwareIdAsync()
    {
        try
        {
            return await WMI.LenovoGameZoneData.GetDGPUHWIdAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // 读取失败时返回空 ID，调用方据此判定独显不可用
            return HardwareId.Empty;
        }
    }
}

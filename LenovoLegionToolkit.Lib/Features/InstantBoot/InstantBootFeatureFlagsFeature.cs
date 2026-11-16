using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features.InstantBoot;

/// <summary>
/// 即时开机（Instant Boot）特性 —— 基于 FeatureFlags（设备当前支持特性位）的实现。
/// 通过 WMI Lenovo_Other_Method 的 Get/SetDeviceCurrentSupportFeature 接口按位读写：
/// bit5 表示 AC 适配器供电开机，bit6 表示 USB Power Delivery 供电开机。
/// </summary>
public class InstantBootFeatureFlagsFeature : IFeature<InstantBootState>
{
    // 特性位索引：AC 适配器供电、USB PD 供电
    private const int AC_INDEX = 5;
    private const int USB_POWER_DELIVERY_INDEX = 6;

    /// <inheritdoc/>
    public async Task<bool> IsSupportedAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            // 必须来源为 Flags 且两个能力位都置位
            return mi.Features.Source == MachineInformation.FeatureData.SourceType.Flags && mi.Features[CapabilityID.InstantBootAc] && mi.Features[CapabilityID.InstantBootUsbPowerDelivery];
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public Task<InstantBootState[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<InstantBootState>());

    /// <inheritdoc/>
    public async Task<InstantBootState> GetStateAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting state...");

        var flags = await WMI.LenovoOtherMethod.GetDeviceCurrentSupportFeatureAsync().ConfigureAwait(false);

        var acAdapter = flags.IsBitSet(AC_INDEX);
        var usbPowerDelivery = flags.IsBitSet(USB_POWER_DELIVERY_INDEX);

        // 两个特性位组合为四种状态
        var result = (acAdapter, usbPowerDelivery) switch
        {
            (true, true) => InstantBootState.AcAdapterAndUsbPowerDelivery,
            (true, false) => InstantBootState.AcAdapter,
            (false, true) => InstantBootState.UsbPowerDelivery,
            _ => InstantBootState.Off
        };

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State is {result}");

        return result;
    }

    /// <inheritdoc/>
    public async Task SetStateAsync(InstantBootState state)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting state to {state}...");

        // 把组合状态拆解为两个特性位值
        var (acAdapter, usbPowerDelivery) = state switch
        {
            InstantBootState.AcAdapterAndUsbPowerDelivery => (1, 1),
            InstantBootState.AcAdapter => (1, 0),
            InstantBootState.UsbPowerDelivery => (0, 1),
            _ => (0, 0)
        };

        // 按位索引分别写入；第一个参数为位索引，第二个参数为 0/1
        await WMI.LenovoOtherMethod.SetDeviceCurrentSupportFeatureAsync(AC_INDEX, acAdapter).ConfigureAwait(false);
        await WMI.LenovoOtherMethod.SetDeviceCurrentSupportFeatureAsync(USB_POWER_DELIVERY_INDEX, usbPowerDelivery).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Set state to {state}");
    }
}

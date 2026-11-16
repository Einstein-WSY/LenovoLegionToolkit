using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features.InstantBoot;

/// <summary>
/// 即时开机（Instant Boot，插电/供电即开机）特性 —— 基于 CapabilityData 机制的实现。
/// 由两个独立的 Capability 组合而成：AC 适配器供电、USB Power Delivery 供电；
/// 因此需要两者同时支持才视为整体支持，状态则是这两个开关的组合。
/// </summary>
public class InstantBootCapabilityFeature : IFeature<InstantBootState>
{
    // 内部使用简单的开/关枚举，最终再映射为对外的 InstantBootState
    private enum State
    {
        Off,
        On
    }

    // 分别对应"接入电源适配器即开机"和"USB PD 供电即开机"两个能力
    private class InstantBootAcFeature() : AbstractCapabilityFeature<State>(CapabilityID.InstantBootAc);
    private class InstantBootUsbPowerDeliveryFeature() : AbstractCapabilityFeature<State>(CapabilityID.InstantBootUsbPowerDelivery);

    private readonly InstantBootAcFeature _ac = new();
    private readonly InstantBootUsbPowerDeliveryFeature _usbPowerDelivery = new();

    /// <inheritdoc/>
    public async Task<bool> IsSupportedAsync()
    {
        // 只有两个能力都支持时，组合状态才完整可用
        return await _ac.IsSupportedAsync().ConfigureAwait(false) && await _usbPowerDelivery.IsSupportedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<InstantBootState[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<InstantBootState>());

    /// <inheritdoc/>
    public async Task<InstantBootState> GetStateAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting state...");

        var ac = await _ac.GetStateAsync().ConfigureAwait(false);
        var usbPowerDelivery = await _usbPowerDelivery.GetStateAsync().ConfigureAwait(false);

        // 两个布尔开关组合为四种状态
        var result = (ac, usbPowerDelivery) switch
        {
            (State.On, State.On) => InstantBootState.AcAdapterAndUsbPowerDelivery,
            (State.On, State.Off) => InstantBootState.AcAdapter,
            (State.Off, State.On) => InstantBootState.UsbPowerDelivery,
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

        // 把组合状态拆解为两个独立开关
        var (ac, usbPowerDelivery) = state switch
        {
            InstantBootState.AcAdapterAndUsbPowerDelivery => (State.On, State.On),
            InstantBootState.AcAdapter => (State.On, State.Off),
            InstantBootState.UsbPowerDelivery => (State.Off, State.On),
            _ => (State.Off, State.Off)
        };

        await _ac.SetStateAsync(ac).ConfigureAwait(false);
        await _usbPowerDelivery.SetStateAsync(usbPowerDelivery).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Set state to {state}");
    }
}

using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// 单级白色键盘背光特性（仅支持开/关，无亮度分级）。
/// 通过能量管理驱动 IOCTL_ENERGY_SETTINGS 控制。
/// </summary>
public class OneLevelWhiteKeyboardBacklightFeature() : AbstractDriverFeature<OneLevelWhiteKeyboardBacklightState>(Drivers.GetEnergy, Drivers.IOCTL_ENERGY_SETTINGS)
{
    /// <inheritdoc/>
    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            var outBuffer = await SendCodeAsync(DriverHandle(), ControlCode, GetInBufferValue()).ConfigureAwait(false);
            // bit4 置位表示本机型具备单级白色背光能力
            var result = ((int)outBuffer & 16) == 16;
            return result;
        }
        catch
        {
            return false;
        }
    }

    // 查询能力/状态时使用的 inBuffer 标记值
    protected override uint GetInBufferValue() => 0x2;

    // 状态 -> 驱动指令：0x9 关闭，0x8 开启
    protected override Task<uint[]> ToInternalAsync(OneLevelWhiteKeyboardBacklightState state)
    {
        var result = state switch
        {
            OneLevelWhiteKeyboardBacklightState.Off => new uint[] { 0x9 },
            OneLevelWhiteKeyboardBacklightState.On => [0x8],
            _ => throw new InvalidOperationException("Invalid state"),
        };
        return Task.FromResult(result);
    }

    // 返回值 bit5 表示背光当前是否开启
    protected override Task<OneLevelWhiteKeyboardBacklightState> FromInternalAsync(uint state)
    {
        var isOn = ((int)state & 32) == 32;
        var result = isOn ? OneLevelWhiteKeyboardBacklightState.On : OneLevelWhiteKeyboardBacklightState.Off;
        return Task.FromResult(result);
    }
}

using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// 电池夜间充电特性。开启后电池在夜间（通常 23:00-07:00）不会充到满电，以延长电池寿命。
/// 通过能量管理驱动 IOCTL_ENERGY_BATTERY_NIGHT_CHARGE 控制。
/// </summary>
public class BatteryNightChargeFeature() : AbstractDriverFeature<BatteryNightChargeState>(Drivers.GetEnergy, Drivers.IOCTL_ENERGY_BATTERY_NIGHT_CHARGE)
{
    // 查询当前状态时使用的 inBuffer 标记值
    protected override uint GetInBufferValue() => 0x11;

    // 状态 -> 驱动指令。开启时最高位（0x80000000）作为"开启"标志
    protected override Task<uint[]> ToInternalAsync(BatteryNightChargeState state)
    {
        uint[] result = state switch
        {
            BatteryNightChargeState.On => [0x80000012u],
            BatteryNightChargeState.Off => [0x12u],
            _ => throw new InvalidOperationException("Invalid state")
        };
        return Task.FromResult(result);
    }

    // bit0 为 1 表示状态有效，bit4 表示是否开启夜间充电
    protected override Task<BatteryNightChargeState> FromInternalAsync(uint state)
    {
        if (state.GetNthBit(0))
            return Task.FromResult(state.GetNthBit(4) ? BatteryNightChargeState.On : BatteryNightChargeState.Off);

        throw new InvalidOperationException($"Unknown battery night charge state: {state} [bits={Convert.ToString(state, 2)}]");
    }
}

using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// USB 持续供电（Always-On USB）特性。
/// 通过能量管理驱动的 IOCTL_ENERGY_SETTINGS 控制关机/睡眠时 USB 端口是否继续供电。
/// </summary>
public class AlwaysOnUSBFeature() : AbstractDriverFeature<AlwaysOnUSBState>(Drivers.GetEnergy, Drivers.IOCTL_ENERGY_SETTINGS)
{
    // 查询当前状态时使用的 inBuffer 标记值
    protected override uint GetInBufferValue() => 0x2;

    // 状态 -> 需要发送的驱动指令序列（每条指令为 4 字节 inBuffer）
    protected override Task<uint[]> ToInternalAsync(AlwaysOnUSBState state)
    {
        var result = state switch
        {
            AlwaysOnUSBState.Off => new uint[] { 0xB, 0x12 },
            AlwaysOnUSBState.OnWhenSleeping => [0xA, 0x12],
            AlwaysOnUSBState.OnAlways => [0xA, 0x13],
            _ => throw new InvalidOperationException("Invalid state"),
        };
        return Task.FromResult(result);
    }

    // 驱动返回值需按字节序反转后才能按位解析（驱动以大端顺序返回）
    protected override Task<AlwaysOnUSBState> FromInternalAsync(uint state)
    {
        state = state.ReverseEndianness();

        if (state.GetNthBit(31)) // is on?
        {
            // bit23 为 1 表示"始终开启"，否则为"仅睡眠时开启"
            if (state.GetNthBit(23))
                return Task.FromResult(AlwaysOnUSBState.OnAlways);

            return Task.FromResult(AlwaysOnUSBState.OnWhenSleeping);
        }

        return Task.FromResult(AlwaysOnUSBState.Off);
    }
}

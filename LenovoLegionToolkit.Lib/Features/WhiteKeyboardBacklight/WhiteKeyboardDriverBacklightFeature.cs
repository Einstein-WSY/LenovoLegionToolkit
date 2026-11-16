using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System;

namespace LenovoLegionToolkit.Lib.Features.WhiteKeyboardBacklight;

/// <summary>
/// 白色键盘背光特性 —— 基于能量管理驱动 IOCTL_ENERGY_KEYBOARD 的实现。
/// 支持关 / 低亮 / 高亮三档。
/// </summary>
public class WhiteKeyboardDriverBacklightFeature()
    : AbstractDriverFeature<WhiteKeyboardBacklightState>(Drivers.GetEnergy, Drivers.IOCTL_ENERGY_KEYBOARD)
{
    /// <inheritdoc/>
    public override async Task<bool> IsSupportedAsync()
    {
        try
        {
            // 发送 0x1 能力查询指令，右移 1 位后等于 0x2 说明支持白色键盘背光
            var outBuffer = await SendCodeAsync(DriverHandle(), ControlCode, 0x1).ConfigureAwait(false);
            outBuffer >>= 1;
            return outBuffer == 0x2;
        }
        catch
        {
            return false;
        }
    }

    // 查询当前状态时使用的 inBuffer 标记值
    protected override uint GetInBufferValue() => 0x22;

    // 状态 -> 驱动指令：高 4 位表示亮度等级（0/1/2），低 16 位 0x23 为键盘背光命令字
    protected override Task<uint[]> ToInternalAsync(WhiteKeyboardBacklightState state)
    {
        var result = state switch
        {
            WhiteKeyboardBacklightState.Off => new uint[] { 0x00023 },
            WhiteKeyboardBacklightState.Low => [0x10023],
            WhiteKeyboardBacklightState.High => [0x20023],
            _ => throw new InvalidOperationException("Invalid state"),
        };
        return Task.FromResult(result);
    }

    // 驱动返回的亮度编码：0x1=关、0x3=低亮、0x5=高亮
    protected override Task<WhiteKeyboardBacklightState> FromInternalAsync(uint state)
    {
        var result = state switch
        {
            0x1 => WhiteKeyboardBacklightState.Off,
            0x3 => WhiteKeyboardBacklightState.Low,
            0x5 => WhiteKeyboardBacklightState.High,
            _ => throw new InvalidOperationException("Invalid state"),
        };
        return Task.FromResult(result);
    }
}

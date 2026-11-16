using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// Fn 锁特性。控制 F1-F12 按键默认执行功能键（媒体键）还是标准 F 键。
/// 通过能量管理驱动 IOCTL_ENERGY_SETTINGS 控制。
/// </summary>
public class FnLockFeature() : AbstractDriverFeature<FnLockState>(Drivers.GetEnergy, Drivers.IOCTL_ENERGY_SETTINGS)
{
    // 查询当前状态时使用的 inBuffer 标记值（与 AlwaysOnUSB 共用同一组设置接口）
    protected override uint GetInBufferValue() => 0x2;

    // 状态 -> 驱动指令：0xE 表示开启 Fn 锁，0xF 表示关闭
    protected override Task<uint[]> ToInternalAsync(FnLockState state)
    {
        var lockOn = state switch
        {
            FnLockState.On => true,
            FnLockState.Off => false,
            _ => throw new InvalidOperationException("Invalid state"),
        };

        var value = lockOn ? new uint[] { 0xE } : [0xF];
        return Task.FromResult(value);
    }

    // 返回值 bit10 表示 Fn 锁是否开启
    protected override Task<FnLockState> FromInternalAsync(uint state)
    {
        var value = state.GetNthBit(10) ? FnLockState.On : FnLockState.Off;
        return Task.FromResult(value);
    }
}

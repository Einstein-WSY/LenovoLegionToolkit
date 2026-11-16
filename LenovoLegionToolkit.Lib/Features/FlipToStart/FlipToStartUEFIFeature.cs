using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;

namespace LenovoLegionToolkit.Lib.Features.FlipToStart;

/// <summary>
/// 开盖即开机（Flip To Start）特性 —— 基于 UEFI 固件变量的实现。
/// 读写 GUID <c>{D743491E-F484-4952-A87D-8D5DD189B70C}</c> 下名为 <c>FBSWIF</c> 的固件变量，
/// 第一个字节表示是否开启，其余三个字节为保留字段。
/// </summary>
public class FlipToStartUEFIFeature() : AbstractUEFIFeature<FlipToStartState>(
    "{D743491E-F484-4952-A87D-8D5DD189B70C}",
    "FBSWIF",
    PInvokeExtensions.VARIABLE_ATTRIBUTE_NON_VOLATILE |
    PInvokeExtensions.VARIABLE_ATTRIBUTE_BOOTSERVICE_ACCESS |
    PInvokeExtensions.VARIABLE_ATTRIBUTE_RUNTIME_ACCESS)
{
    // 固件变量结构体：Pack=1 保证与固件的紧凑字节布局一致
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct FlipToBootStruct
    {
        // 0 表示关闭，非 0 表示开启
        [MarshalAs(UnmanagedType.U1)]
        public byte FlipToBootEn;
        [MarshalAs(UnmanagedType.U1)]
        public byte Reserved1;
        [MarshalAs(UnmanagedType.U1)]
        public byte Reserved2;
        [MarshalAs(UnmanagedType.U1)]
        public byte Reserved3;
    }

    // ReSharper disable once StringLiteralTypo

    /// <inheritdoc/>
    public override async Task<FlipToStartState> GetStateAsync()
    {
        // 读取固件结构体，首个字节非 0 即视为开启
        var result = await ReadFromUefiAsync<FlipToBootStruct>().ConfigureAwait(false);
        return result.FlipToBootEn == 0 ? FlipToStartState.Off : FlipToStartState.On;
    }

    /// <inheritdoc/>
    public override async Task SetStateAsync(FlipToStartState state)
    {
        // 保留字段必须清零，避免写入固件时产生意外值
        var structure = new FlipToBootStruct
        {
            FlipToBootEn = state == FlipToStartState.On ? (byte)1 : (byte)0,
            Reserved1 = 0,
            Reserved2 = 0,
            Reserved3 = 0
        };
        await WriteToUefiAsync(structure).ConfigureAwait(false);
    }
}

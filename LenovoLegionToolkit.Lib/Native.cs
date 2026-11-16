using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace LenovoLegionToolkit.Lib;

// ReSharper disable InconsistentNaming
// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable
// ReSharper disable CollectionNeverQueried.Local

#region Battery

// 电池固件返回的原始结构（联想 WMI/EC 布局）。
// 温度与日期为紧凑编码：温度通常需除以 10 得到摄氏度，日期为 BIOS 打包格式。
[StructLayout(LayoutKind.Sequential)]
internal readonly struct LENOVO_BATTERY_INFORMATION
{
    // 前 13 字节保留（未知数据），必须保留占位以维持后续字段的内存偏移
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 13)]
    private readonly byte[] Bytes1;
    public readonly ushort Temperature; // 电池温度（原始编码值）
    public readonly ushort ManufactureDate; // 生产日期（BIOS 打包格式）
    public readonly ushort FirstUseDate; // 首次使用日期（BIOS 打包格式）
    // 尾部 64 字节保留，保证结构总大小与固件缓冲区一致
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    private readonly byte[] Bytes2;
}

#endregion

#region RGB Keyboard

// 四分区 RGB 键盘状态结构，Pack=1 保证无填充字节，与 HID 报文严格对齐。
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct LENOVO_RGB_KEYBOARD_STATE
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public byte[] Header; // 2 字节报文头
    public byte Effect; // 灯效类型
    public byte Speed; // 灯效速度
    public byte Brightness; // 亮度
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public byte[] Zone1Rgb; // 分区 1 的 R/G/B
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public byte[] Zone2Rgb; // 分区 2 的 R/G/B
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public byte[] Zone3Rgb; // 分区 3 的 R/G/B
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public byte[] Zone4Rgb; // 分区 4 的 R/G/B
    public byte Padding; // 对齐填充
    public byte WaveLTR; // 从左到右波纹参数
    public byte WaveRTL; // 从右到左波纹参数
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 13)]
    public byte[] Unused; // 保留字节，凑满固定报文长度
}

#endregion

#region Spectrum Keyboard

// Spectrum 键盘 HID 操作码。数值由联想固件定义，不可修改。
internal enum LENOVO_SPECTRUM_OPERATION_TYPE : byte
{
    Compatibility = 0xD1, // 查询是否兼容 Spectrum 协议
    KeyCount = 0xC4, // 查询按键数量/分页信息
    KeyPage = 0xC5, // 按页查询按键编码
    ProfileChange = 0xC8, // 切换灯效配置
    ProfileDefault = 0xC9, // 恢复默认配置
    Profile = 0xCA, // 查询当前配置
    EffectChange = 0xCB, // 修改灯效
    Effect = 0xCC, // 查询灯效
    GetBrightness = 0xCD, // 查询亮度
    Brightness = 0xCE, // 设置亮度
    AuroraStartStop = 0xD0, // 启动/停止逐键极光模式
    AuroraSendBitmap = 0xA1, // 发送逐键位图
    GetLogoStatus = 0xA5, // 查询面板 Logo 灯状态
    LogoStatus = 0xA6 // 设置面板 Logo 灯状态
}

// Spectrum 灯效类型编号（固件定义）
internal enum LENOVO_SPECTRUM_EFFECT_TYPE : byte
{
    ScrewRainbow = 1,
    RainbowWave = 2,
    ColorChange = 3,
    ColorPulse = 4,
    ColorWave = 5,
    Smooth = 6,
    Rain = 7,
    Ripple = 8,
    AudioBounceLighting = 9,
    AudioRippleLighting = 10,
    Always = 11,
    TypeLighting = 12,
    LegionAuraSync = 13,
}

// 颜色模式：随机单色 / 使用颜色列表
internal enum LENOVO_SPECTRUM_COLOR_MODE : byte
{
    None = 0,
    RandomColor = 1,
    ColorList = 2
}

// 灯效速度档位（1 最慢，3 最快）
internal enum LENOVO_SPECTRUM_SPEED : byte
{
    None = 0,
    Speed1 = 1,
    Speed2 = 2,
    Speed3 = 3
}

// 旋转类灯效的旋转方向
internal enum LENOVO_SPECTRUM_CLOCKWISE_DIRECTION : byte
{
    None = 0,
    Clockwise = 1,
    CounterClockwise = 2
}

// 流动类灯效的方向
internal enum LENOVO_SPECTRUM_DIRECTION : byte
{
    None = 0,
    BottomToTop = 1,
    TopToBottom = 2,
    RightToLeft = 3,
    LeftToRight = 4
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct LENOVO_SPECTRUM_COLOR(byte r, byte g, byte b)
{
    public readonly byte R = r;
    public readonly byte G = g;
    public readonly byte B = b;
}

// 单个按键的灯色状态
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct LENOVO_SPECTRUM_KEY_STATE
{
    public readonly ushort KeyCode; // 按键编码
    public LENOVO_SPECTRUM_COLOR Color;
}

// 按键分页项：页内索引 + 按键编码
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct LENOVO_SPECTRUM_KEY_PAGE_ITEM
{
    private readonly byte Index;
    public readonly ushort KeyCode;
}

// 通用报文头：固定 4 字节（0x07、操作码、长度、0x03）
[StructLayout(LayoutKind.Sequential)]
internal readonly struct LENOVO_SPECTRUM_HEADER(LENOVO_SPECTRUM_OPERATION_TYPE type, int size)
{
    public readonly byte Head = 7;
    public readonly LENOVO_SPECTRUM_OPERATION_TYPE Type = type;
    public readonly byte Size = (byte)(size % 256); // 长度仅保留低 8 位
    public readonly byte Tail = 3;
}

// 灯效头：固件要求字段间交替填充固定序号字节（0x1..0x6），这些"Unknown"实为协议校验标记
[StructLayout(LayoutKind.Sequential)]
internal readonly struct LENOVO_SPECTRUM_EFFECT_HEADER(
    LENOVO_SPECTRUM_EFFECT_TYPE effectType,
    LENOVO_SPECTRUM_SPEED speed,
    LENOVO_SPECTRUM_DIRECTION direction,
    LENOVO_SPECTRUM_CLOCKWISE_DIRECTION clockwiseDirection,
    LENOVO_SPECTRUM_COLOR_MODE colorMode)
{
    public readonly byte Head = 0x6;
    public readonly byte Unknown1 = 0x1; // 固定 0x1
    public readonly LENOVO_SPECTRUM_EFFECT_TYPE EffectType = effectType;
    public readonly byte Unknown2 = 0x2; // 固定 0x2
    public readonly LENOVO_SPECTRUM_SPEED Speed = speed;
    public readonly byte Unknown3 = 0x3; // 固定 0x3
    public readonly LENOVO_SPECTRUM_CLOCKWISE_DIRECTION ClockwiseDirection = clockwiseDirection;
    public readonly byte Unknown5 = 0x4; // 固定 0x4（固件中的 0x5 号字段）
    public readonly LENOVO_SPECTRUM_DIRECTION Direction = direction;
    public readonly byte Unknown6 = 0x5; // 固定 0x5（固件中的 0x6 号字段）
    public readonly LENOVO_SPECTRUM_COLOR_MODE ColorMode = colorMode;
    public readonly byte Unknown7 = 0x6; // 固定 0x6
    public readonly byte Tail = 0x0;
}

// 单个灯效的完整描述：头 + 颜色列表 + 按键列表
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct LENOVO_SPECTRUM_EFFECT(
    LENOVO_SPECTRUM_EFFECT_HEADER effectHeader,
    int effectNo,
    LENOVO_SPECTRUM_COLOR[] colors,
    ushort[] keyCodes)
{
    public readonly byte EffectNo = (byte)effectNo;
    public readonly LENOVO_SPECTRUM_EFFECT_HEADER EffectHeader = effectHeader;
    public readonly byte NumberOfColors = (byte)colors.Length;
    public readonly LENOVO_SPECTRUM_COLOR[] Colors = colors;
    public readonly byte NumberOfKeys = (byte)keyCodes.Length;
    public readonly ushort[] KeyCodes = keyCodes;
}

// 极光（逐键）位图中的一个键位及其颜色
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal readonly struct LENOVO_SPECTRUM_AURORA_ITEM(ushort keyCode, LENOVO_SPECTRUM_COLOR color)
{
    public readonly ushort KeyCode = keyCode;
    public readonly LENOVO_SPECTRUM_COLOR Color = color;
}

// 所有 HID 报文固定为 960 字节（固件缓冲区大小），请求结构用 Size 强行占位

[StructLayout(LayoutKind.Sequential, Size = 960)]
internal readonly struct LENOVO_SPECTRUM_GET_COMPATIBILITY_REQUEST()
{
    private readonly LENOVO_SPECTRUM_HEADER Header = new(LENOVO_SPECTRUM_OPERATION_TYPE.Compatibility, 0xC0);
}

[StructLayout(LayoutKind.Sequential, Size = 960)]
internal readonly struct LENOVO_SPECTRUM_GET_COMPATIBILITY_RESPONSE
{
    private readonly byte ReportId;
    private readonly LENOVO_SPECTRUM_OPERATION_TYPE Type;
    private readonly byte Length;
    private readonly byte Unknown1;
    private readonly byte Compatibility;

    // 固件约定：Compatibility 为 0 表示支持 Spectrum 协议
    public bool IsCompatible => Compatibility == 0;
}

[StructLayout(LayoutKind.Sequential, Size = 960)]
internal readonly struct LENOVO_SPECTRUM_GET_KEY_COUNT_REQUEST()
{
    private readonly LENOVO_SPECTRUM_HEADER Header = new(LENOVO_SPECTRUM_OPERATION_TYPE.KeyCount, 0xC0);
    private readonly byte Parameter = 7;
}

[StructLayout(LayoutKind.Sequential, Size = 960)]
internal readonly struct LENOVO_SPECTRUM_GET_KEY_COUNT_RESPONSE
{
    private readonly byte ReportId;
    private readonly LENOVO_SPECTRUM_OPERATION_TYPE Type;
    private readonly byte Length;
    private readonly byte Unknown1;
    private readonly byte Parameter;
    public readonly byte Indexes; // 按键分页数量
    public readonly byte KeysPerIndex; // 每页按键数
}

[StructLayout(LayoutKind.Sequential, Size = 960)]
internal readonly struct LENOVO_SPECTRUM_GET_KEY_PAGE_REQUEST(byte index, bool secondary = false)
{
    private readonly LENOVO_SPECTRUM_HEADER Header = new(LENOVO_SPECTRUM_OPERATION_TYPE.KeyPage, 0xC0);
    // 参数 7/8 区分主键盘页与副键盘页
    private readonly byte Parameter = secondary ? (byte)8 : (byte)7;
    private readonly byte Index = index;
}

[StructLayout(LayoutKind.Sequential, Size = 960)]
internal readonly struct LENOVO_SPECTRUM_GET_KEY_PAGE_RESPONSE
{
    private readonly byte ReportId;
    private readonly LENOVO_SPECTRUM_OPERATION_TYPE Type;
    private readonly byte Length;
    private readonly byte Unknown1;
    private readonly byte Parameter;
    private readonly byte Index;
    // 每页固定 32 个按键项
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
    public readonly LENOVO_SPECTRUM_KEY_PAGE_ITEM[] Items;
}

[StructLayout(LayoutKind.Sequential, Size = 960)]
internal readonly struct LENOVO_SPECTRUM_GET_BRIGHTNESS_REQUEST()
{
    private readonly LENOVO_SPECTRUM_HEADER Header = new(LENOVO_SPECTRUM_OPERATION_TYPE.GetBrightness, 0xC0);
}

[StructLayout(LayoutKind.Sequential, Size = 960)]
internal readonly struct LENOVO_SPECTRUM_GET_BRIGHTNESS_RESPONSE
{
    private readonly byte ReportId;
    private readonly LENOVO_SPECTRUM_OPERATION_TYPE Type;
    private readonly byte Length;
    private readonly byte Unknown1;
    public readonly byte Brightness;
}

[StructLayout(LayoutKind.Sequential, Size = 960)]
internal readonly struct LENOVO_SPECTRUM_SET_BRIGHTNESS_REQUEST(byte brightness)
{
    private readonly LENOVO_SPECTRUM_HEADER Header = new(LENOVO_SPECTRUM_OPERATION_TYPE.Brightness, 0xC0);
    private readonly byte Brightness = brightness;
}

[StructLayout(LayoutKind.Sequential, Size = 960)]
internal readonly struct LENOVO_SPECTRUM_GET_LOGO_STATUS()
{
    private readonly LENOVO_SPECTRUM_HEADER Header = new(LENOVO_SPECTRUM_OPERATION_TYPE.GetLogoStatus, 0xC0);
}

[StructLayout(LayoutKind.Sequential, Size = 960)]
internal readonly struct LENOVO_SPECTRUM_GET_LOGO_STATUS_RESPONSE
{
    private readonly byte ReportId;
    private readonly LENOVO_SPECTRUM_OPERATION_TYPE Type;
    private readonly byte Length;
    private readonly byte Unknown1;
    private readonly byte Status;

    // 状态值为 1 表示 Logo 灯点亮
    public bool IsOn => Status == 1;
}

[StructLayout(LayoutKind.Sequential, Size = 960)]
internal readonly struct LENOVO_SPECTRUM_SET_LOGO_STATUS_REQUEST(bool isOn)
{
    private readonly LENOVO_SPECTRUM_HEADER Header = new(LENOVO_SPECTRUM_OPERATION_TYPE.LogoStatus, 0xC0);
    private readonly byte Status = (byte)(isOn ? 1 : 0);
}

[StructLayout(LayoutKind.Sequential, Size = 960)]
internal readonly struct LENOVO_SPECTRUM_GET_PROFILE_REQUEST()
{
    private readonly LENOVO_SPECTRUM_HEADER Header = new(LENOVO_SPECTRUM_OPERATION_TYPE.Profile, 0xC0);
}

[StructLayout(LayoutKind.Sequential, Size = 960)]
internal readonly struct LENOVO_SPECTRUM_GET_PROFILE_RESPONSE
{
    private readonly byte ReportId;
    private readonly LENOVO_SPECTRUM_OPERATION_TYPE Type;
    private readonly byte Length;
    private readonly byte Unknown1;
    public readonly byte Profile;
}

[StructLayout(LayoutKind.Sequential, Size = 960)]
internal readonly struct LENOVO_SPECTRUM_SET_PROFILE_REQUEST(byte profile)
{
    private readonly LENOVO_SPECTRUM_HEADER Header = new(LENOVO_SPECTRUM_OPERATION_TYPE.ProfileChange, 0xC0);
    private readonly byte Profile = profile;
}

[StructLayout(LayoutKind.Sequential, Size = 960)]
internal readonly struct LENOVO_SPECTRUM_SET_PROFILE_DEFAULT_REQUEST(byte profile)
{
    private readonly LENOVO_SPECTRUM_HEADER Header = new(LENOVO_SPECTRUM_OPERATION_TYPE.ProfileDefault, 0xC0);
    private readonly byte Profile = profile;
}

[StructLayout(LayoutKind.Sequential, Size = 960)]
internal readonly struct LENOVO_SPECTRUM_GET_EFFECT_REQUEST(byte profile)
{
    private readonly LENOVO_SPECTRUM_HEADER Header = new(LENOVO_SPECTRUM_OPERATION_TYPE.Effect, 0xC0);
    private readonly byte Profile = profile;
}

/// <summary>
/// 一个配置（Profile）下的全部灯效描述，负责与固件字节流互转。
/// </summary>
internal readonly struct LENOVO_SPECTRUM_EFFECT_DESCRIPTION(
    LENOVO_SPECTRUM_HEADER header,
    byte profile,
    LENOVO_SPECTRUM_EFFECT[] effects)
{
    public readonly byte Profile = profile;
    private readonly byte Unknown1 = 1;
    private readonly byte Unknown2 = 1;
    public readonly LENOVO_SPECTRUM_EFFECT[] Effects = effects;

    /// <summary>
    /// 从固件返回的字节流解析出灯效描述。
    /// 固件按"效果号递增"排列，遇到编号回退即视为列表结束。
    /// </summary>
    /// <param name="bytes">固件原始报文（首字节为 ReportId）。</param>
    /// <returns>解析后的灯效描述实例。</returns>
    public static LENOVO_SPECTRUM_EFFECT_DESCRIPTION FromBytes(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var br = new BinaryReader(ms);

        // 跳过 ReportId，读取报文类型与长度，重建头部
        _ = br.ReadByte();
        var type = (LENOVO_SPECTRUM_OPERATION_TYPE)br.ReadByte();
        var size = br.ReadByte();
        _ = br.ReadByte();

        var header = new LENOVO_SPECTRUM_HEADER(type, size);

        var profile = br.ReadByte();
        _ = br.ReadByte(); // 保留
        _ = br.ReadByte(); // 保留

        var effects = new List<LENOVO_SPECTRUM_EFFECT>();

        var lastEffectNo = 1;
        while (true)
        {
            var effectNo = br.ReadByte();

            // 效果号不再递增说明已读到结尾（固件不返回显式结束标记）
            if (effectNo < lastEffectNo)
                break;

            lastEffectNo = effectNo;

            // 跳过效果号后的两个保留字节，再读取灯效参数
            _ = br.ReadByte();
            _ = br.ReadByte();
            var effectType = (LENOVO_SPECTRUM_EFFECT_TYPE)br.ReadByte();
            _ = br.ReadByte();
            var speed = (LENOVO_SPECTRUM_SPEED)br.ReadByte();
            _ = br.ReadByte();
            var clockwiseDirection = (LENOVO_SPECTRUM_CLOCKWISE_DIRECTION)br.ReadByte();
            _ = br.ReadByte();
            var direction = (LENOVO_SPECTRUM_DIRECTION)br.ReadByte();
            _ = br.ReadByte();
            var colorMode = (LENOVO_SPECTRUM_COLOR_MODE)br.ReadByte();
            _ = br.ReadByte();
            _ = br.ReadByte();

            var effectHeader = new LENOVO_SPECTRUM_EFFECT_HEADER(effectType, speed, direction, clockwiseDirection, colorMode);
            var colors = new List<LENOVO_SPECTRUM_COLOR>();
            var keyCodes = new List<ushort>();

            // 颜色列表：每个颜色 3 字节（R/G/B）
            var noOfColors = br.ReadByte();
            for (var i = 0; i < noOfColors; i++)
            {
                var r = br.ReadByte();
                var g = br.ReadByte();
                var b = br.ReadByte();
                colors.Add(new(r, g, b));
            }

            // 按键列表：每个按键 2 字节（ushort 编码）
            var noOfKeys = br.ReadByte();
            for (var i = 0; i < noOfKeys; i++)
            {
                var keyCode = br.ReadUInt16();
                keyCodes.Add(keyCode);
            }

            effects.Add(new(effectHeader, effectNo, [.. colors], [.. keyCodes]));
        }

        return new(header, profile, [.. effects]);
    }

    /// <summary>
    /// 序列化为 960 字节的固件报文；写入完成后回填实际长度到头部第 3 字节。
    /// </summary>
    /// <returns>可直接写入 HID 的字节数组。</returns>
    public byte[] ToBytes()
    {
        using var ms = new MemoryStream(new byte[960]); // 报文固定 960 字节
        using var bf = new BinaryWriter(ms);

        bf.Write(header.Head);
        bf.Write((byte)header.Type);
        bf.Write(header.Size);
        bf.Write(header.Tail);

        bf.Write(Profile);
        bf.Write(Unknown1);
        bf.Write(Unknown2);

        foreach (var effect in Effects)
        {
            bf.Write(effect.EffectNo);

            bf.Write(effect.EffectHeader.Head);
            bf.Write(effect.EffectHeader.Unknown1);
            bf.Write((byte)effect.EffectHeader.EffectType);
            bf.Write(effect.EffectHeader.Unknown2);
            bf.Write((byte)effect.EffectHeader.Speed);
            bf.Write(effect.EffectHeader.Unknown3);
            bf.Write((byte)effect.EffectHeader.ClockwiseDirection);
            bf.Write(effect.EffectHeader.Unknown5);
            bf.Write((byte)effect.EffectHeader.Direction);
            bf.Write(effect.EffectHeader.Unknown6);
            bf.Write((byte)effect.EffectHeader.ColorMode);
            bf.Write(effect.EffectHeader.Unknown7);
            bf.Write(effect.EffectHeader.Tail);

            bf.Write(effect.NumberOfColors);
            foreach (var color in effect.Colors)
            {
                bf.Write(color.R);
                bf.Write(color.G);
                bf.Write(color.B);
            }

            bf.Write(effect.NumberOfKeys);
            foreach (var keyCode in effect.KeyCodes)
            {
                bf.Write(keyCode);
            }
        }

        // 回填实际写入长度：偏移 2 处的 Size 字段需要为最终报文长度（固件按 255 取模）
        var position = ms.Position;
        bf.Seek(2, SeekOrigin.Begin);
        bf.Write((byte)(position % 255));

        return ms.ToArray();
    }
}

[StructLayout(LayoutKind.Sequential, Size = 960)]
internal readonly struct LENOVO_SPECTRUM_AURORA_START_STOP_REQUEST(bool start, byte profile)
{
    private readonly LENOVO_SPECTRUM_HEADER Header = new(LENOVO_SPECTRUM_OPERATION_TYPE.AuroraStartStop, 0xC0);
    // 1=启动极光模式，2=停止
    private readonly byte StartStop = start ? (byte)1 : (byte)2;
    private readonly byte Profile = profile;
}

/// <summary>
/// 逐键极光位图请求：把每个按键的颜色一次性发送给键盘。
/// </summary>
internal readonly struct LENOVO_SPECTRUM_AURORA_SEND_BITMAP_REQUEST(LENOVO_SPECTRUM_AURORA_ITEM[] items)
{
    private readonly LENOVO_SPECTRUM_HEADER Header = new(LENOVO_SPECTRUM_OPERATION_TYPE.AuroraSendBitmap, 0xC0);

    /// <summary>
    /// 序列化为 960 字节报文，依次写入每个键位（2 字节键码 + 3 字节颜色）。
    /// </summary>
    public byte[] ToBytes()
    {
        using var ms = new MemoryStream(new byte[960]);
        using var bf = new BinaryWriter(ms);

        bf.Write(Header.Head);
        bf.Write((byte)Header.Type);
        bf.Write(Header.Size);
        bf.Write(Header.Tail);

        foreach (var item in items)
        {
            bf.Write(item.KeyCode);
            bf.Write(item.Color.R);
            bf.Write(item.Color.G);
            bf.Write(item.Color.B);
        }

        return ms.ToArray();
    }
}

[StructLayout(LayoutKind.Sequential, Size = 960)]
internal readonly struct LENOVO_SPECTRUM_STATE_RESPONSE
{
    private readonly byte ReportId;
    private readonly LENOVO_SPECTRUM_OPERATION_TYPE Type;
    private readonly byte Length;
    private readonly byte Unknown1;
    // 最多 190 个按键状态（960 - 4 字节头 ≈ 190 × 5 字节）
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 190)]
    public readonly LENOVO_SPECTRUM_KEY_STATE[] Data;
}

#endregion

#region Boot Logo

// 开机 Logo 支持的图片格式位标志（对应固件能力位）
[Flags]
public enum BootLogoFormat : byte
{
    Jpeg = 0x1, // JPEG
    Bmp = 0x10, // BMP
    Png = 0x20, // PNG
}

// 开机 Logo 能力信息：是否已启用、支持的尺寸与格式
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BootLogoInfo
{
    public byte Enabled; // 非 0 表示已启用自定义 Logo
    public readonly int SupportedWidth; // 支持的最大宽度
    public readonly int SupportedHeight; // 支持的最大高度
    public readonly BootLogoFormat SupportedFormat; // 支持的图片格式
}

// 开机 Logo 校验信息：固件在固定偏移处读写 CRC32，其余字段为占位保留
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct BootLogoChecksum
{
    private readonly int Unused1;
    public uint Crc; // Logo 图片的 CRC32 校验值
    private readonly int Unused2;
    private readonly int Unused3;
    private readonly int Unused4;
    private readonly int Unused5;
    private readonly int Unused6;
    private readonly int Unused7;
    private readonly int Unused8;
    private readonly int Unused9;
}

#endregion

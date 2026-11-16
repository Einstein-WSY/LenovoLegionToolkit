using System.Runtime.InteropServices;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System;
using Windows.Win32;

// ReSharper disable all

// Spectrum 测试工具：绕过上层封装，直接向键盘的 HID 设备收发 Spectrum 灯效报文，
// 用于探测厂商私有协议、确认机型支持的灯效布局以及定位按键键码。仅供开发调试使用。
Console.WriteLine(@"How to use:
  1. Make sure Vantage and LLT is closed.
  2. Set the keyboard brightness to maximum.

When ready, press any key to continue...
");
Console.ReadKey();

// 通过 HID 设备接口查找支持 Spectrum 灯效的键盘，未找到则说明本机不支持
var device = Devices.GetSpectrumRGBKeyboard();

Console.WriteLine("Finding Spectrum keyboard...");

if (device is null)
{
    Console.WriteLine("Spectrum not supported");
    Console.ReadKey();
    return;
}


Console.WriteLine("Spectrum keyboard found");
Console.WriteLine();

// 以下逐个发送探测指令（0xD1、0xC6、0x04、0xC7 等）并读取响应，
// 通过分析返回字节判断固件能力，例如 0xC7 的响应可以推断灯效布局。
Console.WriteLine("Reading response for 0xD1...");
SetFeature(device, new LENOVO_SPECTRUM_GENERIC_REQUEST(LENOVO_SPECTRUM_OPERATION_TYPE.UnknownD1, 0, 0));
GetFeature(device, out LENOVO_SPECTRUM_GENERIC_RESPONSE resD1);
Print(resD1.Bytes);
Console.WriteLine();

Console.WriteLine("Reading response for 0xC6...");
SetFeature(device, new LENOVO_SPECTRUM_GENERIC_REQUEST(LENOVO_SPECTRUM_OPERATION_TYPE.UnknownC6, 0, 0));
GetFeature(device, out LENOVO_SPECTRUM_GENERIC_RESPONSE resC6);
Print(resC6.Bytes);
Console.WriteLine();

Console.WriteLine("Reading response for 0x04...");
SetFeature(device, new LENOVO_SPECTRUM_GENERIC_REQUEST(LENOVO_SPECTRUM_OPERATION_TYPE.Unknown04, 0, 0));
GetFeature(device, out LENOVO_SPECTRUM_GENERIC_RESPONSE res04);
Print(res04.Bytes);
Console.WriteLine();

Console.WriteLine("Reading response for 0xC7...");
SetFeature(device, new LENOVO_SPECTRUM_GENERIC_REQUEST(LENOVO_SPECTRUM_OPERATION_TYPE.UnknownC7, 0, 0));
GetFeature(device, out LENOVO_SPECTRUM_GENERIC_RESPONSE resC7);
Print(resC7.Bytes);
Console.WriteLine();

Console.WriteLine("Reading response for 0xC4 7...");
SetFeature(device, new LENOVO_SPECTRUM_GENERIC_REQUEST(LENOVO_SPECTRUM_OPERATION_TYPE.UnknownC4, 7, 0));
GetFeature(device, out LENOVO_SPECTRUM_GENERIC_RESPONSE resC47);
Print(resC47.Bytes);
Console.WriteLine();

Console.WriteLine("Reading response for 0xC4 8...");
SetFeature(device, new LENOVO_SPECTRUM_GENERIC_REQUEST(LENOVO_SPECTRUM_OPERATION_TYPE.UnknownC4, 8, 0));
GetFeature(device, out LENOVO_SPECTRUM_GENERIC_RESPONSE resC48);
Print(resC48.Bytes);
Console.WriteLine();

// 0xC5 指令带两个参数字节，第二个参数遍历 0-9 以探测不同分区/参数的响应差异
for (var i = 0; i < 10; i++)
{
    Console.WriteLine($"Reading response for 0xC5 7 {i}...");
    SetFeature(device, new LENOVO_SPECTRUM_GENERIC_REQUEST(LENOVO_SPECTRUM_OPERATION_TYPE.UnknownC5, 7, (byte)i));
    GetFeature(device, out LENOVO_SPECTRUM_GENERIC_RESPONSE resC57);
    Print(resC57.Bytes);
    Console.WriteLine();
}

Console.WriteLine($"Reading response for 0xC5 8...");
SetFeature(device, new LENOVO_SPECTRUM_GENERIC_REQUEST(LENOVO_SPECTRUM_OPERATION_TYPE.UnknownC5, 8, 0));
GetFeature(device, out LENOVO_SPECTRUM_GENERIC_RESPONSE resC58);
Print(resC58.Bytes);
Console.WriteLine();

Console.WriteLine(resD1.Bytes[4] == 0 ? "Keyboard is RGB." : "Not compatible.");

// 0xC7 响应中第 5、6 字节描述灯区数量与按键数，据此判断键盘属于哪种灯效布局
var spectrumLayout = (resC47.Bytes[6], resC47.Bytes[5]) switch
{
    (22, 9) => "Full",
    (20, 8) => "KeyboardAndFront",
    (20, 7) => "KeyboardOnly",
    _ => "Unknown"
};
Console.WriteLine($"Layout is {spectrumLayout}.");
Console.WriteLine("Reading config complete.");
Console.WriteLine();
Console.ReadKey();

Console.WriteLine(@"*** TEST 1***

This program will now try to control brightness of the keyboard. Keyboard should turn off and then gradually increase brightness and turn off at the end.

When ready, press any key to continue...
");

// 亮度等级 0-9，最后回到 0，验证亮度调节报文是否生效
var brightnessLevels = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 };

foreach (var level in brightnessLevels)
{
    Console.WriteLine($"Setting brightness level to {level}...");
    SetFeature(device, new LENOVO_SPECTRUM_GENERIC_REQUEST(LENOVO_SPECTRUM_OPERATION_TYPE.Brightness, level, 0));
    await Task.Delay(250);
}

Console.WriteLine("Test complete, press any key to continue...");
Console.WriteLine();
Console.ReadKey();

Console.WriteLine(@"***TEST 2***

How to find a keycode for a specific key:
  1. Open Vantage
  2. Select the key that you want to find keycode for
  3. Set the key to plain white (Hex: #FFFFFF, RGB: 255,255,255)
  4. Make sure all other keys don't have any effect set (are off)
  5. Set the keyboard brightness to maximum

When ready, press any key to continue...
");
Console.ReadKey();

Console.WriteLine("Reading white key keycodes...");
Console.WriteLine();

const int Iterations = 5;

// 多次读取键盘全键状态，把亮白色（用户在 Vantage 中设置的目标键）的键码打印出来，
// 重复读取可避免灯光刷新时序导致的漏读
for (var i = 0; i < Iterations; i++)
{
    GetFeature(device, out LENOVO_SPECTRUM_STATE state);

    var keys = state.Data
        .Where(kv => HasColor(kv.Color))
        .Select(kv => kv.Key)
        .Select(k => "0x" + k.ToString("X4"))
        .Split(8);

    await Task.Delay(1000);

    if (keys.SelectMany(k => k).IsEmpty())
    {
        Console.WriteLine($"[{i + 1}/{Iterations}] No keys found");
        continue;
    }

    Console.WriteLine($"[{i + 1}/{Iterations}] Keys with color found:");

    foreach (var line in keys)
        Console.WriteLine("    " + string.Join(", ", line));

}

Console.WriteLine();
Console.WriteLine("Test complete.");
Console.WriteLine("Press any key to exit...");
Console.ReadLine();

#region Methods

// 按报文格式打印：第 3 个字节（bytes[2]）为负载长度，加上 4 字节头部即为总长度，每行输出 16 字节
void Print(byte[] bytes)
{
    var length = bytes[2] + 4;
    foreach (var i in bytes.Take(length).Split(16))
        Console.WriteLine(string.Join(" ", i.Select(i => $"{i:X2}")));
}

// 判断某个键是否被点亮为纯白色，用于在 TEST 2 中识别用户指定的目标键
bool HasColor(LENOVO_SPECTRUM_COLOR rgbColor) => rgbColor.Red == 255 && rgbColor.Green == 255 && rgbColor.Blue == 255;

// 通过 HID 的 SetFeature 发送输出报文：结构体需先封送到非托管内存，byte[] 则直接复制
unsafe void SetFeature<T>(SafeHandle handle, T str) where T : notnull
{
    var ptr = IntPtr.Zero;
    try
    {
        int size;
        if (str is byte[] bytes)
        {
            // 原始字节数组按原样发送，长度即报文长度
            size = bytes.Length;
            ptr = Marshal.AllocHGlobal(size);
            Marshal.Copy(bytes, 0, ptr, size);
        }
        else
        {
            // 结构体按其封送布局写入非托管缓冲区
            size = Marshal.SizeOf<T>();
            ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(str, ptr, false);
        }

        var result = PInvoke.HidD_SetFeature(handle, ptr.ToPointer(), (uint)size);
        if (!result)
            PInvokeExtensions.ThrowIfWin32Error(typeof(T).Name);
    }
    finally
    {
        // 无论成功与否都要释放缓冲区，避免泄漏
        Marshal.FreeHGlobal(ptr);
    }
}

// 通过 HID 的 GetFeature 读取输入报文：首个字节需填入 Report ID（此处固定为 7）
unsafe void GetFeature<T>(SafeHandle handle, out T str) where T : struct
{
    var ptr = IntPtr.Zero;
    try
    {
        var size = Marshal.SizeOf<T>();
        ptr = Marshal.AllocHGlobal(size);
        // 写入 Report ID，HidD_GetFeature 依据该字节返回对应报表
        Marshal.Copy(new byte[] { 7 }, 0, ptr, 1);

        var result = PInvoke.HidD_GetFeature(handle, ptr.ToPointer(), (uint)size);
        if (!result)
            PInvokeExtensions.ThrowIfWin32Error(typeof(T).Name);

        str = Marshal.PtrToStructure<T>(ptr);
    }
    finally
    {
        Marshal.FreeHGlobal(ptr);
    }
}

#endregion

#region Structs

/// <summary>
/// Spectrum 报文头部：固定以 7 开头、以 3 结尾，中间为操作码与负载长度。
/// </summary>
/// <param name="type">操作码，决定报文含义。</param>
/// <param name="size">负载长度，写入时仅取低 8 位。</param>
[StructLayout(LayoutKind.Sequential)]
internal struct LENOVO_SPECTRUM_HEADER(LENOVO_SPECTRUM_OPERATION_TYPE type, int size)
{
    /// <summary>固定起始字节。</summary>
    public byte Head = 7;
    /// <summary>操作码。</summary>
    public LENOVO_SPECTRUM_OPERATION_TYPE Type = type;
    /// <summary>负载长度（0-255 循环）。</summary>
    public byte Size = (byte)(size % 256);
    /// <summary>固定结束字节。</summary>
    public byte Tail = 3;
}

/// <summary>
/// 通用 Spectrum 请求报文：头部 + 两个参数字节，整体固定 960 字节以匹配 HID 报文长度。
/// </summary>
/// <param name="operation">操作码。</param>
/// <param name="value">第一个参数字节。</param>
/// <param name="value2">第二个参数字节。</param>
[StructLayout(LayoutKind.Sequential, Size = 960)]
internal struct LENOVO_SPECTRUM_GENERIC_REQUEST(LENOVO_SPECTRUM_OPERATION_TYPE operation, byte value, byte value2)
{
    /// <summary>报文头部，负载长度固定按 0xC0 填写。</summary>
    public LENOVO_SPECTRUM_HEADER Header = new(operation, 0xC0);
    /// <summary>第一个参数字节。</summary>
    public byte Value = value;
    /// <summary>第二个参数字节。</summary>
    public byte Value2 = value2;
}

/// <summary>
/// 通用 Spectrum 响应报文：原始 960 字节数据，需按操作码自行解析各字段含义。
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 960)]
internal struct LENOVO_SPECTRUM_GENERIC_RESPONSE
{
    /// <summary>响应原始字节，长度固定 960。</summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 960)]
    public byte[] Bytes;
}

/// <summary>
/// Spectrum 操作码。除已确认含义的指令外，其余以探测到的编码命名（UnknownXX）。
/// </summary>
internal enum LENOVO_SPECTRUM_OPERATION_TYPE : byte
{
    /// <summary>设置灯效配置（方式一）。</summary>
    ProfileSet1 = 0xC8,
    /// <summary>读取当前灯效配置。</summary>
    GetProfile = 0xCA,
    /// <summary>切换灯效。</summary>
    EffectChange = 0xCB,
    /// <summary>设置灯效配置（方式二）。</summary>
    ProfileSet2 = 0xCC,
    /// <summary>读取当前亮度。</summary>
    GetBrightness = 0xCD,
    /// <summary>设置亮度。</summary>
    Brightness = 0xCE,
    /// <summary>逐键位图下发（Aurora 灯效）。</summary>
    AuroraSendBitmap = 0xA1,
    /// <summary>读取键盘全键状态。</summary>
    State = 0x03,
    /// <summary>未确认用途（0x04）。</summary>
    Unknown04 = 0x04,
    /// <summary>未确认用途（0xC4），带参可探测分区信息。</summary>
    UnknownC4 = 0xC4,
    /// <summary>未确认用途（0xC5）。</summary>
    UnknownC5 = 0xC5,
    /// <summary>未确认用途（0xC6）。</summary>
    UnknownC6 = 0xC6,
    /// <summary>未确认用途（0xC7），响应可推断灯效布局。</summary>
    UnknownC7 = 0xC7,
    /// <summary>未确认用途（0xD1），响应可判断是否支持 RGB。</summary>
    UnknownD1 = 0xD1,
}

/// <summary>
/// 单个键位或灯区的 RGB 颜色。
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LENOVO_SPECTRUM_COLOR
{
    /// <summary>红色分量。</summary>
    public byte Red;
    /// <summary>绿色分量。</summary>
    public byte Green;
    /// <summary>蓝色分量。</summary>
    public byte Blue;
}

/// <summary>
/// 键位状态：键码与当前颜色。使用 Pack = 1 保证与设备报文的无填充布局一致。
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct LENOVO_SPECTRUM_KEY_STATE
{
    /// <summary>键码。</summary>
    public ushort Key;
    /// <summary>该键当前颜色。</summary>
    public LENOVO_SPECTRUM_COLOR Color;
}

/// <summary>
/// 键盘完整灯效状态报文：头部若干字段 + 最多 191 个键位状态。
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 960)]
internal struct LENOVO_SPECTRUM_STATE
{
    /// <summary>HID 报文 ID。</summary>
    public byte ReportId;
    /// <summary>操作码。</summary>
    public LENOVO_SPECTRUM_OPERATION_TYPE Type;
    /// <summary>含义未确认的保留字节。</summary>
    public byte Unknown2;
    /// <summary>含义未确认的保留字节。</summary>
    public byte Unknown3;
    /// <summary>键位状态数组，固定 191 项。</summary>
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 191)]
    public LENOVO_SPECTRUM_KEY_STATE[] Data;
    /// <summary>含义未确认的保留字节。</summary>
    public byte Unknown4;
}

#endregion

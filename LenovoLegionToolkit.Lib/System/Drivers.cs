using System;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;

namespace LenovoLegionToolkit.Lib.System;

/// <summary>
/// 联想能量驱动（EnergyDrv）访问辅助类。
/// </summary>
public static class Drivers
{
    // 以下 IOCTL 控制码由联想能量驱动定义，高位 0x83 表示 FILE_DEVICE_UNKNOWN，
    // 后接访问权限与功能号，用于通过 DeviceIoControl 与驱动通信
    /// <summary>查询电池信息（如设计容量、循环次数）。</summary>
    public const uint IOCTL_ENERGY_BATTERY_INFORMATION = 0x83102138;
    /// <summary>写入/读取电量与充电相关的综合设置。</summary>
    public const uint IOCTL_ENERGY_SETTINGS = 0x831020E8;
    /// <summary>设置电池充电模式（快充/标准/养护等）。</summary>
    public const uint IOCTL_ENERGY_BATTERY_CHARGE_MODE = 0x831020F8;
    /// <summary>设置夜间充电（夜间静音充电）开关。</summary>
    public const uint IOCTL_ENERGY_BATTERY_NIGHT_CHARGE = 0x83102150;
    /// <summary>键盘背光相关控制。</summary>
    public const uint IOCTL_ENERGY_KEYBOARD = 0x83102144;
    /// <summary>等待按键事件句柄，用于阻塞等待 Fn 组合键。</summary>
    public const uint IOCTL_KEY_WAIT_HANDLE = 0x831020D8;
    /// <summary>读取按键值。</summary>
    public const uint IOCTL_KEY_VALUE = 0x831020CC;

    private static readonly object Lock = new();

    private static SafeFileHandle? _energy;

    /// <summary>
    /// 获取能量驱动设备句柄（进程内单例，惰性创建）。
    /// </summary>
    /// <returns>已打开的 \\.\EnergyDrv 设备句柄。</returns>
    /// <exception cref="InvalidOperationException">设备打开失败（驱动未安装或权限不足）时抛出。</exception>
    public static SafeFileHandle GetEnergy()
    {
        // 无锁快速路径：句柄已创建则直接复用
        if (_energy is not null)
            return _energy;

        lock (Lock)
        {
            // 双重检查，避免并发重复打开设备
            if (_energy is not null)
                return _energy;

            // 以读写方式打开设备符号链接；OPEN_EXISTING 表示必须已存在，不自动创建
            var handle = PInvoke.CreateFile(@"\\.\EnergyDrv",
                (uint)FILE_ACCESS_RIGHTS.FILE_READ_DATA | (uint)FILE_ACCESS_RIGHTS.FILE_WRITE_DATA,
                FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
                null,
                FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL,
                null);

            if (handle.IsInvalid)
                throw new InvalidOperationException("handle is invalid");

            _energy = handle;
        }

        return _energy;
    }
}

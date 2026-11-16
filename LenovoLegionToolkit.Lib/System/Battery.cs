using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.System.Power;

namespace LenovoLegionToolkit.Lib.System;

/// <summary>
/// 电池信息读取辅助类。
/// 基础数据来自 Windows 电池设备（DeviceIoControl + IOCTL_BATTERY_*），
/// 温度、生产日期、首次使用日期等扩展数据来自联想能量驱动（EnergyDrv）。
/// </summary>
public static class Battery
{
    private static readonly ApplicationSettings Settings = IoCContainer.Resolve<ApplicationSettings>();
    // 放电速率的观测区间：用于平滑抖动的瞬时功率读数
    private static int MinDischargeRate { get; set; } = int.MaxValue;
    private static int MaxDischargeRate { get; set; }

    /// <summary>
    /// 根据当前电池状态更新放电速率的统计区间（最小/最大值）。
    /// 当充放电方向发生变化或断电重置时，区间会被清空后重新累积。
    /// </summary>
    /// <param name="status">电池状态；未提供时内部自行查询。</param>
    public static void SetMinMaxDischargeRate(BATTERY_STATUS? status = null)
    {
        if (!status.HasValue)
        {
            var batteryTag = GetBatteryTag();
            status = GetBatteryStatus(batteryTag);
        }

        // Rate = 0 表示空闲；正负号发生变化说明在充电/放电之间切换，之前的统计值已失效
        if (status.Value.Rate == 0
            || (status.Value.Rate > 0 && (MinDischargeRate < 0 || MaxDischargeRate < 0))
            || (status.Value.Rate < 0 && (MinDischargeRate > 0 || MaxDischargeRate > 0)))
        {
            MinDischargeRate = int.MaxValue;
            MaxDischargeRate = 0;
        }

        if (status.Value.Rate != 0)
        {
            // 用绝对值比较，兼容充电时 Rate 为正、放电时为负的约定
            if (Math.Abs(status.Value.Rate) < Math.Abs(MinDischargeRate))
                MinDischargeRate = status.Value.Rate;
            if (Math.Abs(status.Value.Rate) > Math.Abs(MaxDischargeRate))
                MaxDischargeRate = status.Value.Rate;
        }
    }

    /// <summary>
    /// 汇总读取电池的全部可用信息。
    /// </summary>
    /// <returns>包含电量、容量、循环次数、温度与日期等字段的电池信息对象。</returns>
    public static BatteryInformation GetBatteryInformation()
    {
        var powerStatus = GetSystemPowerStatus();

        var batteryTag = GetBatteryTag();
        var information = GetBatteryInformation(batteryTag);
        var status = GetBatteryStatus(batteryTag);

        double? temperatureC = null;
        DateTime? manufactureDate = null;
        DateTime? firstUseDate = null;

        SetMinMaxDischargeRate(status);

        // 扩展信息依赖联想驱动，非联想机型或驱动缺失时忽略异常，保持基础信息可用
        try
        {
            var lenovoBatteryInformation = FindLenovoBatteryInformation();
            if (lenovoBatteryInformation.HasValue)
            {
                temperatureC = DecodeTemperatureC(lenovoBatteryInformation.Value.Temperature);
                manufactureDate = DecodeDateTime(lenovoBatteryInformation.Value.ManufactureDate);
                firstUseDate = DecodeDateTime(lenovoBatteryInformation.Value.FirstUseDate);
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to get temperature of battery.", ex);
        }

        return new(powerStatus.ACLineStatus == 1,
            powerStatus.BatteryLifePercent,
            (int)powerStatus.BatteryLifeTime,
            (int)powerStatus.BatteryFullLifeTime,
            status.Rate,
            // 空闲时统计区间无意义，直接输出 0
            (status.Rate == 0) ? 0 : MinDischargeRate,
            MaxDischargeRate,
            (int)status.Capacity,
            (int)information.DesignedCapacity,
            (int)information.FullChargedCapacity,
            (int)information.CycleCount,
            // 低电量告警：使用电池且剩余容量低于系统默认告警阈值(DefaultAlert2)
            powerStatus.ACLineStatus == 0 && information.DefaultAlert2 >= status.Capacity,
            temperatureC,
            manufactureDate,
            firstUseDate);
    }

    /// <summary>
    /// 获取电池温度（摄氏度）。
    /// </summary>
    /// <returns>温度值；无法读取时返回 null。</returns>
    public static double? GetBatteryTemperatureC()
    {
        try
        {
            var lenovoBatteryInformation = FindLenovoBatteryInformation();
            return lenovoBatteryInformation.HasValue ? DecodeTemperatureC(lenovoBatteryInformation.Value.Temperature) : null;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to get temperature of battery.", ex);
            return null;
        }
    }

    /// <summary>
    /// 获取本次使用电池（脱离电源）的起始时间。
    /// 通过解析系统事件日志中的 EventID=105（电源来源变化）事件推导。
    /// </summary>
    /// <returns>开始使用电池的时间；无法确定时返回 null。</returns>
    public static DateTime? GetOnBatterySince()
    {
        try
        {
            var resetOnReboot = Settings.Store.ResetBatteryOnSinceTimerOnReboot;

            // Environment.TickCount 为开机以来的毫秒数，反推即可得到本次开机时间
            var lastRebootTime = DateTime.Now - TimeSpan.FromMilliseconds(Environment.TickCount);

            var logs = new List<(DateTime Date, bool IsACOnline)>();

            // Kernel-Power 事件 105：电源来源切换，EventData 中的 AcOnline 指示是否接入交流电
            var query = new EventLogQuery("System", PathType.LogName, "*[System[EventID=105]]");
            using var logReader = new EventLogReader(query);
            using var propertySelector = new EventLogPropertySelector(["Event/EventData/Data[@Name='AcOnline']"]);

            while (logReader.ReadEvent() is EventLogRecord record)
            {
                var date = record.TimeCreated;
                var isAcOnline = record.GetPropertyValues(propertySelector)[0] as bool?;

                if (date is null || isAcOnline is null)
                    continue;

                // 按设置决定是否只统计本次开机之后的事件
                if (resetOnReboot && date < lastRebootTime)
                    continue;

                logs.Add((date.Value, isAcOnline.Value));
            }

            if (logs.Count < 1)
                return null;

            // 事件日志按时间倒序读出，反转后变为正序
            logs.Reverse();

            // 取"最近一段连续使用电池"的最早一条记录
            var (dateTime, _) = logs
                .TakeWhile(log => log.IsACOnline != true)
                .LastOrDefault();

            return dateTime;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to get event.", ex);
        }

        return null;
    }

    /// <summary>
    /// 读取系统电源状态（Win32 GetSystemPowerStatus）。
    /// </summary>
    /// <returns>系统电源状态结构体。</returns>
    private static SYSTEM_POWER_STATUS GetSystemPowerStatus()
    {
        var result = PInvoke.GetSystemPowerStatus(out var sps);

        if (!result)
            PInvokeExtensions.ThrowIfWin32Error("GetSystemPowerStatus");

        return sps;
    }

    /// <summary>
    /// 查询电池标签（Battery Tag）。该标签用于后续所有电池 IOCTL 请求，电池热插拔后会变化。
    /// </summary>
    /// <returns>当前电池标签。</returns>
    private static uint GetBatteryTag()
    {
        var result = PInvokeExtensions.DeviceIoControl(Devices.GetBattery(),
            PInvoke.IOCTL_BATTERY_QUERY_TAG,
            0u,
            out uint tag);

        if (!result)
            PInvokeExtensions.ThrowIfWin32Error("DeviceIoControl, IOCTL_BATTERY_QUERY_TAG");

        return tag;
    }

    /// <summary>
    /// 查询电池静态信息（设计容量、满充容量、循环次数等）。
    /// </summary>
    /// <param name="batteryTag">电池标签。</param>
    /// <returns>电池信息结构体。</returns>
    private static BATTERY_INFORMATION GetBatteryInformation(uint batteryTag)
    {
        // InformationLevel 指定要取回的 BIF 结构类型
        var queryInformation = new BATTERY_QUERY_INFORMATION
        {
            BatteryTag = batteryTag,
            InformationLevel = BATTERY_QUERY_INFORMATION_LEVEL.BatteryInformation,
        };

        var result = PInvokeExtensions.DeviceIoControl(Devices.GetBattery(),
            PInvoke.IOCTL_BATTERY_QUERY_INFORMATION,
            queryInformation,
            out BATTERY_INFORMATION bi);

        if (!result)
            PInvokeExtensions.ThrowIfWin32Error("DeviceIoControl, IOCTL_BATTERY_QUERY_INFORMATION");
        return bi;
    }

    /// <summary>
    /// 查询电池动态状态（剩余容量、充放电速率等）。
    /// </summary>
    /// <param name="batteryTag">电池标签。</param>
    /// <returns>电池状态结构体。</returns>
    private static BATTERY_STATUS GetBatteryStatus(uint batteryTag)
    {
        // BATTERY_WAIT_STATUS 的时间/容量阈值均留空，表示立即返回当前状态
        var waitStatus = new BATTERY_WAIT_STATUS
        {
            BatteryTag = batteryTag,
        };
        var result = PInvokeExtensions.DeviceIoControl(Devices.GetBattery(),
            PInvoke.IOCTL_BATTERY_QUERY_STATUS,
            waitStatus,
            out BATTERY_STATUS s);

        if (!result)
            PInvokeExtensions.ThrowIfWin32Error("DeviceIoControl, IOCTL_BATTERY_QUERY_STATUS");

        return s;
    }

    /// <summary>
    /// 在联想能量驱动暴露的多个电池索引中查找有效数据。
    /// 不同机型/电池数量下有效索引不固定，因此最多探测前 3 个。
    /// </summary>
    /// <returns>有效的电池扩展信息；均无效时返回 null。</returns>
    private static LENOVO_BATTERY_INFORMATION? FindLenovoBatteryInformation()
    {
        for (uint index = 0; index < 3; index++)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Checking battery data at index {index}...");

            var info = GetLenovoBatteryInformation(index);
            // 0x0000 / 0xFFFF 是驱动对"该索引无电池"的占位返回值
            if (info.Temperature is ushort.MinValue or ushort.MaxValue)
                continue;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Battery data found at index {index}.");

            return info;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Battery data not found.");

        return null;
    }

    /// <summary>
    /// 通过能源驱动读取指定索引的电池扩展信息。
    /// </summary>
    /// <param name="index">电池索引（0 起）。</param>
    /// <returns>联想自定义的电池信息结构体。</returns>
    private static LENOVO_BATTERY_INFORMATION GetLenovoBatteryInformation(uint index)
    {
        var result = PInvokeExtensions.DeviceIoControl(Drivers.GetEnergy(),
            Drivers.IOCTL_ENERGY_BATTERY_INFORMATION,
            index,
            out LENOVO_BATTERY_INFORMATION bi);
        if (!result)
            PInvokeExtensions.ThrowIfWin32Error("DeviceIoControl, 0x83102138");

        return bi;
    }

    /// <summary>
    /// 解码联想驱动以 16 位压缩格式返回的日期。
    /// 位布局与 FAT 日期一致：高 7 位为年份偏移（自 1980），中间 4 位为月份，低 5 位为日。
    /// </summary>
    /// <param name="s">压缩后的日期值。</param>
    /// <returns>解码后的日期；非法值返回 null。</returns>
    private static DateTime? DecodeDateTime(ushort s)
    {
        try
        {
            if (s < 1)
                return null;

            var date = new DateTime((s >> 9) + 1980, (s >> 5) & 15, (s & 31), 0, 0, 0, DateTimeKind.Unspecified);
            // 超出 Legion 产品存在区间的日期视为驱动返回的脏数据
            if (date.Year is < 2018 or > 2030)
                return null;
            return date;
        }
        catch
        {
            // 月/日越界时 DateTime 构造会抛异常，按无效值处理
            return null;
        }
    }

    /// <summary>
    /// 解码联想驱动返回的电池温度。
    /// 驱动以 0.1 开尔文为单位返回，2731.6 对应 0 摄氏度（273.16 K × 10）。
    /// </summary>
    /// <param name="s">原始温度值。</param>
    /// <returns>摄氏度温度；负值（无效读数）返回 null。</returns>
    private static double? DecodeTemperatureC(ushort s)
    {
        var value = (s - 2731.6) / 10.0;
        if (value < 0)
            return null;
        return value;
    }
}

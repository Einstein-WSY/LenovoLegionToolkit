using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using LenovoLegionToolkit.Lib.Extensions;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib;

/// <summary>
/// 电池信息快照：电量、充放电状态、健康度与温度等。
/// </summary>
public readonly struct BatteryInformation(
    bool isCharging,
    int batteryPercentage,
    int batteryLifeRemaining,
    int fullBatteryLifeRemaining,
    int dischargeRate,
    int minDischargeRate,
    int maxDischargeRate,
    int estimateChargeRemaining,
    int designCapacity,
    int fullChargeCapacity,
    int cycleCount,
    bool isLowBattery,
    double? batteryTemperatureC,
    DateTime? manufactureDate,
    DateTime? firstUseDate)
{
    public bool IsCharging { get; } = isCharging; // 是否正在充电
    public int BatteryPercentage { get; } = batteryPercentage; // 当前电量百分比
    public int BatteryLifeRemaining { get; } = batteryLifeRemaining; // 剩余可用时间（分钟）
    public int FullBatteryLifeRemaining { get; init; } = fullBatteryLifeRemaining; // 满电时的预计可用时间（分钟）
    public int DischargeRate { get; } = dischargeRate; // 当前放电功率（mW）
    public int MinDischargeRate { get; } = minDischargeRate; // 观测到的最小放电功率（mW）
    public int MaxDischargeRate { get; } = maxDischargeRate; // 观测到的最大放电功率（mW）
    public int EstimateChargeRemaining { get; } = estimateChargeRemaining; // 预计充满剩余时间（分钟）
    public int DesignCapacity { get; } = designCapacity; // 设计容量（mWh）
    public int FullChargeCapacity { get; } = fullChargeCapacity; // 当前满充容量（mWh），随磨损下降
    public int CycleCount { get; } = cycleCount; // 充放电循环次数
    public bool IsLowBattery { get; } = isLowBattery; // 是否处于低电量状态
    public double? BatteryTemperatureC { get; } = batteryTemperatureC; // 电池温度（摄氏度）
    public DateTime? ManufactureDate { get; } = manufactureDate; // 电池生产日期
    public DateTime? FirstUseDate { get; } = firstUseDate; // 电池首次使用日期

    /// <summary>
    /// 电池健康度（百分比）= 满充容量 / 设计容量 × 100，保留两位小数。
    /// 设计容量为 0 时返回 0，避免除零。
    /// </summary>
    public double BatteryHealth =>
        DesignCapacity > 0
        ? Math.Round((double)FullChargeCapacity / DesignCapacity * 100.0, 2, MidpointRounding.AwayFromZero)
        : 0.0;
}

/// <summary>
/// BIOS 版本号：由前缀（机型代号，如 "GKCN"）与数字版本组成，用于版本比较。
/// </summary>
public readonly struct BiosVersion(string prefix, int? version)
{
    public string Prefix { get; } = prefix; // BIOS 版本前缀，标识机型/平台
    public int? Version { get; } = version; // 数字版本号，可能无法解析而缺失

    /// <summary>
    /// 判断当前版本是否不低于 <paramref name="other"/>。
    /// 仅当前缀一致时才有可比性；任一版本号为 null 时保守地返回 true。
    /// </summary>
    /// <param name="other">用于比较的目标版本。</param>
    /// <returns>当前版本 &gt;= 目标版本时为 true。</returns>
    public bool IsHigherOrEqualThan(BiosVersion other)
    {
        if (!Prefix.Equals(other.Prefix, StringComparison.InvariantCultureIgnoreCase))
            return false;

        if (Version is null || other.Version is null)
            return true;

        return Version >= other.Version;
    }

    /// <summary>
    /// 判断当前版本是否低于 <paramref name="other"/>。
    /// 前缀不一致时返回 false；任一版本号为 null 时保守地返回 true。
    /// </summary>
    /// <param name="other">用于比较的目标版本。</param>
    /// <returns>当前版本 &lt; 目标版本时为 true。</returns>
    public bool IsLowerThan(BiosVersion other)
    {
        if (!Prefix.Equals(other.Prefix, StringComparison.InvariantCultureIgnoreCase))
            return false;

        if (Version is null || other.Version is null)
            return true;

        return Version < other.Version;
    }

    public override string ToString() => $"{nameof(Prefix)}: {Prefix}, {nameof(Version)}: {Version}";
}

/// <summary>
/// 屏幕亮度值（0-255，对应 WMI 亮度接口的原始值）。
/// </summary>
public readonly struct Brightness(byte value)
{
    public byte Value { get; } = value;
}

/// <summary>
/// 离散能力项：某个 <see cref="CapabilityID"/> 对应的整型取值（非区间型能力）。
/// </summary>
public readonly struct DiscreteCapability(CapabilityID id, int value)
{
    public CapabilityID Id { get; } = id;
    public int Value { get; } = value;
}

/// <summary>
/// Windows 高级颜色（HDR / 广色域）的支持与启用状态。
/// </summary>
public readonly struct DisplayAdvancedColorInfo(bool advancedColorSupported, bool advancedColorEnabled, bool wideColorEnforced, bool advancedColorForceDisabled)
{
    public bool AdvancedColorSupported { get; } = advancedColorSupported; // 硬件是否支持高级颜色
    public bool AdvancedColorEnabled { get; } = advancedColorEnabled; // 当前是否已启用
    public bool WideColorEnforced { get; } = wideColorEnforced; // 是否由系统/驱动强制启用广色域
    public bool AdvancedColorForceDisabled { get; } = advancedColorForceDisabled; // 是否被策略强制禁用，导致无法开启
}

/// <summary>
/// 设备树中一个设备节点的只读快照（来自 SetupAPI / WMI 查询结果）。
/// </summary>
public struct Device(
    string name,
    string description,
    string busReportedDeviceDescription,
    string deviceInstanceId,
    Guid classGuid,
    string className,
    bool isRemovable,
    bool isDisconnected)
{
    public string Name { get; } = name; // 设备友好名称
    public string Description { get; } = description; // 设备描述
    public string BusReportedDeviceDescription { get; } = busReportedDeviceDescription; // 总线报告的设备描述（更底层、更稳定）
    public string DeviceInstanceId { get; } = deviceInstanceId; // 设备实例 ID（如 PCI\VEN_10DE...）
    public Guid ClassGuid { get; } = classGuid; // 设备类 GUID
    public string ClassName { get; } = className; // 设备类名称
    public bool IsRemovable { get; } = isRemovable; // 是否为可移动设备
    public bool IsDisconnected { get; } = isDisconnected; // 当前是否已断开（存在但不在线）

    private string? _index; // 延迟计算的唯一索引缓存

    /// <summary>
    /// 由多个字段拼接而成的唯一索引，用于设备匹配与去重；首次访问时惰性计算并缓存。
    /// </summary>
    public string Index
    {
        get
        {
            _index ??= new StringBuilder()
                .Append(ClassName)
                .Append(ClassGuid)
                .Append(BusReportedDeviceDescription)
                .Append(Description)
                .Append(Name)
                .Append(DeviceInstanceId)
                .ToString();
            return _index;
        }
    }
}

/// <summary>
/// 驱动程序信息：所属设备、硬件 ID、驱动版本与日期。
/// </summary>
public readonly struct DriverInfo(string deviceId, string hardwareId, Version? version, DateTime? date)
{
    public string DeviceId { get; } = deviceId;
    public string HardwareId { get; } = hardwareId;
    public Version? Version { get; } = version;
    public DateTime? Date { get; } = date;
}

/// <summary>
/// 风扇曲线表中单个点的数据：某风扇/传感器的转速-温度映射。
/// </summary>
public readonly struct FanTableData(FanTableType type, byte fanId, byte sensorId, ushort[] fanSpeeds, ushort[] temps)
{
    public FanTableType Type { get; } = type; // 风扇表类型（CPU/GPU 等）
    public byte FanId { get; } = fanId; // 风扇编号
    public byte SensorId { get; } = sensorId; // 关联的温度传感器编号
    public ushort[] FanSpeeds { get; } = fanSpeeds; // 各温度档位对应的目标转速
    public ushort[] Temps { get; } = temps; // 与转速对应的温度阈值

    public override string ToString() =>
        $"{nameof(Type)}: {Type}," +
        $" {nameof(FanId)}: {FanId}," +
        $" {nameof(SensorId)}: {SensorId}," +
        $" {nameof(FanSpeeds)}: [{string.Join(", ", FanSpeeds)}]," +
        $" {nameof(Temps)}: [{string.Join(", ", Temps)}]";
}

/// <summary>
/// 风扇曲线表二进制结构（64 字节），与 BIOS/WMI 接受的 Fan Table 布局一致。
/// 字段名沿用联想固件命名：FSTM=表模式、FSID=表 ID、FSTL=表长度、FSS0..FSS9=十个档位转速。
/// </summary>
public readonly struct FanTable
{
    // ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
    // ReSharper disable MemberCanBePrivate.Global
    // ReSharper disable IdentifierTypo
    // ReSharper disable InconsistentNaming

    public byte FSTM { get; init; } // 表模式，固定写入 1
    public byte FSID { get; init; } // 表 ID，固定写入 0
    public uint FSTL { get; init; } // 表长度，固定写入 0
    public ushort FSS0 { get; init; } // 档位 0 转速
    public ushort FSS1 { get; init; } // 档位 1 转速
    public ushort FSS2 { get; init; } // 档位 2 转速
    public ushort FSS3 { get; init; } // 档位 3 转速
    public ushort FSS4 { get; init; } // 档位 4 转速
    public ushort FSS5 { get; init; } // 档位 5 转速
    public ushort FSS6 { get; init; } // 档位 6 转速
    public ushort FSS7 { get; init; } // 档位 7 转速
    public ushort FSS8 { get; init; } // 档位 8 转速
    public ushort FSS9 { get; init; } // 档位 9 转速

    // ReSharper restore AutoPropertyCanBeMadeGetOnly.Global
    // ReSharper restore MemberCanBePrivate.Global
    // ReSharper restore IdentifierTypo
    // ReSharper restore InconsistentNaming

    /// <summary>
    /// 从 10 个转速值构造风扇表，并填充固定的表头字段。
    /// </summary>
    /// <param name="fanTable">长度为 10 的转速数组。</param>
    /// <exception cref="ArgumentException">数组长度不是 10 时抛出。</exception>
    public FanTable(ushort[] fanTable)
    {
        if (fanTable.Length != 10)
            // ReSharper disable once LocalizableElement
            throw new ArgumentException("Fan table length must be 10", nameof(fanTable));

        FSTM = 1;
        FSID = 0;
        FSTL = 0;
        FSS0 = fanTable[0];
        FSS1 = fanTable[1];
        FSS2 = fanTable[2];
        FSS3 = fanTable[3];
        FSS4 = fanTable[4];
        FSS5 = fanTable[5];
        FSS6 = fanTable[6];
        FSS7 = fanTable[7];
        FSS8 = fanTable[8];
        FSS9 = fanTable[9];
    }

    /// <summary>
    /// 按档位顺序返回 10 个转速值。
    /// </summary>
    public ushort[] GetTable() => [FSS0, FSS1, FSS2, FSS3, FSS4, FSS5, FSS6, FSS7, FSS8, FSS9];

    /// <summary>
    /// 序列化为 64 字节数组（1+1+4+10×2，其余补零），供 WMI/固件接口使用。
    /// </summary>
    public byte[] GetBytes()
    {
        using var ms = new MemoryStream(new byte[64]); // 固定 64 字节缓冲区，未写入部分保持 0
        ms.WriteByte(FSTM);
        ms.WriteByte(FSID);
        ms.Write(BitConverter.GetBytes(FSTL));
        ms.Write(BitConverter.GetBytes(FSS0));
        ms.Write(BitConverter.GetBytes(FSS1));
        ms.Write(BitConverter.GetBytes(FSS2));
        ms.Write(BitConverter.GetBytes(FSS3));
        ms.Write(BitConverter.GetBytes(FSS4));
        ms.Write(BitConverter.GetBytes(FSS5));
        ms.Write(BitConverter.GetBytes(FSS6));
        ms.Write(BitConverter.GetBytes(FSS7));
        ms.Write(BitConverter.GetBytes(FSS8));
        ms.Write(BitConverter.GetBytes(FSS9));
        return ms.ToArray();
    }

    public override string ToString() =>
        $"{nameof(FSTM)}: {FSTM}," +
        $" {nameof(FSID)}: {FSID}," +
        $" {nameof(FSTL)}: {FSTL}," +
        $" {nameof(FSS0)}: {FSS0}," +
        $" {nameof(FSS1)}: {FSS1}," +
        $" {nameof(FSS2)}: {FSS2}," +
        $" {nameof(FSS3)}: {FSS3}," +
        $" {nameof(FSS4)}: {FSS4}," +
        $" {nameof(FSS5)}: {FSS5}," +
        $" {nameof(FSS6)}: {FSS6}," +
        $" {nameof(FSS7)}: {FSS7}," +
        $" {nameof(FSS8)}: {FSS8}," +
        $" {nameof(FSS9)}: {FSS9}";
}

/// <summary>
/// 完整的风扇表信息：数据点列表 + 对应的二进制表。
/// </summary>
public readonly struct FanTableInfo(FanTableData[] data, FanTable table)
{
    public FanTableData[] Data { get; } = data;
    public FanTable Table { get; } = table;

    public override string ToString() =>
        $"{nameof(Data)}: [{string.Join(", ", Data)}]," +
        $" {nameof(Table)}: {Table}";
}

/// <summary>
/// 显卡超频偏移量（核心/显存频率增量，单位 MHz）。
/// </summary>
public readonly struct GPUOverclockInfo(int coreDeltaMhz, int memoryDeltaMhz)
{
    public static readonly GPUOverclockInfo Zero = new(); // 无偏移（默认值）

    public int CoreDeltaMhz { get; } = coreDeltaMhz; // GPU 核心频率偏移
    public int MemoryDeltaMhz { get; } = memoryDeltaMhz; // 显存频率偏移

    #region Equality

    public override bool Equals(object? obj) => obj is GPUOverclockInfo other && CoreDeltaMhz == other.CoreDeltaMhz && MemoryDeltaMhz == other.MemoryDeltaMhz;

    public override int GetHashCode() => HashCode.Combine(CoreDeltaMhz, MemoryDeltaMhz);

    public static bool operator ==(GPUOverclockInfo left, GPUOverclockInfo right) => left.Equals(right);

    public static bool operator !=(GPUOverclockInfo left, GPUOverclockInfo right) => !left.Equals(right);

    #endregion

    public override string ToString() => $"{nameof(CoreDeltaMhz)}: {CoreDeltaMhz}, {nameof(MemoryDeltaMhz)}: {MemoryDeltaMhz}";

}

/// <summary>
/// 自定义模式（God Mode）各可调项的出厂默认值，用于初始化与"恢复默认"。
/// 所有字段可空：null 表示该机型/版本不支持该项。
/// </summary>
public readonly struct GodModeDefaults
{
    public int? CPULongTermPowerLimit { get; init; } // CPU 长时功耗限制 PL1（W）
    public int? CPUShortTermPowerLimit { get; init; } // CPU 短时功耗限制 PL2（W）
    public int? CPUPeakPowerLimit { get; init; } // CPU 峰值功耗（W）
    public int? CPUCrossLoadingPowerLimit { get; init; } // CPU 交叉负载功耗限制（W）
    public int? CPUPL1Tau { get; init; } // PL1 时间窗常数（秒），决定长时功耗的维持时长
    public int? APUsPPTPowerLimit { get; init; } // AMD APU 的 sPPT 功耗限制（W）
    public int? CPUTemperatureLimit { get; init; } // CPU 温度墙（摄氏度）
    public int? GPUPowerBoost { get; init; } // GPU 动态加速功耗（W）
    public int? GPUConfigurableTGP { get; init; } // GPU 可配置 TGP（W）
    public int? GPUTemperatureLimit { get; init; } // GPU 温度墙（摄氏度）
    public int? GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline { get; init; } // 接通电源时 GPU 总功耗目标相对基线的偏移
    public int? GPUToCPUDynamicBoost { get; init; } // GPU 向 CPU 动态借电能力等级
    public FanTable? FanTable { get; init; } // 默认风扇曲线表
    public bool? FanFullSpeed { get; init; } // 是否默认开启风扇全速

    public override string ToString() =>
        $"{nameof(CPULongTermPowerLimit)}: {CPULongTermPowerLimit}," +
        $" {nameof(CPUShortTermPowerLimit)}: {CPUShortTermPowerLimit}," +
        $" {nameof(CPUPeakPowerLimit)}: {CPUPeakPowerLimit}," +
        $" {nameof(CPUCrossLoadingPowerLimit)}: {CPUCrossLoadingPowerLimit}," +
        $" {nameof(CPUPL1Tau)}: {CPUPL1Tau}," +
        $" {nameof(APUsPPTPowerLimit)}: {APUsPPTPowerLimit}," +
        $" {nameof(CPUTemperatureLimit)}: {CPUTemperatureLimit}," +
        $" {nameof(GPUPowerBoost)}: {GPUPowerBoost}," +
        $" {nameof(GPUConfigurableTGP)}: {GPUConfigurableTGP}," +
        $" {nameof(GPUTemperatureLimit)}: {GPUTemperatureLimit}," +
        $" {nameof(GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline)}: {GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline}," +
        $" {nameof(GPUToCPUDynamicBoost)}: {GPUToCPUDynamicBoost}," +
        $" {nameof(FanTable)}: {FanTable}," +
        $" {nameof(FanFullSpeed)}: {FanFullSpeed}";
}

/// <summary>
/// 自定义模式的整体状态：当前激活的预设 ID 与全部预设集合。
/// </summary>
public readonly struct GodModeState
{
    public Guid ActivePresetId { get; init; } // 当前激活预设的标识
    public ReadOnlyDictionary<Guid, GodModePreset> Presets { get; init; } // 预设 ID → 预设内容
}

/// <summary>
/// 单个自定义模式预设：保存用户在 UI 中设定的各项功耗/温度/风扇参数。
/// </summary>
public readonly struct GodModePreset
{
    public string Name { get; init; } // 预设名称
    public StepperValue? CPULongTermPowerLimit { get; init; } // CPU 长时功耗限制（含范围信息）
    public StepperValue? CPUShortTermPowerLimit { get; init; } // CPU 短时功耗限制
    public StepperValue? CPUPeakPowerLimit { get; init; } // CPU 峰值功耗
    public StepperValue? CPUCrossLoadingPowerLimit { get; init; } // CPU 交叉负载功耗限制
    public StepperValue? CPUPL1Tau { get; init; } // PL1 时间窗常数
    public StepperValue? APUsPPTPowerLimit { get; init; } // AMD APU sPPT 功耗限制
    public StepperValue? CPUTemperatureLimit { get; init; } // CPU 温度墙
    public StepperValue? GPUPowerBoost { get; init; } // GPU 动态加速功耗
    public StepperValue? GPUConfigurableTGP { get; init; } // GPU 可配置 TGP
    public StepperValue? GPUTemperatureLimit { get; init; } // GPU 温度墙
    public StepperValue? GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline { get; init; } // GPU 总功耗目标偏移
    public StepperValue? GPUToCPUDynamicBoost { get; init; } // GPU→CPU 动态借电等级
    public FanTableInfo? FanTableInfo { get; init; } // 风扇曲线
    public bool? FanFullSpeed { get; init; } // 风扇全速开关
    public int? MinValueOffset { get; init; } // 所有可调项统一最小值偏移（用于机型微调）
    public int? MaxValueOffset { get; init; } // 所有可调项统一最大值偏移

    public override string ToString() =>
        $"{nameof(Name)}: {Name}," +
        $" {nameof(CPULongTermPowerLimit)}: {CPULongTermPowerLimit}," +
        $" {nameof(CPUShortTermPowerLimit)}: {CPUShortTermPowerLimit}," +
        $" {nameof(CPUPeakPowerLimit)}: {CPUPeakPowerLimit}," +
        $" {nameof(CPUCrossLoadingPowerLimit)}: {CPUCrossLoadingPowerLimit}," +
        $" {nameof(CPUPL1Tau)}: {CPUPL1Tau}," +
        $" {nameof(APUsPPTPowerLimit)}: {APUsPPTPowerLimit}," +
        $" {nameof(CPUTemperatureLimit)}: {CPUTemperatureLimit}," +
        $" {nameof(GPUPowerBoost)}: {GPUPowerBoost}," +
        $" {nameof(GPUConfigurableTGP)}: {GPUConfigurableTGP}," +
        $" {nameof(GPUTemperatureLimit)}: {GPUTemperatureLimit}," +
        $" {nameof(GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline)}: {GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline}," +
        $" {nameof(GPUToCPUDynamicBoost)}: {GPUToCPUDynamicBoost}," +
        $" {nameof(FanTableInfo)}: {FanTableInfo}," +
        $" {nameof(FanFullSpeed)}: {FanFullSpeed}," +
        $" {nameof(MinValueOffset)}: {MinValueOffset}," +
        $" {nameof(MaxValueOffset)}: {MaxValueOffset}";
}

/// <summary>
/// GPU 运行状态：当前状态、性能状态字符串与占用 GPU 的进程列表。
/// </summary>
public readonly struct GPUStatus(GPUState state, string? performanceState, List<Process> processes)
{
    public GPUState State { get; } = state; // GPU 电源/工作状态（如 Active、PoweredOff）
    public string? PerformanceState { get; } = performanceState; // 性能状态（P-State，由驱动报告）
    public List<Process> Processes { get; } = processes; // 正在使用 GPU 的进程
    public int ProcessCount => Processes.Count; // 占用 GPU 的进程数量
}

/// <summary>
/// 硬件 ID：厂商 ID + 设备 ID，比较时忽略大小写。
/// </summary>
public readonly struct HardwareId(string vendor, string device)
{
    public static readonly HardwareId Empty = new(); // 空硬件 ID，用于表示"未匹配"

    public string Vendor { get; } = vendor; // 厂商 ID（VEN_xxxx）
    public string Device { get; } = device; // 设备 ID（DEV_xxxx）

    #region Equality

    public override bool Equals(object? obj)
    {
        if (obj is not HardwareId other)
            return false;

        if (!Vendor.Equals(other.Vendor, StringComparison.InvariantCultureIgnoreCase))
            return false;

        if (!Device.Equals(other.Device, StringComparison.InvariantCultureIgnoreCase))
            return false;

        return true;
    }

    public override int GetHashCode() => HashCode.Combine(Vendor, Device);

    public static bool operator ==(HardwareId left, HardwareId right) => left.Equals(right);

    public static bool operator !=(HardwareId left, HardwareId right) => !left.Equals(right);

    #endregion
}

/// <summary>
/// 整机信息：厂商、机型、序列号、BIOS、支持的功能与机型特性。
/// 是本项目做硬件适配判断的核心数据源。
/// </summary>
public readonly struct MachineInformation
{
    /// <summary>
    /// 机型能力集合：来自能力位（Flags）或能力数据（CapabilityData）的 <see cref="CapabilityID"/> 集合。
    /// </summary>
    public readonly struct FeatureData(FeatureData.SourceType sourceType, IEnumerable<CapabilityID> capabilities)
    {
        public static readonly FeatureData Unknown = new(SourceType.Unknown); // 未知能力（空集合）

        /// <summary>
        /// 能力数据的来源类型。
        /// </summary>
        public enum SourceType
        {
            Unknown, // 未知来源
            Flags, // 来自固定的能力标志位
            CapabilityData // 来自运行时查询的能力数据
        }

        private readonly HashSet<CapabilityID> _capabilities = [.. capabilities]; // 去重的能力集合

        public SourceType Source { get; } = sourceType; // 数据来源

        /// <summary>
        /// 按枚举顺序返回所有已具备的能力。
        /// </summary>
        public IEnumerable<CapabilityID> All => _capabilities.Order().AsEnumerable();

        public FeatureData(SourceType sourceType) : this(sourceType, []) { }

        /// <summary>
        /// 按能力 ID 读写是否具备该能力（init 时真则加入、假则移除）。
        /// </summary>
        /// <param name="key">要查询/设置的能力 ID。</param>
        public bool this[CapabilityID key]
        {
            get => _capabilities.Contains(key);
            init
            {
                if (value)
                    _capabilities.Add(key);
                else
                    _capabilities.Remove(key);
            }
        }
    }

    /// <summary>
    /// 机型特性标志：描述该机型在功能上的支持情况与已知问题。
    /// </summary>
    public readonly struct PropertyData
    {
        public bool SupportsGodMode => SupportsGodModeV1 || SupportsGodModeV2; // 任一版本的自定义模式可用即可

        public (bool status, bool connectivity) SupportsAlwaysOnAc { get; init; } // 支持"接通电源时保持常开"及其是否可配置
        public bool SupportsGodModeV1 { get; init; } // 支持 V1 版自定义模式
        public bool SupportsGodModeV2 { get; init; } // 支持 V2 版自定义模式
        public bool SupportsGSync { get; init; } // 支持 G-Sync
        public bool SupportsIGPUMode { get; init; } // 支持集显模式切换
        public bool SupportsAIMode { get; init; } // 支持 AI 模式
        public bool SupportBootLogoChange { get; init; } // 支持自定义开机 Logo
        public bool HasQuietToPerformanceModeSwitchingBug { get; init; } // 存在安静→性能模式切换的固件缺陷
        public bool HasGodModeToOtherModeSwitchingBug { get; init; } // 存在自定义→其他模式切换的固件缺陷
        public bool IsExcludedFromLenovoLighting { get; init; } // 是否不适用联想灯光协议
        public bool IsExcludedFromPanelLogoLenovoLighting { get; init; } // 面板 Logo 是否不适用联想灯光协议
        public bool HasAlternativeFullSpectrumLayout { get; init; } // 是否使用另一种全彩键盘布局
    }

    public string Vendor { get; init; } // 厂商
    public string MachineType { get; init; } // 机型类型编号
    public string Model { get; init; } // 具体型号
    public string SerialNumber { get; init; } // 序列号
    public BiosVersion? BiosVersion { get; init; } // 解析后的 BIOS 版本
    public string? BiosVersionRaw { get; init; } // 原始 BIOS 版本字符串
    public PowerModeState[] SupportedPowerModes { get; init; } // 支持的电源模式列表
    public int SmartFanVersion { get; init; } // Smart Fan 功能版本，0 表示不支持
    public int LegionZoneVersion { get; init; } // Legion Zone 软件版本
    public FeatureData Features { get; init; } // 能力集合
    public PropertyData Properties { get; init; } // 机型特性标志
}

/// <summary>
/// 可下载的驱动/软件包信息（来自联想 PCSupport 或 Vantage 数据源）。
/// </summary>
public struct Package
{
    public string Id { get; init; } // 包唯一标识
    public string Title { get; init; } // 标题
    public string Description { get; init; } // 说明
    public string Version { get; init; } // 版本号
    public string Category { get; init; } // 分类（BIOS/驱动/应用等）
    public string FileName { get; init; } // 文件名
    public string FileSize { get; init; } // 文件大小（含单位的展示字符串）
    public string? FileCrc { get; init; } // 文件 CRC 校验值，用于校验下载完整性
    public DateTime ReleaseDate { get; init; } // 发布日期
    public string? Readme { get; init; } // 更新说明
    public string FileLocation { get; init; } // 下载地址
    public bool IsUpdate { get; init; } // 相对已安装版本是否为更新
    public RebootType Reboot { get; init; } // 安装后需要的重启方式

    private string? _index; // 延迟计算的索引缓存

    /// <summary>
    /// 用于搜索/去重的复合索引（标题+说明+版本+分类+文件名），首次访问时惰性计算。
    /// </summary>
    public string Index
    {
        get
        {
            _index ??= new StringBuilder()
                .Append(Title)
                .Append(Description)
                .Append(Version)
                .Append(Category)
                .Append(FileName)
                .ToString();
            return _index;
        }
    }
}

/// <summary>
/// Windows 电源计划：GUID、名称与是否为当前激活计划。
/// </summary>
public readonly struct WindowsPowerPlan(Guid guid, string name, bool isActive)
{
    public Guid Guid { get; } = guid; // 电源计划 GUID
    public string Name { get; } = name; // 电源计划显示名称
    public bool IsActive { get; } = isActive; // 是否为当前激活计划

    public override string ToString() => $"{nameof(Guid)}: {Guid}, {nameof(Name)}: {Name}, {nameof(IsActive)}: {IsActive}";

    #region Equality

    public override bool Equals(object? obj) => obj is WindowsPowerPlan other && Guid.Equals(other.Guid);

    public override int GetHashCode() => Guid.GetHashCode();

    public static bool operator ==(WindowsPowerPlan left, WindowsPowerPlan right) => left.Equals(right);

    public static bool operator !=(WindowsPowerPlan left, WindowsPowerPlan right) => !left.Equals(right);

    #endregion
}

/// <summary>
/// 进程信息：进程名与可执行文件路径，支持按名称/路径排序。
/// </summary>
[method: JsonConstructor]
public readonly struct ProcessInfo(string name, string? executablePath) : IComparable
{
    /// <summary>
    /// 由可执行文件完整路径构造进程信息（名称取不带扩展名的文件名）。
    /// </summary>
    /// <param name="path">可执行文件路径。</param>
    public static ProcessInfo FromPath(string path) => new(Path.GetFileNameWithoutExtension(path), path);

    public string Name { get; } = name; // 进程名

    public string? ExecutablePath { get; } = executablePath; // 可执行文件完整路径

    public override string ToString() => $"{nameof(Name)}: {Name}, {nameof(ExecutablePath)}: {ExecutablePath}";

    #region Equality

    public int CompareTo(object? obj)
    {
        var other = obj is null ? default : (ProcessInfo)obj;
        var result = string.Compare(Name, other.Name, StringComparison.InvariantCultureIgnoreCase);
        return result != 0 ? result : string.Compare(ExecutablePath, other.ExecutablePath, StringComparison.InvariantCultureIgnoreCase);
    }

    public override bool Equals(object? obj) => obj is ProcessInfo info && Name == info.Name && ExecutablePath == info.ExecutablePath;

    public override int GetHashCode() => HashCode.Combine(Name, ExecutablePath);

    public static bool operator ==(ProcessInfo left, ProcessInfo right) => left.Equals(right);

    public static bool operator !=(ProcessInfo left, ProcessInfo right) => !(left == right);

    public static bool operator <(ProcessInfo left, ProcessInfo right) => left.CompareTo(right) < 0;

    public static bool operator <=(ProcessInfo left, ProcessInfo right) => left.CompareTo(right) <= 0;

    public static bool operator >(ProcessInfo left, ProcessInfo right) => left.CompareTo(right) > 0;

    public static bool operator >=(ProcessInfo left, ProcessInfo right) => left.CompareTo(right) >= 0;

    #endregion
}

/// <summary>
/// 区间型能力约束：默认值、最小值、最大值与步进，用于构建可调参数的 UI 控件。
/// </summary>
public readonly struct RangeCapability(CapabilityID id, int defaultValue, int min, int max, int step)
{
    public CapabilityID Id { get; } = id; // 能力 ID
    public int DefaultValue { get; } = defaultValue; // 默认值
    public int Min { get; } = min; // 允许的最小值
    public int Max { get; } = max; // 允许的最大值
    public int Step { get; } = step; // 调节步进
}

/// <summary>
/// RGB 颜色值（每通道 0-255），并提供若干预置颜色。
/// </summary>
[method: JsonConstructor]
public readonly struct RGBColor(byte r, byte g, byte b)
{
    public static readonly RGBColor Green = new(142, 255, 0); // 联想主题绿
    public static readonly RGBColor Pink = new(186, 0, 255); // 粉色
    public static readonly RGBColor Purple = new(101, 0, 255); // 紫色
    public static readonly RGBColor Red = new(255, 0, 0); // 红色
    public static readonly RGBColor Teal = new(0, 212, 255); // 青色
    public static readonly RGBColor White = new(255, 255, 255); // 白色

    public byte R { get; } = r; // 红色通道
    public byte G { get; } = g; // 绿色通道
    public byte B { get; } = b; // 蓝色通道

    #region Equality

    public override bool Equals(object? obj)
    {
        return obj is RGBColor color && R == color.R && G == color.G && B == color.B;
    }

    public override int GetHashCode() => (R, G, B).GetHashCode();

    public static bool operator ==(RGBColor left, RGBColor right) => left.Equals(right);

    public static bool operator !=(RGBColor left, RGBColor right) => !left.Equals(right);

    #endregion

    public override string ToString() => $"{nameof(R)}: {R}, {nameof(G)}: {G}, {nameof(B)}: {B}";
}

/// <summary>
/// 四分区 RGB 键盘的一个灯效预设描述：灯效、速度、亮度与四个分区的颜色。
/// </summary>
[method: JsonConstructor]
public readonly struct RGBKeyboardBacklightBacklightPresetDescription(
    RGBKeyboardBacklightEffect effect,
    RGBKeyboardBacklightSpeed speed,
    RGBKeyboardBacklightBrightness brightness,
    RGBColor zone1,
    RGBColor zone2,
    RGBColor zone3,
    RGBColor zone4)
{
    public static readonly RGBKeyboardBacklightBacklightPresetDescription Default = new(RGBKeyboardBacklightEffect.Static, RGBKeyboardBacklightSpeed.Slowest, RGBKeyboardBacklightBrightness.High, RGBColor.White, RGBColor.White, RGBColor.White, RGBColor.White);

    public RGBKeyboardBacklightEffect Effect { get; } = effect; // 灯效类型
    public RGBKeyboardBacklightSpeed Speed { get; } = speed; // 灯效速度
    public RGBKeyboardBacklightBrightness Brightness { get; } = brightness; // 亮度档位
    public RGBColor Zone1 { get; } = zone1; // 分区 1 颜色
    public RGBColor Zone2 { get; } = zone2; // 分区 2 颜色
    public RGBColor Zone3 { get; } = zone3; // 分区 3 颜色
    public RGBColor Zone4 { get; } = zone4; // 分区 4 颜色

    #region Equality

    public override bool Equals(object? obj)
    {
        return obj is RGBKeyboardBacklightBacklightPresetDescription settings &&
               Effect == settings.Effect &&
               Speed == settings.Speed &&
               Brightness == settings.Brightness &&
               Zone1.Equals(settings.Zone1) &&
               Zone2.Equals(settings.Zone2) &&
               Zone3.Equals(settings.Zone3) &&
               Zone4.Equals(settings.Zone4);
    }

    public override int GetHashCode() => HashCode.Combine(Effect, Speed, Brightness, Zone1, Zone2, Zone3, Zone4);

    public static bool operator ==(RGBKeyboardBacklightBacklightPresetDescription left, RGBKeyboardBacklightBacklightPresetDescription right) => left.Equals(right);

    public static bool operator !=(RGBKeyboardBacklightBacklightPresetDescription left, RGBKeyboardBacklightBacklightPresetDescription right) => !(left == right);

    #endregion

    public override string ToString() =>
        $"{nameof(Effect)}: {Effect}," +
        $" {nameof(Speed)}: {Speed}," +
        $" {nameof(Brightness)}: {Brightness}," +
        $" {nameof(Zone1)}: {Zone1}," +
        $" {nameof(Zone2)}: {Zone2}," +
        $" {nameof(Zone3)}: {Zone3}," +
        $" {nameof(Zone4)}: {Zone4}";
}

/// <summary>
/// RGB 键盘灯效状态：当前选中的预设与各预设的具体描述。
/// </summary>
[method: JsonConstructor]
public readonly struct RGBKeyboardBacklightState(
    RGBKeyboardBacklightPreset selectedPreset,
    Dictionary<RGBKeyboardBacklightPreset, RGBKeyboardBacklightBacklightPresetDescription> presets)
{
    public RGBKeyboardBacklightPreset SelectedPreset { get; } = selectedPreset; // 当前选中的预设
    public Dictionary<RGBKeyboardBacklightPreset, RGBKeyboardBacklightBacklightPresetDescription> Presets { get; } = presets; // 预设 → 描述
}

/// <summary>
/// 单个传感器（CPU 或 GPU）的读数：占用率、频率、温度与风扇转速，均附带最大值。
/// -1 表示该项不可用。
/// </summary>
public readonly struct SensorData(
    int utilization,
    int maxUtilization,
    int coreClock,
    int maxCoreClock,
    int memoryClock,
    int maxMemoryClock,
    int temperature,
    int maxTemperature,
    int fanSpeed,
    int maxFanSpeed)
{
    public static readonly SensorData Empty = new(-1, -1, -1, -1, -1, -1, -1, -1, -1, -1); // 全 -1，表示无数据

    public int Utilization { get; } = utilization; // 当前占用率（%）
    public int MaxUtilization { get; } = maxUtilization; // 观测到的最大占用率（%）
    public int CoreClock { get; } = coreClock; // 当前核心频率（MHz）
    public int MaxCoreClock { get; } = maxCoreClock; // 观测到的最大核心频率（MHz）
    public int MemoryClock { get; } = memoryClock; // 当前显存频率（MHz）
    public int MaxMemoryClock { get; } = maxMemoryClock; // 观测到的最大显存频率（MHz）
    public int Temperature { get; } = temperature; // 当前温度（摄氏度）
    public int MaxTemperature { get; } = maxTemperature; // 观测到的最高温度（摄氏度）
    public int FanSpeed { get; } = fanSpeed; // 风扇转速（RPM）
    public int MaxFanSpeed { get; } = maxFanSpeed; // 观测到的最高风扇转速（RPM）

    public override string ToString() =>
        $"{nameof(Utilization)}: {Utilization}," +
        $" {nameof(MaxUtilization)}: {MaxUtilization}," +
        $" {nameof(CoreClock)}: {CoreClock}," +
        $" {nameof(MaxCoreClock)}: {MaxCoreClock}," +
        $" {nameof(MemoryClock)}: {MemoryClock}," +
        $" {nameof(MaxMemoryClock)}: {MaxMemoryClock}," +
        $" {nameof(Temperature)}: {Temperature}," +
        $" {nameof(MaxTemperature)}: {MaxTemperature}," +
        $" {nameof(FanSpeed)}: {FanSpeed}," +
        $" {nameof(MaxFanSpeed)}: {MaxFanSpeed}";
}

/// <summary>
/// CPU 与 GPU 两组传感器读数的组合。
/// </summary>
public readonly struct SensorsData(SensorData cpu, SensorData gpu)
{
    public static readonly SensorsData Empty = new(SensorData.Empty, SensorData.Empty); // 无数据

    public SensorData CPU { get; } = cpu; // CPU 传感器读数
    public SensorData GPU { get; } = gpu; // GPU 传感器读数

    public override string ToString() => $"{nameof(CPU)}: {CPU}, {nameof(GPU)}: {GPU}";
}

/// <summary>
/// 显示缩放比例（百分比），用于分辨率/缩放设置。
/// </summary>
[method: JsonConstructor]
public readonly struct DpiScale(int scale) : IDisplayName, IEquatable<DpiScale>
{
    public int Scale { get; } = scale; // 缩放百分比，如 125 表示 125%

    [JsonIgnore]
    public string DisplayName => $"{Scale}%"; // 界面展示文本

    #region Equality

    public override bool Equals(object? obj) => obj is DpiScale rate && Equals(rate);

    public bool Equals(DpiScale other) => Scale == other.Scale;

    public override int GetHashCode() => HashCode.Combine(Scale);

    public static bool operator ==(DpiScale left, DpiScale right) => left.Equals(right);

    public static bool operator !=(DpiScale left, DpiScale right) => !(left == right);

    #endregion
}

/// <summary>
/// 显示器刷新率（Hz）。
/// </summary>
[method: JsonConstructor]
public readonly struct RefreshRate(int frequency) : IDisplayName, IEquatable<RefreshRate>
{
    public int Frequency { get; } = frequency; // 刷新率数值（Hz）

    [JsonIgnore]
    public string DisplayName => $"{Frequency} Hz"; // 界面展示文本

    public override string ToString() => $"{Frequency}Hz";

    #region Equality

    public override bool Equals(object? obj) => obj is RefreshRate rate && Equals(rate);

    public bool Equals(RefreshRate other) => Frequency == other.Frequency;

    public override int GetHashCode() => HashCode.Combine(Frequency);

    public static bool operator ==(RefreshRate left, RefreshRate right) => left.Equals(right);

    public static bool operator !=(RefreshRate left, RefreshRate right) => !(left == right);

    #endregion
}

/// <summary>
/// 屏幕分辨率（像素宽 × 高），可隐式转换为 <see cref="Size"/>。
/// </summary>
[method: JsonConstructor]
public readonly struct Resolution(int width, int height) : IDisplayName, IEquatable<Resolution>, IComparable<Resolution>
{
    [JsonProperty]
    public int Width { get; } = width; // 水平像素数

    [JsonProperty]
    public int Height { get; } = height; // 垂直像素数

    [JsonIgnore]
    public string DisplayName => $"{Width} × {Height}"; // 界面展示文本

    public Resolution(Size size) : this(size.Width, size.Height) { } // 由 System.Drawing.Size 转换

    public override string ToString() => $"{Width}x{Height}";

    /// <summary>
    /// 先按宽度、再按高度比较，用于分辨率排序。
    /// </summary>
    /// <param name="other">作为比较目标的分辨率。</param>
    public int CompareTo(Resolution other)
    {
        var widthComparison = Width.CompareTo(other.Width);
        return widthComparison != 0
            ? widthComparison
            : Height.CompareTo(other.Height);
    }

    #region Conversion

    public static explicit operator Resolution(Size value) => new(value);

    public static implicit operator Size(Resolution data) => new(data.Width, data.Height);

    #endregion

    #region Equality

    public override bool Equals(object? obj) => obj is Resolution other && Equals(other);

    public bool Equals(Resolution other) => Width == other.Width && Height == other.Height;

    public override int GetHashCode() => HashCode.Combine(Width, Height);

    public static bool operator ==(Resolution left, Resolution right) => left.Equals(right);

    public static bool operator !=(Resolution left, Resolution right) => !(left == right);

    #endregion

}

/// <summary>
/// 全彩（Spectrum）键盘灯效定义：灯效类型、速度、方向、颜色列表与生效按键。
/// </summary>
public readonly struct SpectrumKeyboardBacklightEffect(
    SpectrumKeyboardBacklightEffectType type,
    SpectrumKeyboardBacklightSpeed speed,
    SpectrumKeyboardBacklightDirection direction,
    SpectrumKeyboardBacklightClockwiseDirection clockwiseDirection,
    RGBColor[] colors,
    ushort[] keys)
{
    public SpectrumKeyboardBacklightEffectType Type { get; } = type; // 灯效类型
    public SpectrumKeyboardBacklightSpeed Speed { get; } = speed; // 灯效速度
    public SpectrumKeyboardBacklightDirection Direction { get; } = direction; // 灯效方向
    public SpectrumKeyboardBacklightClockwiseDirection ClockwiseDirection { get; } = clockwiseDirection; // 顺时针/逆时针（旋转类灯效）
    public RGBColor[] Colors { get; } = colors; // 颜色列表
    // 整灯效（全局灯效）不需要按键列表，统一置空以节省数据量
    public ushort[] Keys { get; } = type.IsAllLightsEffect() ? [] : keys;
}

/// <summary>
/// 可调节数值项：当前值、范围、步进与可选步进列表，用于自定义模式等 UI。
/// </summary>
public readonly struct StepperValue(int value, int min, int max, int step, int[] steps, int? defaultValue)
{
    public int Value { get; } = value; // 当前值
    public int Min { get; } = min; // 最小值
    public int Max { get; } = max; // 最大值
    public int Step { get; } = step; // 步进
    public int[] Steps { get; } = steps; // 允许的离散取值列表（优先于 Step）
    public int? DefaultValue { get; } = defaultValue; // 默认值

    /// <summary>
    /// 返回仅修改当前值、保留范围信息的新实例。
    /// </summary>
    /// <param name="value">新的数值。</param>
    public StepperValue WithValue(int value) => new(value, Min, Max, Step, Steps, DefaultValue);

    public override string ToString() =>
        $"{nameof(Value)}: {Value}," +
        $" {nameof(Min)}: {Min}," +
        $" {nameof(Max)}: {Max}," +
        $" {nameof(Step)}: {Step}," +
        $" {nameof(Steps)}: [{string.Join(", ", Steps)}]," +
        $" {nameof(DefaultValue)} : {DefaultValue}";
}

/// <summary>
/// 一天中的时间（时/分），用于定时自动化规则。
/// </summary>
public readonly struct Time(int hour, int minute)
{
    public int Hour { get; } = hour; // 小时（0-23）
    public int Minute { get; } = minute; // 分钟（0-59）

    #region Equality

    public override bool Equals(object? obj) => obj is Time time && Hour == time.Hour && Minute == time.Minute;

    public override int GetHashCode() => HashCode.Combine(Hour, Minute);

    public static bool operator ==(Time left, Time right) => left.Equals(right);

    public static bool operator !=(Time left, Time right) => !(left == right);

    #endregion
}

/// <summary>
/// 窗口尺寸（逻辑像素），用于记住主窗口大小。
/// </summary>
public readonly struct WindowSize(double width, double height)
{
    public double Width { get; } = width; // 窗口宽度
    public double Height { get; } = height; // 窗口高度
}

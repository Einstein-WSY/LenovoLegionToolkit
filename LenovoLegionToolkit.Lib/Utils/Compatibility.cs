using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.System.Management;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Power;

// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// 机型兼容性判定与机器信息采集工具：读取厂商/型号/BIOS/能力位，并推导各功能支持情况。
/// </summary>
public static partial class Compatibility
{
    // 匹配 BIOS 版本前缀（4 位大写字母/数字），例如 "GKCN"。
    [GeneratedRegex("^[A-Z0-9]{4}")]
    private static partial Regex BiosPrefixRegex();

    // 匹配 BIOS 版本号（2 位数字），例如 "GKCN54WW" 中的 "54"。
    [GeneratedRegex("[0-9]{2}")]
    private static partial Regex BiosVersionRegex();

    private const string ALLOWED_VENDOR = "LENOVO";

    // 受支持的机型前缀白名单，按全球版本、中国版本与有限兼容机型分组。
    private static readonly string[] AllowedModelsPrefix = [
        // Worldwide variants
        "17ACH",
        "17ARH",
        "17ITH",
        "17IMH",

        "16ACH",
        "16AHP",
        "16APH",
        "16ARH",
        "16ARP",
        "16ARX",
        "16IAH",
        "16IAX",
        "16IRH",
        "16IRX",
        "16ITH",

        "15ACH",
        "15AHP",
        "15APH",
        "15ARH",
        "15ARP",
        "15IAH",
        "15IAX",
        "15IHU",
        "15IMH",
        "15IRH",
        "15ITH",

        "14APH",
        "14IRP",

        // Chinese variants
        "G5000",
        "R9000",
        "R7000",
        "Y9000",
        "Y7000",
            
        // Limited compatibility
        "17IR",
        "15IR",
        "15IC",
        "15IK"
    ];

    private static MachineInformation? _machineInformation;

    /// <summary>
    /// 基础兼容性检查：仅验证本机是否存在 Lenovo GameZone WMI 接口。
    /// </summary>
    /// <returns>存在则返回 true。</returns>
    public static Task<bool> CheckBasicCompatibilityAsync() => WMI.LenovoGameZoneData.ExistsAsync();

    /// <summary>
    /// 综合判断本机是否被支持：需具备 WMI 接口、厂商为 Lenovo，且型号命中白名单前缀。
    /// </summary>
    /// <returns>是否兼容的布尔值，以及已采集的机器信息。</returns>
    public static async Task<(bool isCompatible, MachineInformation machineInformation)> IsCompatibleAsync()
    {
        var mi = await GetMachineInformationAsync().ConfigureAwait(false);

        if (!await CheckBasicCompatibilityAsync().ConfigureAwait(false))
            return (false, mi);

        if (!mi.Vendor.Equals(ALLOWED_VENDOR, StringComparison.InvariantCultureIgnoreCase))
            return (false, mi);

        // 型号只要包含任一允许前缀即视为兼容（忽略大小写）。
        foreach (var allowedModel in AllowedModelsPrefix)
            if (mi.Model.Contains(allowedModel, StringComparison.InvariantCultureIgnoreCase))
                return (true, mi);

        return (false, mi);
    }

    /// <summary>
    /// 采集并缓存机器信息（厂商、机型、BIOS、电源模式、各类特性支持情况等）。
    /// 首次调用后结果会被缓存，后续直接返回。
    /// </summary>
    /// <returns>完整的机器信息对象。</returns>
    public static async Task<MachineInformation> GetMachineInformationAsync()
    {
        if (_machineInformation.HasValue)
            return _machineInformation.Value;

        var (vendor, machineType, model, serialNumber) = await GetModelDataAsync().ConfigureAwait(false);
        var (biosVersion, biosVersionRaw) = GetBIOSVersion();
        var supportedPowerModes = (await GetSupportedPowerModesAsync().ConfigureAwait(false)).ToArray();
        var smartFanVersion = await GetSmartFanVersionAsync().ConfigureAwait(false);
        var legionZoneVersion = await GetLegionZoneVersionAsync().ConfigureAwait(false);
        var features = await GetFeaturesAsync().ConfigureAwait(false);

        var machineInformation = new MachineInformation
        {
            Vendor = vendor,
            MachineType = machineType,
            Model = model,
            SerialNumber = serialNumber,
            BiosVersion = biosVersion,
            BiosVersionRaw = biosVersionRaw,
            SupportedPowerModes = supportedPowerModes,
            SmartFanVersion = smartFanVersion,
            LegionZoneVersion = legionZoneVersion,
            Features = features,
            Properties = new()
            {
                SupportsAlwaysOnAc = GetAlwaysOnAcStatus(),
                SupportsGodModeV1 = GetSupportsGodModeV1(supportedPowerModes, smartFanVersion, legionZoneVersion, biosVersion),
                SupportsGodModeV2 = GetSupportsGodModeV2(supportedPowerModes, smartFanVersion, legionZoneVersion),
                SupportsGSync = await GetSupportsGSyncAsync().ConfigureAwait(false),
                SupportsIGPUMode = await GetSupportsIGPUModeAsync().ConfigureAwait(false),
                SupportsAIMode = await GetSupportsAIModeAsync().ConfigureAwait(false),
                SupportBootLogoChange = GetSupportBootLogoChange(smartFanVersion),
                HasQuietToPerformanceModeSwitchingBug = GetHasQuietToPerformanceModeSwitchingBug(biosVersion),
                HasGodModeToOtherModeSwitchingBug = GetHasGodModeToOtherModeSwitchingBug(biosVersion),
                IsExcludedFromLenovoLighting = GetIsExcludedFromLenovoLighting(biosVersion),
                IsExcludedFromPanelLogoLenovoLighting = GetIsExcludedFromPanelLenovoLighting(machineType, model),
                HasAlternativeFullSpectrumLayout = GetHasAlternativeFullSpectrumLayout(machineType),
            }
        };

        if (Log.Instance.IsTraceEnabled)
        {
            Log.Instance.Trace($"Retrieved machine information:");
            Log.Instance.Trace($" * Vendor: '{machineInformation.Vendor}'");
            Log.Instance.Trace($" * Machine Type: '{machineInformation.MachineType}'");
            Log.Instance.Trace($" * Model: '{machineInformation.Model}'");
            Log.Instance.Trace($" * BIOS: '{machineInformation.BiosVersion}' [{machineInformation.BiosVersionRaw}]");
            Log.Instance.Trace($" * SupportedPowerModes: '{string.Join(",", machineInformation.SupportedPowerModes)}'");
            Log.Instance.Trace($" * SmartFanVersion: '{machineInformation.SmartFanVersion}'");
            Log.Instance.Trace($" * LegionZoneVersion: '{machineInformation.LegionZoneVersion}'");
            Log.Instance.Trace($" * Features: {machineInformation.Features.Source}:{string.Join(',', machineInformation.Features.All)}");
            Log.Instance.Trace($" * Properties:");
            Log.Instance.Trace($"     * SupportsAlwaysOnAc: '{machineInformation.Properties.SupportsAlwaysOnAc.status}, {machineInformation.Properties.SupportsAlwaysOnAc.connectivity}'");
            Log.Instance.Trace($"     * SupportsGodModeV1: '{machineInformation.Properties.SupportsGodModeV1}'");
            Log.Instance.Trace($"     * SupportsGodModeV2: '{machineInformation.Properties.SupportsGodModeV2}'");
            Log.Instance.Trace($"     * SupportsGSync: '{machineInformation.Properties.SupportsGSync}'");
            Log.Instance.Trace($"     * SupportsIGPUMode: '{machineInformation.Properties.SupportsIGPUMode}'");
            Log.Instance.Trace($"     * SupportsAIMode: '{machineInformation.Properties.SupportsAIMode}'");
            Log.Instance.Trace($"     * SupportBootLogoChange: '{machineInformation.Properties.SupportBootLogoChange}'");
            Log.Instance.Trace($"     * HasQuietToPerformanceModeSwitchingBug: '{machineInformation.Properties.HasQuietToPerformanceModeSwitchingBug}'");
            Log.Instance.Trace($"     * HasGodModeToOtherModeSwitchingBug: '{machineInformation.Properties.HasGodModeToOtherModeSwitchingBug}'");
            Log.Instance.Trace($"     * IsExcludedFromLenovoLighting: '{machineInformation.Properties.IsExcludedFromLenovoLighting}'");
            Log.Instance.Trace($"     * IsExcludedFromPanelLogoLenovoLighting: '{machineInformation.Properties.IsExcludedFromPanelLogoLenovoLighting}'");
            Log.Instance.Trace($"     * HasAlternativeFullSpectrumLayout: '{machineInformation.Properties.HasAlternativeFullSpectrumLayout}'");
        }

        // 将采集结果写入静态缓存，避免重复访问 WMI 与注册表。
        return (_machineInformation = machineInformation).Value;
    }

    private static Task<(string, string, string, string)> GetModelDataAsync() => WMI.Win32.ComputerSystemProduct.ReadAsync();

    /// <summary>
    /// 从注册表读取 BIOS 版本字符串，并解析出「4 位前缀 + 2 位版本号」结构。
    /// </summary>
    /// <returns>解析后的 BIOS 版本（失败为 null）与原始字符串（失败为 null）。</returns>
    private static (BiosVersion?, string?) GetBIOSVersion()
    {
        var result = Registry.GetValue("HKEY_LOCAL_MACHINE", "HARDWARE\\DESCRIPTION\\System\\BIOS", "BIOSVersion", string.Empty).Trim();

        var prefixRegex = BiosPrefixRegex();
        var versionRegex = BiosVersionRegex();

        // 分别从前缀与版本号正则中提取匹配片段，再进行数值解析。
        var prefix = prefixRegex.Match(result).Value;
        var versionString = versionRegex.Match(result).Value;

        if (!int.TryParse(versionRegex.Match(versionString).Value, out var version))
            return (null, null);

        return (new(prefix, version), result);
    }

    /// <summary>
    /// 采集特性支持信息：优先使用能力数据表接口，其次回退到特性标志位接口，均失败则返回 Unknown。
    /// </summary>
    private static async Task<MachineInformation.FeatureData> GetFeaturesAsync()
    {
        // 首选：读取标准能力数据表（CapabilityData）。
        try
        {
            var capabilities = await WMI.LenovoCapabilityData00.ReadAsync().ConfigureAwait(false);
            return new(MachineInformation.FeatureData.SourceType.CapabilityData, capabilities);
        }
        catch { /* Ignored. */ }

        // 回退：读取旧机型的 64 位特性标志位，按位映射到各 CapabilityID。
        try
        {
            var featureFlags = await WMI.LenovoOtherMethod.GetLegionDeviceSupportFeatureAsync().ConfigureAwait(false);

            return new(MachineInformation.FeatureData.SourceType.Flags)
            {
                [CapabilityID.IGPUMode] = featureFlags.IsBitSet(0),
                [CapabilityID.NvidiaGPUDynamicDisplaySwitching] = featureFlags.IsBitSet(4),
                [CapabilityID.InstantBootAc] = featureFlags.IsBitSet(5),
                [CapabilityID.InstantBootUsbPowerDelivery] = featureFlags.IsBitSet(6),
                [CapabilityID.AMDSmartShiftMode] = featureFlags.IsBitSet(7),
                [CapabilityID.AMDSkinTemperatureTracking] = featureFlags.IsBitSet(8),
                [CapabilityID.FlipToStart] = true,
                [CapabilityID.OverDrive] = true
            };
        }
        catch { /* Ignored. */ }

        return MachineInformation.FeatureData.Unknown;
    }

    /// <summary>
    /// 读取受支持的电源模式集合：优先按能力位解析，回退到热模式支持位图。
    /// </summary>
    private static async Task<IEnumerable<PowerModeState>> GetSupportedPowerModesAsync()
    {
        // 首选：SupportedPowerModes 能力值，位 0/1/2/16 分别对应安静/均衡/性能/自定义(God)。
        try
        {
            var powerModes = new List<PowerModeState>();

            var value = await WMI.LenovoOtherMethod.GetFeatureValueAsync(CapabilityID.SupportedPowerModes).ConfigureAwait(false);

            if (value.IsBitSet(0))
                powerModes.Add(PowerModeState.Quiet);
            if (value.IsBitSet(1))
                powerModes.Add(PowerModeState.Balance);
            if (value.IsBitSet(2))
                powerModes.Add(PowerModeState.Performance);
            if (value.IsBitSet(16))
                powerModes.Add(PowerModeState.GodMode);

            return powerModes;
        }
        catch { /* Ignored. */ }

        // 回退：读取热模式支持位图，位定义与上一种方式一致。
        try
        {
            var powerModes = new List<PowerModeState>();

            var result = await WMI.LenovoOtherMethod.GetSupportThermalModeAsync().ConfigureAwait(false);

            if (result.IsBitSet(0))
                powerModes.Add(PowerModeState.Quiet);
            if (result.IsBitSet(1))
                powerModes.Add(PowerModeState.Balance);
            if (result.IsBitSet(2))
                powerModes.Add(PowerModeState.Performance);
            if (result.IsBitSet(16))
                powerModes.Add(PowerModeState.GodMode);

            return powerModes;
        }
        catch { /* Ignored. */ }

        return [];
    }

    /// <summary>
    /// 读取 Smart Fan 版本号；接口不可用时返回 -1。
    /// </summary>
    private static async Task<int> GetSmartFanVersionAsync()
    {
        try
        {
            return await WMI.LenovoGameZoneData.IsSupportSmartFanAsync().ConfigureAwait(false);
        }
        catch { /* Ignored. */ }

        return -1;
    }

    /// <summary>
    /// 读取 Legion Zone 支持版本；优先能力值接口，回退到专用查询接口，均失败返回 -1。
    /// </summary>
    private static async Task<int> GetLegionZoneVersionAsync()
    {
        try
        {
            return await WMI.LenovoOtherMethod.GetFeatureValueAsync(CapabilityID.LegionZoneSupportVersion).ConfigureAwait(false);
        }
        catch { /* Ignored. */ }

        try
        {
            return await WMI.LenovoOtherMethod.GetSupportLegionZoneVersionAsync().ConfigureAwait(false);
        }
        catch { /* Ignored. */ }

        return -1;
    }

    /// <summary>
    /// 通过 NtPowerInformation 查询系统是否支持「接通电源时保持唤醒」及其连通性支持情况。
    /// </summary>
    /// <returns>支持状态与连通性支持情况；查询失败时均返回 false。</returns>
    private static unsafe (bool status, bool connectivity) GetAlwaysOnAcStatus()
    {
        var capabilities = new SYSTEM_POWER_CAPABILITIES();
        var result = PInvoke.CallNtPowerInformation(POWER_INFORMATION_LEVEL.SystemPowerCapabilities,
            null,
            0,
            &capabilities,
            (uint)Marshal.SizeOf<SYSTEM_POWER_CAPABILITIES>());

        if (result.SeverityCode == NTSTATUS.Severity.Success)
            return (false, false);

        return (capabilities.AoAc, capabilities.AoAcConnectivitySupported);
    }

    /// <summary>
    /// 判断是否支持第一代「God Mode」（自定义模式），需综合电源模式、Smart Fan/Legion Zone 版本并排除受影响的 BIOS。
    /// </summary>
    private static bool GetSupportsGodModeV1(IEnumerable<PowerModeState> supportedPowerModes, int smartFanVersion, int legionZoneVersion, BiosVersion? biosVersion)
    {
        if (!supportedPowerModes.Contains(PowerModeState.GodMode))
            return false;

        // 这些 BIOS 版本低于阈值时不支持第一代 God Mode。
        var affectedBiosVersions = new BiosVersion[]
        {
            new("G9CN", 24),
            new("GKCN", 46),
            new("H1CN", 39),
            new("HACN", 31),
            new("HHCN", 20)
        };

        if (affectedBiosVersions.Any(bv => biosVersion?.IsLowerThan(bv) ?? false))
            return false;

        return smartFanVersion is 4 or 5 || legionZoneVersion is 1 or 2;
    }

    /// <summary>
    /// 判断是否支持第二代「God Mode」（自定义模式），要求更高的 Smart Fan/Legion Zone 版本。
    /// </summary>
    private static bool GetSupportsGodModeV2(IEnumerable<PowerModeState> supportedPowerModes, int smartFanVersion, int legionZoneVersion)
    {
        if (!supportedPowerModes.Contains(PowerModeState.GodMode))
            return false;

        return smartFanVersion is 6 or 7 || legionZoneVersion is 3 or 4;
    }

    /// <summary>
    /// 查询是否支持 G-Sync；接口不可用时返回 false。
    /// </summary>
    private static async Task<bool> GetSupportsGSyncAsync()
    {
        try
        {
            return await WMI.LenovoGameZoneData.IsSupportGSyncAsync().ConfigureAwait(false) > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 查询是否支持核显模式（iGPU Only）；接口不可用时返回 false。
    /// </summary>
    private static async Task<bool> GetSupportsIGPUModeAsync()
    {
        try
        {
            return await WMI.LenovoGameZoneData.IsSupportIGPUModeAsync().ConfigureAwait(false) > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 通过能否读取智能子模式来间接判断是否支持 AI 模式；接口不可用时返回 false。
    /// </summary>
    private static async Task<bool> GetSupportsAIModeAsync()
    {
        try
        {
            await WMI.LenovoGameZoneData.GetIntelligentSubModeAsync().ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Smart Fan 版本低于 8 的机型支持更换开机 Logo。
    private static bool GetSupportBootLogoChange(int smartFanVersion) => smartFanVersion < 8;

    /// <summary>
    /// 判断是否存在「安静模式切换到性能模式」的已知 BIOS 缺陷。
    /// </summary>
    private static bool GetHasQuietToPerformanceModeSwitchingBug(BiosVersion? biosVersion)
    {
        var affectedBiosVersions = new BiosVersion[]
        {
            new("J2CN", null)
        };

        return affectedBiosVersions.Any(bv => biosVersion?.IsHigherOrEqualThan(bv) ?? false);
    }

    /// <summary>
    /// 判断是否存在「God Mode 切换到其他模式」的已知 BIOS 缺陷。
    /// </summary>
    private static bool GetHasGodModeToOtherModeSwitchingBug(BiosVersion? biosVersion)
    {
        var affectedBiosVersions = new BiosVersion[]
        {
            new("K1CN", null)
        };

        return affectedBiosVersions.Any(bv => biosVersion?.IsHigherOrEqualThan(bv) ?? false);
    }

    /// <summary>
    /// 判断该 BIOS 版本是否应排除在 Lenovo 灯效控制之外（低于受影响阈值）。
    /// </summary>
    private static bool GetIsExcludedFromLenovoLighting(BiosVersion? biosVersion)
    {
        var affectedBiosVersions = new BiosVersion[]
        {
            new("GKCN", 54)
        };

        return affectedBiosVersions.Any(bv => biosVersion?.IsLowerThan(bv) ?? false);
    }

    /// <summary>
    /// 判断该机型（机型码 + 型号）是否应排除在「屏幕/Lid Logo 灯效」之外。
    /// </summary>
    private static bool GetIsExcludedFromPanelLenovoLighting(string machineType, string model)
    {
        // 机型码与型号需同时匹配时，才视为需要排除的特定机型组合。
        (string machineType, string model)[] excludedModels =
        [
            ("82JH", "15ITH6H"),
            ("82JK", "15ITH6"),
            ("82JM", "17ITH6H"),
            ("82JN", "17ITH6"),
            ("82JU", "15ACH6H"),
            ("82JW", "15ACH6"),
            ("82JY", "17ACH6H"),
            ("82K0", "17ACH6"),
            ("82K1", "15IHU6"),
            ("82K2", "15ACH6"),
            ("82NW", "15ACH6A")
        ];

        return excludedModels.Where(m =>
        {
            var result = machineType.Contains(m.machineType);
            result &= model.Contains(m.model);
            return result;
        }).Any();
    }

    /// <summary>
    /// 判断该机型是否使用替代的全光谱（per-key）灯效布局。
    /// </summary>
    private static bool GetHasAlternativeFullSpectrumLayout(string machineType)
    {
        var machineTypes = new[]
        {
            "83G0", // Gen 9
            "83AG"  // Gen 8
        };
        return machineTypes.Contains(machineType);
    }
}

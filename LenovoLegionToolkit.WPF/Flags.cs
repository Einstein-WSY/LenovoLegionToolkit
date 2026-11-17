using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LenovoLegionToolkit.Lib.Utils;

// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.WPF;

/// <summary>
/// 启动标志集合，由命令行参数（以及 AppData 下的 args.txt）解析而来，
/// 用于开启调试、跳过兼容性检查、禁用特定功能等。
/// </summary>
public class Flags
{
    /// <summary>是否启用详细（Trace）日志。</summary>
    public bool IsTraceEnabled { get; }
    /// <summary>启动后是否直接最小化到托盘。</summary>
    public bool Minimized { get; }
    /// <summary>是否跳过机型兼容性检查。</summary>
    public bool SkipCompatibilityCheck { get; }
    /// <summary>是否禁用托盘图标提示气泡。</summary>
    public bool DisableTrayTooltip { get; }
    /// <summary>是否允许在电池供电时使用所有电源模式。</summary>
    public bool AllowAllPowerModesOnBattery { get; }
    /// <summary>是否强制禁用 RGB 键盘支持。</summary>
    public bool ForceDisableRgbKeyboardSupport { get; }
    /// <summary>是否强制禁用 Spectrum 键盘支持。</summary>
    public bool ForceDisableSpectrumKeyboardSupport { get; }
    /// <summary>是否强制禁用 Lenovo Lighting 灯效支持。</summary>
    public bool ForceDisableLenovoLighting { get; }
    /// <summary>是否启用实验性的 GPU 工作模式。</summary>
    public bool ExperimentalGPUWorkingMode { get; }
    /// <summary>是否启用混合模式（Hybrid Mode）自动化。</summary>
    public bool EnableHybridModeAutomation { get; }
    /// <summary>代理服务器地址，未设置时为 <c>null</c>。</summary>
    public Uri? ProxyUrl { get; }
    /// <summary>代理用户名。</summary>
    public string? ProxyUsername { get; }
    /// <summary>代理密码。</summary>
    public string? ProxyPassword { get; }
    /// <summary>代理是否接受所有证书（忽略证书校验）。</summary>
    public bool ProxyAllowAllCerts { get; }
    /// <summary>是否禁用冲突软件警告。</summary>
    public bool DisableConflictingSoftwareWarning { get; }

    /// <summary>
    /// 解析启动参数并初始化各标志。
    /// </summary>
    /// <param name="startupArgs">来自命令行的启动参数。</param>
    public Flags(IEnumerable<string> startupArgs)
    {
        // 合并命令行参数与外部 args.txt 中的参数
        var args = startupArgs.Concat(LoadExternalArgs()).ToArray();

        IsTraceEnabled = BoolValue(args, "--trace");
        Minimized = BoolValue(args, "--minimized");
        SkipCompatibilityCheck = BoolValue(args, "--skip-compat-check");
        DisableTrayTooltip = BoolValue(args, "--disable-tray-tooltip");
        AllowAllPowerModesOnBattery = BoolValue(args, "--allow-all-power-modes-on-battery");
        ForceDisableRgbKeyboardSupport = BoolValue(args, "--force-disable-rgbkb");
        ForceDisableSpectrumKeyboardSupport = BoolValue(args, "--force-disable-spectrumkb");
        ForceDisableLenovoLighting = BoolValue(args, "--force-disable-lenovolighting");
        ExperimentalGPUWorkingMode = BoolValue(args, "--experimental-gpu-working-mode");
        EnableHybridModeAutomation = BoolValue(args, "--enable-hybrid-mode-automation");
        ProxyUrl = Uri.TryCreate(StringValue(args, "--proxy-url"), UriKind.Absolute, out var uri) ? uri : null;
        ProxyUsername = StringValue(args, "--proxy-username");
        ProxyPassword = StringValue(args, "--proxy-password");
        ProxyAllowAllCerts = BoolValue(args, "--proxy-allow-all-certs");
        DisableConflictingSoftwareWarning = BoolValue(args, "--disable-conflicting-software-warning");
    }

    /// <summary>
    /// 从 AppData 目录下的 args.txt 读取额外参数，文件不存在或读取异常时返回空数组。
    /// </summary>
    /// <returns>参数行数组。</returns>
    private static string[] LoadExternalArgs()
    {
        try
        {
            var argsFile = Path.Combine(Folders.AppData, "args.txt");
            return !File.Exists(argsFile) ? [] : File.ReadAllLines(argsFile);
        }
        catch
        {
            // 读取失败时静默忽略，不影响启动
            return [];
        }
    }

    /// <summary>判断参数集合中是否包含指定的布尔开关。</summary>
    /// <param name="values">参数集合。</param>
    /// <param name="key">开关名。</param>
    /// <returns>包含则返回 <c>true</c>。</returns>
    private static bool BoolValue(IEnumerable<string> values, string key) => values.Contains(key);

    /// <summary>
    /// 读取形如 <c>--key value</c> 的字符串参数值。
    /// </summary>
    /// <param name="values">参数集合。</param>
    /// <param name="key">参数名（含前缀）。</param>
    /// <returns>去掉“前缀+分隔符”后的值；未找到时返回 <c>null</c>。</returns>
    private static string? StringValue(IEnumerable<string> values, string key)
    {
        var value = values.FirstOrDefault(s => s.StartsWith(key));
        return value?.Remove(0, key.Length + 1);
    }

    /// <summary>输出所有标志键值，便于日志排查。</summary>
    /// <returns>标志的字符串表示。</returns>
    public override string ToString() =>
        $"{nameof(IsTraceEnabled)}: {IsTraceEnabled}," +
        $" {nameof(Minimized)}: {Minimized}," +
        $" {nameof(SkipCompatibilityCheck)}: {SkipCompatibilityCheck}," +
        $" {nameof(DisableTrayTooltip)}: {DisableTrayTooltip}," +
        $" {nameof(AllowAllPowerModesOnBattery)}: {AllowAllPowerModesOnBattery}," +
        $" {nameof(ForceDisableRgbKeyboardSupport)}: {ForceDisableRgbKeyboardSupport}," +
        $" {nameof(ForceDisableSpectrumKeyboardSupport)}: {ForceDisableSpectrumKeyboardSupport}," +
        $" {nameof(ForceDisableLenovoLighting)}: {ForceDisableLenovoLighting}," +
        $" {nameof(ExperimentalGPUWorkingMode)}: {ExperimentalGPUWorkingMode}," +
        $" {nameof(EnableHybridModeAutomation)}: {EnableHybridModeAutomation}," +
        $" {nameof(ProxyUrl)}: {ProxyUrl}," +
        $" {nameof(ProxyUsername)}: {ProxyUsername}," +
        $" {nameof(ProxyPassword)}: {ProxyPassword}," +
        $" {nameof(ProxyAllowAllCerts)}: {ProxyAllowAllCerts}," +
        $" {nameof(DisableConflictingSoftwareWarning)}: {DisableConflictingSoftwareWarning}";
}

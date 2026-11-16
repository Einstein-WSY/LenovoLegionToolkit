namespace LenovoLegionToolkit.CLI.Lib;

/// <summary>
/// 通过命名管道发送给主程序的请求。
/// </summary>
public class IpcRequest
{
    /// <summary>
    /// 请求的操作类型，主程序据此分发到对应的处理逻辑。
    /// </summary>
    public enum OperationType
    {
        /// <summary>未知操作，主程序会拒绝处理。</summary>
        Unknown,
        /// <summary>列出所有可用功能。</summary>
        ListFeatures,
        /// <summary>列出某个功能所有可选的值。</summary>
        ListFeatureValues,
        /// <summary>列出所有快捷操作（Quick Action）。</summary>
        ListQuickActions,
        /// <summary>获取功能当前的值。</summary>
        GetFeatureValue,
        /// <summary>设置功能的值。</summary>
        SetFeatureValue,
        /// <summary>获取 Spectrum 灯效配置。</summary>
        GetSpectrumProfile,
        /// <summary>设置 Spectrum 灯效配置。</summary>
        SetSpectrumProfile,
        /// <summary>获取 Spectrum 亮度。</summary>
        GetSpectrumBrightness,
        /// <summary>设置 Spectrum 亮度。</summary>
        SetSpectrumBrightness,
        /// <summary>获取 RGB 灯效预设。</summary>
        GetRGBPreset,
        /// <summary>设置 RGB 灯效预设。</summary>
        SetRGBPreset,
        /// <summary>执行指定的快捷操作。</summary>
        QuickAction,
    }

    /// <summary>要执行的操作类型。</summary>
    public OperationType? Operation { get; init; }

    /// <summary>操作对象名称（功能名或快捷操作名）。</summary>
    public string? Name { get; init; }

    /// <summary>要设置的值。</summary>
    public string? Value { get; init; }
}

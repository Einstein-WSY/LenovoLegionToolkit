using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Features.Hybrid;

/// <summary>
/// iGPU 模式组合特性，负责在三套底层实现中选出可用的一套：
/// <list type="number">
/// <item><description><c>feature1</c>：GameZone WMI 实现（老机型，稳定）。</description></item>
/// <item><description><c>feature2</c>：CapabilityData 实现（新机型）。</description></item>
/// <item><description><c>feature3</c>：FeatureFlags 实现（实验性）。</description></item>
/// </list>
/// 默认（非实验模式）只使用稳定的 GameZone 实现；开启实验模式后才回退到后两者。
/// </summary>
public class IGPUModeFeature(IGPUModeGamezoneFeature feature1, IGPUModeCapabilityFeature feature2, IGPUModeFeatureFlagsFeature feature3)
    : AbstractCompositeFeature<IGPUModeState>(feature1, feature2, feature3)
{
    /// <summary>
    /// 是否启用实验性的 GPU 工作模式实现（CapabilityData / FeatureFlags）。
    /// 为 <c>false</c> 时仅使用 GameZone WMI 实现。
    /// </summary>
    public bool ExperimentalGPUWorkingMode { get; set; }

    /// <inheritdoc/>
    protected override async Task<IFeature<IGPUModeState>?> ResolveAsync()
    {
        // 非实验模式：只接受 GameZone 实现，不支持则视为整体不支持
        if (!ExperimentalGPUWorkingMode)
            return await feature1.IsSupportedAsync().ConfigureAwait(false) ? feature1 : null;

        // 实验模式：优先 CapabilityData，再退到 FeatureFlags
        if (await feature2.IsSupportedAsync().ConfigureAwait(false))
            return feature2;

        if (await feature3.IsSupportedAsync().ConfigureAwait(false))
            return feature3;

        return null;

    }
}

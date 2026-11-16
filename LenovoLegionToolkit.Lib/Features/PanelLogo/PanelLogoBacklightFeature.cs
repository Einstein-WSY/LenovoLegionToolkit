namespace LenovoLegionToolkit.Lib.Features.PanelLogo;

/// <summary>
/// 顶盖 Logo 背光组合特性。
/// 优先使用 Lenovo Lighting 实现，不支持时回退到 Spectrum（光谱键盘）实现。
/// </summary>
/// <param name="feature1">基于 Lenovo Lighting 的实现（优先级较高）。</param>
/// <param name="feature2">基于 Spectrum 控制器的实现（回退方案）。</param>
public class PanelLogoBacklightFeature(PanelLogoLenovoLightingBacklightFeature feature1, PanelLogoSpectrumBacklightFeature feature2)
    : AbstractCompositeFeature<PanelLogoBacklightState>(feature1, feature2);

using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;

namespace LenovoLegionToolkit.Lib.Features.PanelLogo;

/// <summary>
/// 顶盖 Logo 背光特性 —— 基于 Spectrum（光谱）键盘控制器实现。
/// 仅当键盘布局为 Full（全尺寸）时才具备独立 Logo 灯控制能力。
/// </summary>
/// <param name="controller">Spectrum 键盘背光控制器。</param>
public class PanelLogoSpectrumBacklightFeature(SpectrumKeyboardBacklightController controller) : IFeature<PanelLogoBacklightState>
{
    /// <inheritdoc/>
    public async Task<bool> IsSupportedAsync()
    {
        var isSupported = await controller.IsSupportedAsync().ConfigureAwait(false);
        if (!isSupported)
            return false;

        // 只有全尺寸布局才有 Logo 灯区域
        var (layout, _, _) = await controller.GetKeyboardLayoutAsync().ConfigureAwait(false);
        return layout == SpectrumLayout.Full;
    }

    /// <inheritdoc/>
    public Task<PanelLogoBacklightState[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<PanelLogoBacklightState>());

    /// <inheritdoc/>
    public async Task<PanelLogoBacklightState> GetStateAsync() => await controller.GetLogoStatusAsync().ConfigureAwait(false)
        ? PanelLogoBacklightState.On
        : PanelLogoBacklightState.Off;

    /// <inheritdoc/>
    public Task SetStateAsync(PanelLogoBacklightState state) => controller.SetLogoStatusAsync(state == PanelLogoBacklightState.On);
}

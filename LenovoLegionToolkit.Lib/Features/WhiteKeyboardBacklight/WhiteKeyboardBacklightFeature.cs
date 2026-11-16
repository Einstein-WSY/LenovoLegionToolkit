using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;

namespace LenovoLegionToolkit.Lib.Features.WhiteKeyboardBacklight;

/// <summary>
/// 白色键盘背光组合特性。
/// 优先使用 Lenovo Lighting 实现，其次使用驱动实现；
/// 但如果机器已支持 Spectrum / RGB 彩色键盘，则白色背光不适用，直接判定为不支持。
/// </summary>
/// <param name="feature1">基于 Lenovo Lighting 的实现。</param>
/// <param name="feature2">基于能量驱动 IOCTL 的实现。</param>
/// <param name="spectrumController">Spectrum 键盘控制器，用于检测彩色键盘是否独占。</param>
/// <param name="rgbController">RGB 键盘控制器，用于检测彩色键盘是否独占。</param>
public class WhiteKeyboardBacklightFeature(WhiteKeyboardLenovoLightingBacklightFeature feature1,
    WhiteKeyboardDriverBacklightFeature feature2,
    SpectrumKeyboardBacklightController spectrumController,
    RGBKeyboardBacklightController rgbController)
    : AbstractCompositeFeature<WhiteKeyboardBacklightState>(feature1, feature2)
{
    /// <inheritdoc/>
    protected override async Task<IFeature<WhiteKeyboardBacklightState>?> ResolveAsync()
    {
        // 存在彩色键盘控制器时，白色背光特性无意义，返回 null 表示不支持
        if (await spectrumController.IsSupportedAsync().ConfigureAwait(false) || await rgbController.IsSupportedAsync().ConfigureAwait(false))
            return null;

        return await base.ResolveAsync().ConfigureAwait(false);
    }
}

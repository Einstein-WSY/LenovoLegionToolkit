using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features.PanelLogo;

/// <summary>
/// 顶盖 Logo 背光特性 —— 基于 Lenovo Lighting 接口实现。
/// 灯效 ID 为 3、控制接口 1、类型 0；仅开关，无亮度等级。
/// </summary>
public class PanelLogoLenovoLightingBacklightFeature() : AbstractLenovoLightingFeature<PanelLogoBacklightState>(3, 1, 0)
{
    /// <inheritdoc/>
    public override async Task<bool> IsSupportedAsync()
    {
        var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
        // 部分机型虽然暴露了该灯效接口，但实际不可控，需要按机型排除
        if (mi.Properties.IsExcludedFromPanelLogoLenovoLighting)
            return false;

        return await base.IsSupportedAsync().ConfigureAwait(false);
    }

    // 状态类型直接就是枚举值，忽略亮度等级
    protected override PanelLogoBacklightState FromInternal(int stateType, int _) => (PanelLogoBacklightState)stateType;

    // 写入时亮度等级固定为 0
    protected override (int stateType, int level) ToInternal(PanelLogoBacklightState state) => ((int)state, 0);
}

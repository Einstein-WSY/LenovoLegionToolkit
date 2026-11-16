namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// 机身侧边接口（USB 等端口）背光特性。
/// 基于 Lenovo Lighting 接口实现，灯效 ID 为 5、控制接口 1、类型 0。
/// </summary>
public class PortsBacklightFeature() : AbstractLenovoLightingFeature<PortsBacklightState>(5, 1, 0)
{
    // 端口背光只有开/关，状态类型直接就是枚举值，忽略亮度等级
    protected override PortsBacklightState FromInternal(int stateType, int _) => (PortsBacklightState)stateType;

    // 写入时亮度等级固定为 0
    protected override (int stateType, int level) ToInternal(PortsBacklightState state) => ((int)state, 0);
}

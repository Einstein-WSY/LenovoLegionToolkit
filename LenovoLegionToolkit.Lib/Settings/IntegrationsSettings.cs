namespace LenovoLegionToolkit.Lib.Settings;

/// <summary>
/// 外部集成设置：持久化到 integrations.json，控制与第三方工具（HWiNFO、命令行接口）的集成开关。
/// </summary>
public class IntegrationsSettings()
    : AbstractSettings<IntegrationsSettings.IntegrationsSettingsStore>("integrations.json")
{
    /// <summary>
    /// integrations.json 的数据结构。
    /// </summary>
    public class IntegrationsSettingsStore
    {
        /// <summary>
        /// 是否启用 HWiNFO 集成（向其写入传感器数据）。
        /// </summary>
        public bool HWiNFO { get; set; }

        /// <summary>
        /// 是否启用命令行接口（CLI）集成。
        /// </summary>
        public bool CLI { get; set; }
    }
}

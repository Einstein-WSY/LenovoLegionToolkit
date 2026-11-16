using static LenovoLegionToolkit.Lib.Settings.BalanceModeSettings;

namespace LenovoLegionToolkit.Lib.Settings;

/// <summary>
/// 均衡模式设置：持久化到 balancemode.json，仅保存均衡模式的额外 AI 相关开关。
/// </summary>
public class BalanceModeSettings() : AbstractSettings<BalanceModeSettingsStore>("balancemode.json")
{
    /// <summary>
    /// balancemode.json 的数据结构。
    /// </summary>
    public class BalanceModeSettingsStore
    {
        /// <summary>
        /// 均衡模式下是否启用 AI 模式，默认关闭。
        /// </summary>
        public bool AIModeEnabled { get; set; }
    }

    // ReSharper disable once StringLiteralTypo
}

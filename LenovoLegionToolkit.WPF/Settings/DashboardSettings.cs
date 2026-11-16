using LenovoLegionToolkit.Lib.Settings;

namespace LenovoLegionToolkit.WPF.Settings;

/// <summary>
/// 仪表盘页面的持久化设置，保存到 dashboard.json。
/// </summary>
public class DashboardSettings() : AbstractSettings<DashboardSettings.DashboardSettingsStore>("dashboard.json")
{
    /// <summary>
    /// 仪表盘设置的数据结构。
    /// </summary>
    public class DashboardSettingsStore
    {
        /// <summary>是否显示传感器面板。</summary>
        public bool ShowSensors { get; set; } = true;
        /// <summary>传感器刷新间隔（秒）。</summary>
        public int SensorsRefreshIntervalSeconds { get; set; } = 1;
        /// <summary>用户自定义的分组配置；为 <c>null</c> 时使用默认分组。</summary>
        public DashboardGroup[]? Groups { get; set; }
    }

    /// <summary>提供默认设置值。</summary>
    protected override DashboardSettingsStore Default => new();
}

using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// 即时开机（Instant Boot）仪表盘卡片控件，以下拉框配置开机引导行为。
/// </summary>
public class InstantBootControl : AbstractComboBoxFeatureCardControl<InstantBootState>
{
    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题、副标题。
    /// </summary>
    public InstantBootControl()
    {
        Icon = SymbolRegular.PlugDisconnected24;
        Title = Resource.InstantBootControl_Title;
        Subtitle = Resource.InstantBootControl_Message;
    }
}

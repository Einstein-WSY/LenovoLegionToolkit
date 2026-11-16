using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// 电池模式仪表盘卡片控件，以下拉框形式在节能/普通/养护等电池模式之间切换。
/// </summary>
public class BatteryModeControl : AbstractComboBoxFeatureCardControl<BatteryState>
{
    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题、副标题。
    /// </summary>
    public BatteryModeControl()
    {
        Icon = SymbolRegular.BatteryCharge24;
        Title = Resource.BatteryModeControl_Title;
        Subtitle = Resource.BatteryModeControl_Message;
    }
}

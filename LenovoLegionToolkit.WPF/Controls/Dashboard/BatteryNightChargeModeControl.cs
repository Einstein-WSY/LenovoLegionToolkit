using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “夜间充电模式”仪表盘开关卡片控件，开启后夜间降低充电上限以保护电池。
/// </summary>
public class BatteryNightChargeModeControl : AbstractToggleFeatureCardControl<BatteryNightChargeState>
{
    /// <summary>开关处于“开”状态时对应的固件枚举值。</summary>
    protected override BatteryNightChargeState OnState => BatteryNightChargeState.On;
    /// <summary>开关处于“关”状态时对应的固件枚举值。</summary>
    protected override BatteryNightChargeState OffState => BatteryNightChargeState.Off;

    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题、副标题。
    /// </summary>
    public BatteryNightChargeModeControl()
    {
        Icon = SymbolRegular.WeatherMoon24;
        Title = Resource.BatteryNightChargeModeControl_Title;
        Subtitle = Resource.BatteryNightChargeModeControl_Message;
    }
}

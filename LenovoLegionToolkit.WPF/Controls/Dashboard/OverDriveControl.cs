using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “屏幕超频（Over Drive）”仪表盘开关卡片控件，用于提升屏幕响应速度。
/// </summary>
public class OverDriveControl : AbstractToggleFeatureCardControl<OverDriveState>
{
    /// <summary>开关处于“开”状态时对应的固件枚举值。</summary>
    protected override OverDriveState OnState => OverDriveState.On;

    /// <summary>开关处于“关”状态时对应的固件枚举值。</summary>
    protected override OverDriveState OffState => OverDriveState.Off;

    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题、副标题。
    /// </summary>
    public OverDriveControl()
    {
        Icon = SymbolRegular.TopSpeed24;
        Title = Resource.OverDriveControl_Title;
        Subtitle = Resource.OverDriveControl_Message;
    }
}

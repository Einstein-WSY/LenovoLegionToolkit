using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “翻盖开机”仪表盘开关卡片控件，控制合盖后再次打开时是否自动开机。
/// </summary>
public class FlipToStartControl : AbstractToggleFeatureCardControl<FlipToStartState>
{
    /// <summary>开关处于“开”状态时对应的固件枚举值。</summary>
    protected override FlipToStartState OnState => FlipToStartState.On;

    /// <summary>开关处于“关”状态时对应的固件枚举值。</summary>
    protected override FlipToStartState OffState => FlipToStartState.Off;

    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题、副标题。
    /// </summary>
    public FlipToStartControl()
    {
        Icon = SymbolRegular.Power24;
        Title = Resource.FlipToStartControl_Title;
        Subtitle = Resource.FlipToStartControl_Message;
    }
}

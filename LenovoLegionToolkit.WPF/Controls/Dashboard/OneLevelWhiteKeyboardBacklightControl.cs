using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “一级白色键盘背光”仪表盘开关卡片控件，用于仅支持单档白色的键盘背光机型。
/// </summary>
internal class OneLevelWhiteKeyboardBacklightControl : AbstractToggleFeatureCardControl<OneLevelWhiteKeyboardBacklightState>
{
    /// <summary>开关处于“开”状态时对应的固件枚举值。</summary>
    protected override OneLevelWhiteKeyboardBacklightState OnState => OneLevelWhiteKeyboardBacklightState.On;

    /// <summary>开关处于“关”状态时对应的固件枚举值。</summary>
    protected override OneLevelWhiteKeyboardBacklightState OffState => OneLevelWhiteKeyboardBacklightState.Off;

    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题、副标题。
    /// </summary>
    public OneLevelWhiteKeyboardBacklightControl()
    {
        Icon = SymbolRegular.Keyboard24;
        Title = Resource.OneLevelWhiteKeyboardBacklightControl_Title;
        Subtitle = Resource.OneLevelWhiteKeyboardBacklightControl_Message;
    }
}

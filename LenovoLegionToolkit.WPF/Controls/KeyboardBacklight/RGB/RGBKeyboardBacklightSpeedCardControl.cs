using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.RGB;

/// <summary>
/// RGB 键盘背光“速度”下拉卡片控件，枚举灯效动画的播放速度档位。
/// </summary>
public class RGBKeyboardBacklightSpeedCardControl : AbstractComboBoxRGBKeyboardCardControl<RGBKeyboardBacklightSpeed>
{
    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题。
    /// </summary>
    public RGBKeyboardBacklightSpeedCardControl()
    {
        Icon = SymbolRegular.Keyboard24;
        Title = Resource.RGBKeyboardBacklightSpeedCardControl_Title;
    }
}

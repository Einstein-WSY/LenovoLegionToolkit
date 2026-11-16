using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.RGB;

/// <summary>
/// RGB 键盘背光“亮度”下拉卡片控件，枚举可选的亮度档位。
/// </summary>
public class RGBKeyboardBacklightBrightnessCardControl : AbstractComboBoxRGBKeyboardCardControl<RGBKeyboardBacklightBrightness>
{
    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题。
    /// </summary>
    public RGBKeyboardBacklightBrightnessCardControl()
    {
        Icon = SymbolRegular.Keyboard24;
        Title = Resource.RGBKeyboardBacklightBrightnessCardControl_Brightness;
    }
}

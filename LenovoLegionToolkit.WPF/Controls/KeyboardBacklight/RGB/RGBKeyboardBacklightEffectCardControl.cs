using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.RGB;

/// <summary>
/// RGB 键盘背光“灯效”下拉卡片控件，枚举可选的动态灯效模式。
/// </summary>
public class RGBKeyboardBacklightEffectCardControl : AbstractComboBoxRGBKeyboardCardControl<RGBKeyboardBacklightEffect>
{
    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题。
    /// </summary>
    public RGBKeyboardBacklightEffectCardControl()
    {
        Icon = SymbolRegular.Keyboard24;
        Title = Resource.RGBKeyboardBacklightEffectCardControl_Title;
    }
}

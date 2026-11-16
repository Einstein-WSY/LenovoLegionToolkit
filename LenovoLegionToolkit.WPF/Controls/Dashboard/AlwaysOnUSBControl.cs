using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “始终开启 USB 供电”仪表盘卡片控件，绑定开关状态，值变更后由基类写回固件。
/// </summary>
public class AlwaysOnUSBControl : AbstractComboBoxFeatureCardControl<AlwaysOnUSBState>
{
    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题、副标题。
    /// </summary>
    public AlwaysOnUSBControl()
    {
        Icon = SymbolRegular.UsbStick24;
        Title = Resource.AlwaysOnUSBControl_Title;
        Subtitle = Resource.AlwaysOnUSBControl_Message;
    }
}

using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “白色键盘背光”仪表盘卡片控件，展示并切换白色背光的开关/档位。
/// 通过驱动键监听器感知 Fn+空格 快捷键变化后自动刷新。
/// </summary>
public class WhiteKeyboardBacklightControl : AbstractComboBoxFeatureCardControl<WhiteKeyboardBacklightState>
{
    /// <summary>驱动键监听器，用于捕获键盘快捷键触发的背光变化。</summary>
    private readonly DriverKeyListener _listener = IoCContainer.Resolve<DriverKeyListener>();

    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题、副标题，并订阅驱动键变化事件。
    /// </summary>
    public WhiteKeyboardBacklightControl()
    {
        Icon = SymbolRegular.Keyboard24;
        Title = Resource.WhiteKeyboardBacklightControl_Title;
        Subtitle = Resource.WhiteKeyboardBacklightControl_Message;

        _listener.Changed += ListenerChanged;
    }

    /// <summary>
    /// 驱动键变化回调：仅在控件可见时响应 Fn+空格，切回 UI 线程重新读取硬件状态。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">包含触发按键的参数。</param>
    private void ListenerChanged(object? sender, DriverKeyListener.ChangedEventArgs e) => Dispatcher.Invoke(async () =>
    {
        if (!IsLoaded || !IsVisible)
            return;

        // 只有 Fn+空格 影响键盘背光，其余按键忽略以避免无谓刷新。
        if (e.DriverKey.HasFlag(DriverKey.FnSpace))
            await RefreshAsync();
    });
}

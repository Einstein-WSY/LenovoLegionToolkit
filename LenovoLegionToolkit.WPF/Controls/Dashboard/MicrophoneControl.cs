using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “麦克风”仪表盘开关卡片控件，展示并切换麦克风静音状态；
/// 通过驱动键监听器感知 Fn+F4 快捷键变化后自动刷新。
/// </summary>
public class MicrophoneControl : AbstractToggleFeatureCardControl<MicrophoneState>
{
    /// <summary>驱动键监听器，用于捕获键盘快捷键触发的麦克风状态变化。</summary>
    private readonly DriverKeyListener _listener = IoCContainer.Resolve<DriverKeyListener>();

    /// <summary>开关处于“开”状态时对应的固件枚举值。</summary>
    protected override MicrophoneState OnState => MicrophoneState.On;
    /// <summary>开关处于“关”状态时对应的固件枚举值。</summary>
    protected override MicrophoneState OffState => MicrophoneState.Off;

    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题、副标题，并订阅驱动键变化事件。
    /// </summary>
    public MicrophoneControl()
    {
        Icon = SymbolRegular.Mic24;
        Title = Resource.MicrophoneControl_Title;
        Subtitle = Resource.MicrophoneControl_Message;

        _listener.Changed += Listener_Changed;
    }

    /// <summary>
    /// 驱动键变化回调：仅在控件可见时响应 Fn+F4，切回 UI 线程重新读取硬件状态。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">包含触发按键的参数。</param>
    private void Listener_Changed(object? sender, DriverKeyListener.ChangedEventArgs e) => Dispatcher.Invoke(async () =>
    {
        if (!IsLoaded || !IsVisible)
            return;

        // 只有 Fn+F4 影响麦克风开关，其余按键忽略以避免无谓刷新。
        if (e.DriverKey.HasFlag(DriverKey.FnF4))
            await RefreshAsync();
    });
}

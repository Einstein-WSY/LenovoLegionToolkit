using System.Threading.Tasks;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “触摸板锁定”仪表盘开关卡片控件，展示并切换触摸板禁用状态；
/// 通过驱动键监听器感知 Fn+F10 触发的变化后自动刷新。
/// </summary>
public class TouchpadLockControl : AbstractToggleFeatureCardControl<TouchpadLockState>
{
    /// <summary>驱动键监听器，用于捕获 Fn+F10 触摸板锁定快捷键的变化。</summary>
    private readonly DriverKeyListener _listener = IoCContainer.Resolve<DriverKeyListener>();

    /// <summary>开关处于“开”状态时对应的固件枚举值。</summary>
    protected override TouchpadLockState OnState => TouchpadLockState.On;

    /// <summary>开关处于“关”状态时对应的固件枚举值。</summary>
    protected override TouchpadLockState OffState => TouchpadLockState.Off;

    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题、副标题，并订阅驱动键变化事件。
    /// </summary>
    public TouchpadLockControl()
    {
        Icon = SymbolRegular.Tablet24;
        Title = Resource.TouchpadLockControl_Title;
        Subtitle = Resource.TouchpadLockControl_Message;

        _listener.Changed += Listener_Changed;
    }

    /// <summary>
    /// 处理开关状态切换：切换前后暂停并重启驱动键监听器，避免自身写入触发误刷新。
    /// </summary>
    /// <param name="toggle">触发切换的开关控件。</param>
    /// <param name="feature">对应的触摸板锁定功能实现。</param>
    /// <returns>异步操作任务。</returns>
    protected override async Task OnStateChange(ToggleSwitch toggle, IFeature<TouchpadLockState> feature)
    {
        // 写固件期间暂停监听，防止回读事件与自身操作互相干扰。
        await _listener.StopAsync();
        await base.OnStateChange(toggle, feature);
        await _listener.StartAsync();
    }

    /// <summary>
    /// 驱动键变化回调：仅在控件可见时响应 Fn+F10，切回 UI 线程刷新状态。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">包含触发按键的参数。</param>
    private void Listener_Changed(object? sender, DriverKeyListener.ChangedEventArgs e) => Dispatcher.Invoke(async () =>
    {
        if (!IsLoaded || !IsVisible)
            return;

        // 只有 Fn+F10 影响触摸板锁定，其他按键忽略。
        if (e.DriverKey.HasFlag(DriverKey.FnF10))
            await RefreshAsync();
    });
}

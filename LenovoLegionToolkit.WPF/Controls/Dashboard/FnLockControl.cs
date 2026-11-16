using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “Fn 锁定”仪表盘开关卡片控件，展示并切换 Fn 键锁定状态；
/// 通过特殊键监听器感知 Fn+Esc 触发的变化后自动刷新。
/// </summary>
public class FnLockControl : AbstractToggleFeatureCardControl<FnLockState>
{
    /// <summary>特殊键监听器，用于捕获 Fn 锁定快捷键的状态变化。</summary>
    private readonly SpecialKeyListener _listener = IoCContainer.Resolve<SpecialKeyListener>();

    /// <summary>开关处于“开”状态时对应的固件枚举值。</summary>
    protected override FnLockState OnState => FnLockState.On;

    /// <summary>开关处于“关”状态时对应的固件枚举值。</summary>
    protected override FnLockState OffState => FnLockState.Off;

    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题、副标题，并订阅特殊键变化事件。
    /// </summary>
    public FnLockControl()
    {
        Icon = SymbolRegular.Keyboard24;
        Title = Resource.FnLockControl_Title;
        Subtitle = Resource.FnLockControl_Message;

        _listener.Changed += Listener_Changed;
    }

    /// <summary>
    /// 特殊键变化回调：仅在控件可见时响应 Fn 锁定开/关，切回 UI 线程刷新状态。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">包含触发按键的参数。</param>
    private void Listener_Changed(object? sender, SpecialKeyListener.ChangedEventArgs e) => Dispatcher.Invoke(async () =>
    {
        if (!IsLoaded || !IsVisible)
            return;

        // 只响应 Fn 锁定的开与关两种按键，其他特殊键忽略。
        if (e.SpecialKey is SpecialKey.FnLockOn or SpecialKey.FnLockOff)
            await RefreshAsync();
    });
}

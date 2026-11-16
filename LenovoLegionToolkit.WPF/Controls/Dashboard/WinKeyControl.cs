using System;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “Win 键锁定”仪表盘开关卡片控件，展示并切换 Windows 键是否被禁用；
/// 通过 Win 键监听器感知状态变化后自动刷新。
/// </summary>
public class WinKeyControl : AbstractToggleFeatureCardControl<WinKeyState>
{
    /// <summary>Win 键监听器，用于感知按键锁定状态的变化。</summary>
    private readonly WinKeyListener _listener = IoCContainer.Resolve<WinKeyListener>();

    /// <summary>开关处于“开”状态时对应的固件枚举值。</summary>
    protected override WinKeyState OnState => WinKeyState.On;

    /// <summary>开关处于“关”状态时对应的固件枚举值。</summary>
    protected override WinKeyState OffState => WinKeyState.Off;

    /// <summary>
    /// 刷新期间不禁用开关：Win 键状态切换响应很快，无需阻止用户操作。
    /// </summary>
    protected override bool DisablesWhileRefreshing => false;

    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题、副标题，并订阅 Win 键变化事件。
    /// </summary>
    public WinKeyControl()
    {
        Icon = SymbolRegular.Keyboard24;
        Title = Resource.WinKeyControl_Title;
        Subtitle = Resource.WinKeyControl_Message;

        _listener.Changed += Listener_Changed;
    }

    /// <summary>
    /// Win 键变化回调：控件可见时切回 UI 线程刷新状态。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数（无附加数据）。</param>
    private void Listener_Changed(object? sender, EventArgs e) => Dispatcher.Invoke(async () =>
    {
        if (!IsLoaded || !IsVisible)
            return;

        await RefreshAsync();
    });
}

using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “接口背光”仪表盘开关卡片控件，展示并切换机身接口处的氛围灯；
/// 通过灯光变化监听器感知硬件侧状态变化后自动刷新。
/// </summary>
public class PortsBacklightControl : AbstractToggleFeatureCardControl<PortsBacklightState>
{
    /// <summary>灯光变化监听器，用于感知接口背光的状态变化。</summary>
    private readonly LightingChangeListener _listener = IoCContainer.Resolve<LightingChangeListener>();

    /// <summary>开关处于“开”状态时对应的固件枚举值。</summary>
    protected override PortsBacklightState OnState => PortsBacklightState.On;

    /// <summary>开关处于“关”状态时对应的固件枚举值。</summary>
    protected override PortsBacklightState OffState => PortsBacklightState.Off;

    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题、副标题，并订阅灯光变化事件。
    /// </summary>
    public PortsBacklightControl()
    {
        Icon = SymbolRegular.UsbPlug24;
        Title = Resource.PortsBacklightControl_Title;
        Subtitle = Resource.PortsBacklightControl_Message;

        _listener.Changed += Listener_Changed;
    }

    /// <summary>
    /// 灯光变化回调：仅处理接口背光相关的灯效变化，切回 UI 线程刷新状态。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">包含灯光变化状态的参数。</param>
    private void Listener_Changed(object? sender, LightingChangeListener.ChangedEventArgs e) => Dispatcher.Invoke(async () =>
    {
        // 只关心接口背光灯效，其他灯效（如面板 Logo）直接忽略。
        if (e.State != LightingChangeState.Ports)
            return;

        await RefreshAsync();
    });
}

using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “A 面 Logo 背光”仪表盘开关卡片控件，展示并切换顶盖 Logo 灯光；
/// 通过灯光变化监听器感知硬件侧状态变化后自动刷新。
/// </summary>
public class PanelLogoBacklightControl : AbstractToggleFeatureCardControl<PanelLogoBacklightState>
{
    /// <summary>灯光变化监听器，用于感知面板 Logo 背光的状态变化。</summary>
    private readonly LightingChangeListener _listener = IoCContainer.Resolve<LightingChangeListener>();

    /// <summary>开关处于“开”状态时对应的固件枚举值。</summary>
    protected override PanelLogoBacklightState OnState => PanelLogoBacklightState.On;

    /// <summary>开关处于“关”状态时对应的固件枚举值。</summary>
    protected override PanelLogoBacklightState OffState => PanelLogoBacklightState.Off;

    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题、副标题，并订阅灯光变化事件。
    /// </summary>
    public PanelLogoBacklightControl()
    {
        Icon = SymbolRegular.LightbulbCircle24;
        Title = Resource.PanelLogoBacklightControl_Title;
        Subtitle = Resource.PanelLogoBacklightControl_Message;

        _listener.Changed += Listener_Changed;
    }

    /// <summary>
    /// 灯光变化回调：仅处理面板 Logo 相关的灯效变化，切回 UI 线程刷新状态。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">包含灯光变化状态的参数。</param>
    private void Listener_Changed(object? sender, LightingChangeListener.ChangedEventArgs e) => Dispatcher.Invoke(async () =>
    {
        // 只关心面板 Logo 灯效，其他灯效（如端口背光）直接忽略。
        if (e.State != LightingChangeState.Panel)
            return;

        await RefreshAsync();
    });
}

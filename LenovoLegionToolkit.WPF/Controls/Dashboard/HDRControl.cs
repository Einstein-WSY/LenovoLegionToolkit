using System;
using System.Threading.Tasks;
using System.Windows;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “HDR”仪表盘开关卡片控件，展示并切换高动态范围显示；
/// 刷新时会检测当前显示链路是否禁用 HDR，并在不可用时给出警告。
/// </summary>
public class HDRControl : AbstractToggleFeatureCardControl<HDRState>
{
    /// <summary>显示配置监听器，用于感知 HDR 相关显示设置的变化。</summary>
    private readonly DisplayConfigurationListener _listener = IoCContainer.Resolve<DisplayConfigurationListener>();

    /// <summary>开关处于“开”状态时对应的固件枚举值。</summary>
    protected override HDRState OnState => HDRState.On;

    /// <summary>开关处于“关”状态时对应的固件枚举值。</summary>
    protected override HDRState OffState => HDRState.Off;

    /// <summary>
    /// 初始化控件，设置卡片图标与本地化标题、副标题，并订阅显示配置变化事件。
    /// </summary>
    public HDRControl()
    {
        Icon = SymbolRegular.Hdr24;
        Title = Resource.HDRControl_Title;
        Subtitle = Resource.HDRControl_Message;

        _listener.Changed += Listener_Changed;
    }

    /// <summary>
    /// 刷新控件状态：查询 HDR 是否被当前配置阻止，据此禁用开关或显示警告。
    /// </summary>
    /// <returns>异步刷新任务。</returns>
    protected override async Task OnRefreshAsync()
    {
        await base.OnRefreshAsync();

        try
        {
            // HDR 可能因独显直连、显示器能力或线材等原因被系统阻止。
            var isHdrBlocked = await ((HDRFeature)Feature).IsHdrBlockedAsync();

            IsToggleEnabled = !isHdrBlocked;
            Warning = isHdrBlocked ? Resource.HDRControl_Warning : string.Empty;
        }
        catch
        {
            // 检测失败时按可用处理，避免误锁开关影响正常使用。
            IsToggleEnabled = true;
            Warning = string.Empty;
        }

        Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 显示配置变化回调：控件已加载时切回 UI 线程重新检测 HDR 状态。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数（无附加数据）。</param>
    private void Listener_Changed(object? sender, EventArgs e) => Dispatcher.Invoke(async () =>
    {
        if (IsLoaded)
            await RefreshAsync();
    });
}

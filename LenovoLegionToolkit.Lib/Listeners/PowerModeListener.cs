using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Controllers.GodMode;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.System.Management;

namespace LenovoLegionToolkit.Lib.Listeners;

/// <summary>
/// 性能模式监听器：通过 WMI 监听 LenovoGameZoneSmartFanModeEvent，感知性能模式（安静/均衡/性能/自定义）
/// 被硬件快捷键或其他软件切换的事件，并同步应用相关依赖设置。
/// 同时实现 <see cref="INotifyingListener{TEventArgs,TValue}"/>，允许本工具在主动切换模式时走相同处理流程。
/// </summary>
public class PowerModeListener(
    GodModeController godModeController,
    WindowsPowerModeController windowsPowerModeController,
    WindowsPowerPlanController windowsPowerPlanController)
    : AbstractWMIListener<PowerModeListener.ChangedEventArgs, PowerModeState, int>(WMI.LenovoGameZoneSmartFanModeEvent.Listen), INotifyingListener<PowerModeListener.ChangedEventArgs, PowerModeState>
{
    /// <summary>
    /// 性能模式变更事件参数。
    /// </summary>
    public class ChangedEventArgs(PowerModeState state) : EventArgs
    {
        /// <summary>
        /// 变更后的性能模式。
        /// </summary>
        public PowerModeState State { get; } = state;
    }

    /// <inheritdoc/>
    protected override PowerModeState GetValue(int value)
    {
        // WMI 上报的模式值从 1 开始计数，而 PowerModeState 枚举从 0 开始，需要减 1 对齐
        var result = (PowerModeState)(value - 1);
        return result;
    }

    /// <inheritdoc/>
    protected override ChangedEventArgs GetEventArgs(PowerModeState value) => new(value);

    /// <inheritdoc/>
    protected override async Task OnChangedAsync(PowerModeState value)
    {
        await ChangeDependenciesAsync(value).ConfigureAwait(false);
        PublishNotification(value);
    }

    /// <summary>
    /// 主动通知性能模式已变化（由本工具发起切换时使用），会应用依赖设置并触发 <see cref="AbstractWMIListener{TEventArgs,TValue,TRawValue}.Changed"/> 事件，但不发布通知气泡。
    /// </summary>
    /// <param name="value">新的性能模式。</param>
    public async Task NotifyAsync(PowerModeState value)
    {
        await ChangeDependenciesAsync(value).ConfigureAwait(false);
        RaiseChanged(value);
    }

    /// <summary>
    /// 根据性能模式同步应用其依赖的 Windows 电源模式与电源计划；自定义模式还需应用 God Mode 曲线。
    /// </summary>
    /// <param name="value">性能模式。</param>
    private async Task ChangeDependenciesAsync(PowerModeState value)
    {
        if (value is PowerModeState.GodMode)
            await godModeController.ApplyStateAsync().ConfigureAwait(false);

        await windowsPowerModeController.SetPowerModeAsync(value).ConfigureAwait(false);
        await windowsPowerPlanController.SetPowerPlanAsync(value).ConfigureAwait(false);
    }

    /// <summary>
    /// 发布模式切换通知气泡，便于用户在屏幕上确认当前模式。
    /// </summary>
    /// <param name="value">性能模式。</param>
    private static void PublishNotification(PowerModeState value)
    {
        switch (value)
        {
            case PowerModeState.Quiet:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.PowerModeQuiet, value.GetDisplayName()));
                break;
            case PowerModeState.Balance:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.PowerModeBalance, value.GetDisplayName()));
                break;
            case PowerModeState.Performance:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.PowerModePerformance, value.GetDisplayName()));
                break;
            case PowerModeState.GodMode:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.PowerModeGodMode, value.GetDisplayName()));
                break;
        }
    }
}

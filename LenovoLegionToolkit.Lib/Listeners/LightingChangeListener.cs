using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Features.PanelLogo;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Listeners;

/// <summary>
/// 灯光变更监听器：通过 WMI 监听 LenovoLightingEvent，感知面板 Logo 灯与端口指示灯的
/// 硬件级开关变化（通常由 Fn 快捷键直接切换），并发布对应通知。
/// </summary>
public class LightingChangeListener(
    PanelLogoBacklightFeature panelLogoBacklightFeature,
    PortsBacklightFeature portsBacklightFeature,
    FnKeysDisabler fnKeysDisabler)
    : AbstractWMIListener<LightingChangeListener.ChangedEventArgs, LightingChangeState, int>(WMI.LenovoLightingEvent
        .Listen)
{
    /// <summary>
    /// 灯光变更事件参数。
    /// </summary>
    public class ChangedEventArgs(LightingChangeState state) : EventArgs
    {
        /// <summary>
        /// 发生变化的灯光区域。
        /// </summary>
        public LightingChangeState State { get; } = state;
    }

    /// <inheritdoc/>
    protected override LightingChangeState GetValue(int value)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Event received. [value={value}]");

        var result = (LightingChangeState)value;
        return result;
    }

    /// <inheritdoc/>
    protected override ChangedEventArgs GetEventArgs(LightingChangeState value) => new(value);

    /// <inheritdoc/>
    protected override async Task OnChangedAsync(LightingChangeState value)
    {
        try
        {
            // Lenovo FnKeys 在运行时会自行处理快捷键与通知，此时本监听器避免重复提示
            if (await fnKeysDisabler.GetStatusAsync().ConfigureAwait(false) == SoftwareStatus.Enabled)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Ignoring, FnKeys are enabled.");

                return;
            }

            switch (value)
            {
                case LightingChangeState.Panel when await panelLogoBacklightFeature.IsSupportedAsync().ConfigureAwait(false):
                    {
                        var type = await panelLogoBacklightFeature.GetStateAsync().ConfigureAwait(false) == PanelLogoBacklightState.On
                            ? NotificationType.PanelLogoLightingOn
                            : NotificationType.PanelLogoLightingOff;

                        MessagingCenter.Publish(new NotificationMessage(type));
                        break;
                    }
                case LightingChangeState.Ports when await portsBacklightFeature.IsSupportedAsync().ConfigureAwait(false):
                    {
                        var type = await portsBacklightFeature.GetStateAsync().ConfigureAwait(false) == PortsBacklightState.On
                            ? NotificationType.PortLightingOn
                            : NotificationType.PortLightingOff;

                        MessagingCenter.Publish(new NotificationMessage(type));
                        break;
                    }
            }
        }
        // 通知失败不影响硬件状态本身，直接忽略异常
        catch { /* Ignored. */ }
    }
}

using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Listeners;

/// <summary>
/// 屏幕亮度监听器：通过 WMI 监听 WmiMonitorBrightnessEvent，亮度变化时可选地将新亮度
/// 同步应用到所有电源计划（受设置项 SynchronizeBrightnessToAllPowerPlans 控制）。
/// </summary>
public class DisplayBrightnessListener(WindowsPowerPlanController windowsPowerPlanController, ApplicationSettings settings)
    : AbstractWMIListener<DisplayBrightnessListener.ChangedEventArgs, Brightness, byte>(WMI.WmiMonitorBrightnessEvent.Listen)
{
    /// <summary>
    /// 亮度变化事件参数。
    /// </summary>
    public class ChangedEventArgs(Brightness brightness) : EventArgs
    {
        /// <summary>
        /// 变化后的亮度值。
        /// </summary>
        public Brightness Brightness { get; } = brightness;
    }

    // 亮度在拖动滑块时会连续产生大量 WMI 事件，这里用 2 秒节流合并为最后一次，避免高频重复写电源计划
    private readonly ThrottleLastDispatcher _dispatcher = new(TimeSpan.FromSeconds(2), nameof(DisplayBrightnessListener));

    /// <inheritdoc/>
    protected override Brightness GetValue(byte value) => new(value);

    /// <inheritdoc/>
    protected override ChangedEventArgs GetEventArgs(Brightness value) => new(value);

    /// <inheritdoc/>
    protected override async Task OnChangedAsync(Brightness value) => await SynchronizeBrightnessAsync(value).ConfigureAwait(false);

    /// <summary>
    /// 若启用了"同步亮度到所有电源计划"，则以节流方式将亮度写入每个电源计划。
    /// </summary>
    /// <param name="value">最新亮度值。</param>
    private async Task SynchronizeBrightnessAsync(Brightness value)
    {
        if (!settings.Store.SynchronizeBrightnessToAllPowerPlans)
            return;

        await _dispatcher.DispatchAsync(() =>
        {
            SetBrightnessForAllPowerPlans(value);
            return Task.CompletedTask;
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// 遍历所有电源计划并将亮度参数写入；失败仅记录日志，不影响监听器运行。
    /// </summary>
    /// <param name="brightness">要写入的亮度值。</param>
    private void SetBrightnessForAllPowerPlans(Brightness brightness)
    {
        try
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Setting brightness to {brightness.Value}...");

            var powerPlans = windowsPowerPlanController.GetPowerPlans();

            foreach (var powerPlan in powerPlans)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Modifying power plan {powerPlan.Name}... [powerPlan.Guid={powerPlan.Guid}, brightness={brightness.Value}]");

                windowsPowerPlanController.SetPowerPlanParameter(powerPlan, brightness);
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Brightness set to {brightness.Value}.");
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to set brightness to {brightness.Value}.", ex);
        }
    }
}

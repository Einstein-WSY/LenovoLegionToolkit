using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Listeners;

/// <summary>
/// 特殊功能键监听器：监听 WMI 的 LenovoUtilityEvent，处理摄像头开关、Fn 锁、Fn+R 刷新率切换、
/// 截图、Spectrum/白色键盘背光档位等非驱动类快捷键，并发布对应通知。
/// </summary>
/// <param name="settings">应用设置，读取刷新率切换时需排除的刷新率列表。</param>
/// <param name="fnKeysDisabler">FnKeys 软件状态检测器，FnKeys 启用时让出按键处理。</param>
/// <param name="feature">刷新率功能，用于 Fn+R 循环切换刷新率。</param>
/// <param name="microphoneFeature">麦克风功能，用于 Fn+F4 切换麦克风。</param>
public class SpecialKeyListener(
    ApplicationSettings settings,
    FnKeysDisabler fnKeysDisabler,
    RefreshRateFeature feature,
    MicrophoneFeature microphoneFeature)
    : AbstractWMIListener<SpecialKeyListener.ChangedEventArgs, SpecialKey, int>(WMI.LenovoUtilityEvent.Listen)
{
    /// <summary>
    /// 特殊功能键事件参数。
    /// </summary>
    public class ChangedEventArgs(SpecialKey specialKey) : EventArgs
    {
        /// <summary>
        /// 被按下的特殊功能键。
        /// </summary>
        public SpecialKey SpecialKey { get; } = specialKey;
    }

    // 刷新率切换会触发显示器重新初始化，短时间内可能重复上报按键，使用 2 秒首触发节流
    private readonly ThrottleFirstDispatcher _refreshRateDispatcher = new(TimeSpan.FromSeconds(2), nameof(SpecialKeyListener));

    /// <inheritdoc/>
    protected override SpecialKey GetValue(int value)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Event received. [value={value}]");

        var result = (SpecialKey)value;
        return result;
    }

    /// <inheritdoc/>
    protected override ChangedEventArgs GetEventArgs(SpecialKey value) => new(value);

    /// <inheritdoc/>
    protected override async Task OnChangedAsync(SpecialKey value)
    {
        try
        {
            // Lenovo FnKeys 软件在运行时会处理这些快捷键，此时避免重复执行与提示
            if (await fnKeysDisabler.GetStatusAsync().ConfigureAwait(false) == SoftwareStatus.Enabled)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Ignoring, FnKeys are enabled.");
                return;
            }

            switch (value)
            {
                case SpecialKey.CameraOn or SpecialKey.CameraOff:
                    NotifyCameraState(value);
                    break;
                case SpecialKey.FnLockOn or SpecialKey.FnLockOff:
                    NotifyFnLockState(value);
                    break;
                case SpecialKey.FnR or SpecialKey.FnR2:
                    await ToggleRefreshRateAsync().ConfigureAwait(false);
                    break;
                case SpecialKey.FnPrtSc or SpecialKey.FnPrtSc2:
                    OpenSnippingTool();
                    break;
                case SpecialKey.SpectrumBacklightOff:
                    NotifySpectrumBacklight(SpectrumKeyboardBacklightBrightness.Off);
                    break;
                case SpecialKey.SpectrumBacklight1:
                    NotifySpectrumBacklight(SpectrumKeyboardBacklightBrightness.Low);
                    break;
                case SpecialKey.SpectrumBacklight2:
                    NotifySpectrumBacklight(SpectrumKeyboardBacklightBrightness.Medium);
                    break;
                case SpecialKey.SpectrumBacklight3:
                    NotifySpectrumBacklight(SpectrumKeyboardBacklightBrightness.High);
                    break;
                case SpecialKey.SpectrumPreset1:
                    NotifySpectrumPreset(1);
                    break;
                case SpecialKey.SpectrumPreset2:
                    NotifySpectrumPreset(2);
                    break;
                case SpecialKey.SpectrumPreset3:
                    NotifySpectrumPreset(3);
                    break;
                case SpecialKey.SpectrumPreset4:
                    NotifySpectrumPreset(4);
                    break;
                case SpecialKey.SpectrumPreset5:
                    NotifySpectrumPreset(5);
                    break;
                case SpecialKey.SpectrumPreset6:
                    NotifySpectrumPreset(6);
                    break;
                case SpecialKey.FnF4:
                    await ToggleMicrophoneAsync().ConfigureAwait(false);
                    break;
                case SpecialKey.FnF8:
                    OpenAirplaneModeSettings();
                    break;
                case SpecialKey.WhiteBacklightOff:
                    NotifyWhiteBacklight(WhiteKeyboardBacklightState.Off);
                    break;
                case SpecialKey.WhiteBacklight1:
                    NotifyWhiteBacklight(WhiteKeyboardBacklightState.Low);
                    break;
                case SpecialKey.WhiteBacklight2:
                    NotifyWhiteBacklight(WhiteKeyboardBacklightState.High);
                    break;
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to handle key. [key={value}, value={(int)value}]", ex);
        }
    }

    /// <summary>
    /// 根据按键值发布摄像头开关通知。
    /// </summary>
    /// <param name="value">特殊功能键。</param>
    private static void NotifyCameraState(SpecialKey value)
    {
        switch (value)
        {
            case SpecialKey.CameraOn:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.CameraOn));
                break;
            case SpecialKey.CameraOff:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.CameraOff));
                break;
        }
    }

    /// <summary>
    /// 根据按键值发布 Fn 锁开关通知。
    /// </summary>
    /// <param name="value">特殊功能键。</param>
    private static void NotifyFnLockState(SpecialKey value)
    {
        switch (value)
        {
            case SpecialKey.FnLockOn:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.FnLockOn));
                break;
            case SpecialKey.FnLockOff:
                MessagingCenter.Publish(new NotificationMessage(NotificationType.FnLockOff));
                break;
        }
    }

    /// <summary>
    /// Fn+R 循环切换刷新率：在（全部刷新率 - 用户排除项）中按顺序取下一个，不足两个时无法切换。
    /// </summary>
    private Task ToggleRefreshRateAsync() => _refreshRateDispatcher.DispatchAsync(async () =>
    {
        try
        {
            if (!await feature.IsSupportedAsync().ConfigureAwait(false))
                return;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Switch refresh rate after Fn+R...");

            var all = await feature.GetAllStatesAsync().ConfigureAwait(false);
            var current = await feature.GetStateAsync().ConfigureAwait(false);
            var excluded = settings.Store.ExcludedRefreshRates;

            var filtered = all.Except(excluded).ToArray();

            if (Log.Instance.IsTraceEnabled)
            {
                Log.Instance.Trace($"Refresh rates: [all={string.Join(", ", all.Select(r => r.Frequency))}]");
                Log.Instance.Trace($" - All: {string.Join(", ", all.Select(r => r.Frequency))}");
                Log.Instance.Trace($" - Excluded: {string.Join(", ", excluded.Select(r => r.Frequency))}");
                Log.Instance.Trace($" - Filtered: {string.Join(", ", filtered.Select(r => r.Frequency))}");
            }

            if (filtered.Length < 2)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Can't switch refresh rate after Fn+R when there is less than 2 available.");
                return;
            }

            var currentIndex = Array.IndexOf(filtered, current);
            var newIndex = currentIndex + 1;
            if (newIndex >= filtered.Length)
                newIndex = 0;

            var next = filtered[newIndex];

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Switching refresh rate after Fn+R to {next}...");

            await feature.SetStateAsync(next).ConfigureAwait(false);

            // 刷新率设置写入后需要等待显示器重新同步，延迟 1 秒再弹出通知，避免提示与实际显示不一致
            _ = Task.Delay(TimeSpan.FromSeconds(1)).ContinueWith(_ =>
            {
                MessagingCenter.Publish(new NotificationMessage(NotificationType.RefreshRate, next.DisplayName));
            });

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Switched refresh rate after Fn+R to {next}.");
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to switch refresh rate after Fn+R.", ex);
        }
    });

    /// <summary>
    /// 启动 Windows 截图工具（通过 ms-screenclip: 协议调用系统截图界面）。
    /// </summary>
    private static void OpenSnippingTool()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Starting snipping tool..");

        Process.Start("explorer", "ms-screenclip:");
    }

    /// <summary>
    /// 发布 Spectrum 键盘背光亮度变更通知。
    /// </summary>
    /// <param name="value">新的亮度档位。</param>
    private static void NotifySpectrumBacklight(SpectrumKeyboardBacklightBrightness value)
    {
        var type = value is SpectrumKeyboardBacklightBrightness.Off
            ? NotificationType.SpectrumBacklightOff
            : NotificationType.SpectrumBacklightChanged;
        MessagingCenter.Publish(new NotificationMessage(type, value));
    }

    /// <summary>
    /// 发布 Spectrum 键盘背光预设变更通知。
    /// </summary>
    /// <param name="value">新的预设编号。</param>
    private static void NotifySpectrumPreset(int value) => MessagingCenter.Publish(new NotificationMessage(NotificationType.SpectrumBacklightPresetChanged, value));

    /// <summary>
    /// Fn+F4 切换麦克风开关状态并发布通知。
    /// </summary>
    private async Task ToggleMicrophoneAsync()
    {

        if (!await microphoneFeature.IsSupportedAsync().ConfigureAwait(false))
            return;

        switch (await microphoneFeature.GetStateAsync().ConfigureAwait(false))
        {
            case MicrophoneState.On:
                await microphoneFeature.SetStateAsync(MicrophoneState.Off).ConfigureAwait(false);
                MessagingCenter.Publish(new NotificationMessage(NotificationType.MicrophoneOff));
                break;
            case MicrophoneState.Off:
                await microphoneFeature.SetStateAsync(MicrophoneState.On).ConfigureAwait(false);
                MessagingCenter.Publish(new NotificationMessage(NotificationType.MicrophoneOn));
                break;
        }
    }

    /// <summary>
    /// 打开 Windows 飞行模式设置页面。
    /// </summary>
    private static void OpenAirplaneModeSettings() => AirplaneMode.Open();

    /// <summary>
    /// 发布白色键盘背光状态变更通知。
    /// </summary>
    /// <param name="value">新的背光状态。</param>
    private static void NotifyWhiteBacklight(WhiteKeyboardBacklightState value)
    {
        var type = value is WhiteKeyboardBacklightState.Off
            ? NotificationType.WhiteKeyboardBacklightOff
            : NotificationType.WhiteKeyboardBacklightChanged;
        MessagingCenter.Publish(new NotificationMessage(type, value));
    }
}

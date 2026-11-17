using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Windows;
using LenovoLegionToolkit.WPF.Windows.Utils;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Utils;

/// <summary>
/// 通知管理器：订阅通知消息，根据用户设置与全屏状态决定是否弹出，并在指定屏幕上显示通知窗口。
/// </summary>
public class NotificationsManager
{
    private static Dispatcher Dispatcher => Application.Current.Dispatcher;

    private readonly ApplicationSettings _settings;

    // 当前已显示的通知窗口，用于在下一个通知到来时统一关闭
    private List<INotificationWindow?> _windows = [];

    /// <summary>
    /// 构造通知管理器并订阅通知消息。
    /// </summary>
    /// <param name="settings">应用设置。</param>
    public NotificationsManager(ApplicationSettings settings)
    {
        _settings = settings;

        MessagingCenter.Subscribe<NotificationMessage>(this, OnNotificationReceived);
    }

    // 收到通知消息后，切回 UI 线程完成过滤与显示
    private void OnNotificationReceived(NotificationMessage notification)
    {
        Dispatcher.Invoke(() =>
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Notification {notification} received");

            // 用户可全局关闭通知
            if (_settings.Store.DontShowNotifications)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Notifications are disabled.");

                return;
            }

            // 有全屏应用且未允许置顶时，不打断用户
            if (FullscreenHelper.IsAnyApplicationFullscreen() && !_settings.Store.NotificationAlwaysOnTop)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Some application is in fullscreen.");

                return;
            }

            // 将通知类型映射到对应的分类开关
            var allow = notification.Type switch
            {
                NotificationType.ACAdapterConnected => _settings.Store.Notifications.ACAdapter,
                NotificationType.ACAdapterConnectedLowWattage => _settings.Store.Notifications.ACAdapter,
                NotificationType.ACAdapterDisconnected => _settings.Store.Notifications.ACAdapter,
                NotificationType.AutomationNotification => _settings.Store.Notifications.AutomationNotification,
                NotificationType.CapsLockOn => _settings.Store.Notifications.CapsNumLock,
                NotificationType.CapsLockOff => _settings.Store.Notifications.CapsNumLock,
                NotificationType.CameraOn => _settings.Store.Notifications.CameraLock,
                NotificationType.CameraOff => _settings.Store.Notifications.CameraLock,
                NotificationType.FnLockOn => _settings.Store.Notifications.FnLock,
                NotificationType.FnLockOff => _settings.Store.Notifications.FnLock,
                NotificationType.MicrophoneOn => _settings.Store.Notifications.Microphone,
                NotificationType.MicrophoneOff => _settings.Store.Notifications.Microphone,
                NotificationType.NumLockOn => _settings.Store.Notifications.CapsNumLock,
                NotificationType.NumLockOff => _settings.Store.Notifications.CapsNumLock,
                NotificationType.PanelLogoLightingOn => _settings.Store.Notifications.KeyboardBacklight,
                NotificationType.PanelLogoLightingOff => _settings.Store.Notifications.KeyboardBacklight,
                NotificationType.PortLightingOn => _settings.Store.Notifications.KeyboardBacklight,
                NotificationType.PortLightingOff => _settings.Store.Notifications.KeyboardBacklight,
                NotificationType.PowerModeQuiet => _settings.Store.Notifications.PowerMode,
                NotificationType.PowerModeBalance => _settings.Store.Notifications.PowerMode,
                NotificationType.PowerModePerformance => _settings.Store.Notifications.PowerMode,
                NotificationType.PowerModeGodMode => _settings.Store.Notifications.PowerMode,
                NotificationType.RefreshRate => _settings.Store.Notifications.RefreshRate,
                NotificationType.RGBKeyboardBacklightOff => _settings.Store.Notifications.KeyboardBacklight,
                NotificationType.RGBKeyboardBacklightChanged => _settings.Store.Notifications.KeyboardBacklight,
                NotificationType.SmartKeyDoublePress => _settings.Store.Notifications.SmartKey,
                NotificationType.SmartKeySinglePress => _settings.Store.Notifications.SmartKey,
                NotificationType.SpectrumBacklightChanged => _settings.Store.Notifications.KeyboardBacklight,
                NotificationType.SpectrumBacklightOff => _settings.Store.Notifications.KeyboardBacklight,
                NotificationType.SpectrumBacklightPresetChanged => _settings.Store.Notifications.KeyboardBacklight,
                NotificationType.TouchpadOn => _settings.Store.Notifications.TouchpadLock,
                NotificationType.TouchpadOff => _settings.Store.Notifications.TouchpadLock,
                NotificationType.WhiteKeyboardBacklightOff => _settings.Store.Notifications.KeyboardBacklight,
                NotificationType.WhiteKeyboardBacklightChanged => _settings.Store.Notifications.KeyboardBacklight,
                _ => throw new ArgumentException(nameof(notification.Type))
            };

            if (!allow)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Notification type {notification.Type} is disabled.");

                return;
            }

            // 根据通知类型选择对应的图标
            var symbol = notification.Type switch
            {
                NotificationType.ACAdapterConnected => SymbolRegular.BatteryCharge24,
                NotificationType.ACAdapterConnectedLowWattage => SymbolRegular.BatteryCharge24,
                NotificationType.ACAdapterDisconnected => SymbolRegular.BatteryCharge24,
                NotificationType.AutomationNotification => SymbolRegular.Rocket24,
                NotificationType.CapsLockOn => SymbolRegular.KeyboardShiftUppercase24,
                NotificationType.CapsLockOff => SymbolRegular.KeyboardShiftUppercase24,
                NotificationType.CameraOn => SymbolRegular.Camera24,
                NotificationType.CameraOff => SymbolRegular.Camera24,
                NotificationType.FnLockOn => SymbolRegular.Keyboard24,
                NotificationType.FnLockOff => SymbolRegular.Keyboard24,
                NotificationType.MicrophoneOn => SymbolRegular.Mic24,
                NotificationType.MicrophoneOff => SymbolRegular.Mic24,
                NotificationType.NumLockOn => SymbolRegular.Keyboard12324,
                NotificationType.NumLockOff => SymbolRegular.Keyboard12324,
                NotificationType.PanelLogoLightingOn => SymbolRegular.LightbulbCircle24,
                NotificationType.PanelLogoLightingOff => SymbolRegular.LightbulbCircle24,
                NotificationType.PortLightingOn => SymbolRegular.UsbPlug24,
                NotificationType.PortLightingOff => SymbolRegular.UsbPlug24,
                NotificationType.PowerModeQuiet => SymbolRegular.Gauge24,
                NotificationType.PowerModeBalance => SymbolRegular.Gauge24,
                NotificationType.PowerModePerformance => SymbolRegular.Gauge24,
                NotificationType.PowerModeGodMode => SymbolRegular.Gauge24,
                NotificationType.RefreshRate => SymbolRegular.DesktopPulse24,
                NotificationType.RGBKeyboardBacklightOff => SymbolRegular.Lightbulb24,
                NotificationType.RGBKeyboardBacklightChanged => SymbolRegular.Lightbulb24,
                NotificationType.SmartKeyDoublePress => SymbolRegular.StarEmphasis24,
                NotificationType.SmartKeySinglePress => SymbolRegular.Star24,
                NotificationType.SpectrumBacklightChanged => SymbolRegular.Lightbulb24,
                NotificationType.SpectrumBacklightOff => SymbolRegular.Lightbulb24,
                NotificationType.SpectrumBacklightPresetChanged => SymbolRegular.Lightbulb24,
                NotificationType.TouchpadOn => SymbolRegular.Tablet24,
                NotificationType.TouchpadOff => SymbolRegular.Tablet24,
                NotificationType.WhiteKeyboardBacklightOff => SymbolRegular.Lightbulb24,
                NotificationType.WhiteKeyboardBacklightChanged => SymbolRegular.Lightbulb24,
                _ => throw new ArgumentException(nameof(notification.Type))
            };

            // “关闭”类通知在主图标上叠加一条斜线
            SymbolRegular? overlaySymbol = notification.Type switch
            {
                NotificationType.ACAdapterDisconnected => SymbolRegular.Line24,
                NotificationType.CapsLockOff => SymbolRegular.Line24,
                NotificationType.CameraOff => SymbolRegular.Line24,
                NotificationType.FnLockOff => SymbolRegular.Line24,
                NotificationType.MicrophoneOff => SymbolRegular.Line24,
                NotificationType.NumLockOff => SymbolRegular.Line24,
                NotificationType.PanelLogoLightingOff => SymbolRegular.Line24,
                NotificationType.PortLightingOff => SymbolRegular.Line24,
                NotificationType.RGBKeyboardBacklightOff => SymbolRegular.Line24,
                NotificationType.SpectrumBacklightOff => SymbolRegular.Line24,
                NotificationType.TouchpadOff => SymbolRegular.Line24,
                NotificationType.WhiteKeyboardBacklightOff => SymbolRegular.Line24,
                _ => null,
            };

            // 生成通知正文，部分类型需要格式化 Args 参数
            var text = notification.Type switch
            {
                NotificationType.ACAdapterConnected => Resource.Notification_ACAdapterConnected,
                NotificationType.ACAdapterConnectedLowWattage => Resource.Notification_ACAdapterConnectedLowWattage,
                NotificationType.ACAdapterDisconnected => Resource.Notification_ACAdapterDisconnected,
                NotificationType.AutomationNotification => string.Format("{0}", notification.Args),
                NotificationType.CapsLockOn => Resource.Notification_CapsLockOn,
                NotificationType.CapsLockOff => Resource.Notification_CapsLockOff,
                NotificationType.CameraOn => Resource.Notification_CameraOn,
                NotificationType.CameraOff => Resource.Notification_CameraOff,
                NotificationType.FnLockOn => Resource.Notification_FnLockOn,
                NotificationType.FnLockOff => Resource.Notification_FnLockOff,
                NotificationType.MicrophoneOn => Resource.Notification_MicrophoneOn,
                NotificationType.MicrophoneOff => Resource.Notification_MicrophoneOff,
                NotificationType.NumLockOn => Resource.Notification_NumLockOn,
                NotificationType.NumLockOff => Resource.Notification_NumLockOff,
                NotificationType.PanelLogoLightingOn => Resource.Notification_PanelLogoLightingOn,
                NotificationType.PanelLogoLightingOff => Resource.Notification_PanelLogoLightingOff,
                NotificationType.PortLightingOn => Resource.Notification_PortLightingOn,
                NotificationType.PortLightingOff => Resource.Notification_PortLightingOff,
                NotificationType.PowerModeQuiet => string.Format("{0}", notification.Args),
                NotificationType.PowerModeBalance => string.Format("{0}", notification.Args),
                NotificationType.PowerModePerformance => string.Format("{0}", notification.Args),
                NotificationType.PowerModeGodMode => string.Format("{0}", notification.Args),
                NotificationType.RefreshRate => string.Format("{0}", notification.Args),
                NotificationType.RGBKeyboardBacklightOff => string.Format("{0}", notification.Args),
                NotificationType.RGBKeyboardBacklightChanged => string.Format("{0}", notification.Args),
                NotificationType.SmartKeyDoublePress => string.Format("{0}", notification.Args),
                NotificationType.SmartKeySinglePress => string.Format("{0}", notification.Args),
                NotificationType.SpectrumBacklightChanged => string.Format(Resource.Notification_SpectrumKeyboardBacklight_Brightness, notification.Args),
                NotificationType.SpectrumBacklightOff => string.Format(Resource.Notification_SpectrumKeyboardBacklight_Backlight, notification.Args),
                NotificationType.SpectrumBacklightPresetChanged => string.Format(Resource.Notification_SpectrumKeyboardBacklight_Profile, notification.Args),
                NotificationType.TouchpadOn => Resource.Notification_TouchpadOn,
                NotificationType.TouchpadOff => Resource.Notification_TouchpadOff,
                NotificationType.WhiteKeyboardBacklightOff => string.Format(Resource.Notification_WhiteKeyboardBacklight, notification.Args),
                NotificationType.WhiteKeyboardBacklightChanged => string.Format(Resource.Notification_WhiteKeyboardBacklight, notification.Args),
                _ => throw new ArgumentException(nameof(notification.Type))
            };

            // 电源模式通知使用对应的主题色渲染图标
            Action<SymbolIcon>? symbolTransform = notification.Type switch
            {
                NotificationType.PowerModeQuiet => si => si.Foreground = PowerModeState.Quiet.GetSolidColorBrush(),
                NotificationType.PowerModePerformance => si => si.Foreground = PowerModeState.Performance.GetSolidColorBrush(),
                NotificationType.PowerModeGodMode => si => si.Foreground = PowerModeState.GodMode.GetSolidColorBrush(),
                _ => null
            };

            // 点击通知默认无附加操作
            Action? clickAction = null;

            if (symbolTransform is null && overlaySymbol is not null)
                symbolTransform = si =>
                    si.SetResourceReference(Control.ForegroundProperty, "TextFillColorSecondaryBrush");

            ShowNotification(symbol, overlaySymbol, symbolTransform, text, clickAction);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Notification {notification} shown.");
        });
    }

    // 在主屏或所有屏幕上创建并显示通知窗口，先关闭上一批通知
    private void ShowNotification(SymbolRegular symbol, SymbolRegular? overlaySymbol, Action<SymbolIcon>? symbolTransform, string text, Action? clickAction)
    {
        if (App.Current.MainWindow is not MainWindow mainWindow)
            return;

        // 同一时间只保留最新一批通知
        if (_windows.Count != 0)
        {
            foreach (var window in _windows)
                window?.Close(true);

            _windows.Clear();
        }

        ScreenHelper.UpdateScreenInfos();
        if (_settings.Store.NotificationOnAllScreens)
        {
            foreach (var screen in ScreenHelper.Screens)
            {
                var nw = new NotificationWindow(symbol, overlaySymbol, symbolTransform, text, clickAction, screen, _settings.Store.NotificationPosition) { Owner = mainWindow };
                if (_settings.Store.NotificationAlwaysOnTop)
                {
                    // 置顶模式：先截图再以无边框置顶窗口显示，避免被全屏应用遮挡
                    var bitmap = nw.GetBitmapView();
                    var nwaot = new NotificationAoTWindow(bitmap, screen, _settings.Store.NotificationPosition);
                    nwaot.Show(_settings.Store.NotificationDuration switch
                    {
                        NotificationDuration.Short => 500,
                        NotificationDuration.Long => 2500,
                        NotificationDuration.Normal => 1000,
                        _ => throw new ArgumentException(nameof(_settings.Store.NotificationDuration))
                    });
                    _windows.Add(nwaot);
                }
                else
                {
                    nw.Show(_settings.Store.NotificationDuration switch
                    {
                        NotificationDuration.Short => 500,
                        NotificationDuration.Long => 2500,
                        NotificationDuration.Normal => 1000,
                        _ => throw new ArgumentException(nameof(_settings.Store.NotificationDuration))
                    });
                    _windows.Add(nw);
                }
            }
        }
        else
        {
            // 仅在主屏显示
            var primaryScreen = ScreenHelper.PrimaryScreen;
            if (!primaryScreen.HasValue)
                return;

            var nw = new NotificationWindow(symbol, overlaySymbol, symbolTransform, text, clickAction, primaryScreen.Value, _settings.Store.NotificationPosition) { Owner = mainWindow };
            if (_settings.Store.NotificationAlwaysOnTop)
            {
                var bitmap = nw.GetBitmapView();
                var nwaot = new NotificationAoTWindow(bitmap, primaryScreen.Value, _settings.Store.NotificationPosition);
                nwaot.Show(_settings.Store.NotificationDuration switch
                {
                    NotificationDuration.Short => 500,
                    NotificationDuration.Long => 2500,
                    NotificationDuration.Normal => 1000,
                    _ => throw new ArgumentException(nameof(_settings.Store.NotificationDuration))
                });
                _windows.Add(nwaot);
            }
            else
            {
                nw.Show(_settings.Store.NotificationDuration switch
                {
                    NotificationDuration.Short => 500,
                    NotificationDuration.Long => 2500,
                    NotificationDuration.Normal => 1000,
                    _ => throw new ArgumentException(nameof(_settings.Store.NotificationDuration))
                });
                _windows.Add(nw);
            }
        }
    }
}

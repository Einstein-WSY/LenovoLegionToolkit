using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.NetworkManagement.WiFi;

namespace LenovoLegionToolkit.Lib.AutoListeners;

/// <summary>
/// Wi-Fi 连接状态监听器（基于 WLAN API 的 ACM 通知）。
/// </summary>
public class WiFiAutoListener : AbstractAutoListener<WiFiAutoListener.ChangedEventArgs>
{
    /// <summary>
    /// Wi-Fi 状态变化事件参数。
    /// </summary>
    public class ChangedEventArgs(bool isConnected, string? ssid) : EventArgs
    {
        public bool IsConnected { get; } = isConnected; // 是否已连接
        public string? Ssid { get; } = ssid; // 已连接的 SSID（断开时为 null）
    }

    private readonly IMainThreadDispatcher _mainThreadDispatcher;
    private readonly WLAN_NOTIFICATION_CALLBACK _wlanCallback; // 回调委托需长期持有，防止被 GC 回收

    private LambdaDisposable? _wlanNotificationDisposable;

    public unsafe WiFiAutoListener(IMainThreadDispatcher mainThreadDispatcher)
    {
        _mainThreadDispatcher = mainThreadDispatcher;

        _wlanCallback = WlanCallback;
    }

    protected override Task StartAsync() => _mainThreadDispatcher.DispatchAsync(() =>
    {
        _wlanNotificationDisposable = RegisterWlanNotification();
        return Task.CompletedTask;
    });

    protected override Task StopAsync() => _mainThreadDispatcher.DispatchAsync(() =>
    {
        _wlanNotificationDisposable?.Dispose();
        _wlanNotificationDisposable = null;
        return Task.CompletedTask;
    });

    // WLAN API 句柄需要在消息循环线程上打开，因此切换到主线程
    private unsafe LambdaDisposable? RegisterWlanNotification()
    {
        var handlePtr = IntPtr.Zero;

        try
        {
            // WlanOpenHandle 通过指针返回句柄，先分配非托管内存
            handlePtr = Marshal.AllocHGlobal(Marshal.SizeOf<HANDLE>());

            // 参数 2 为 WLAN API 版本号（Windows Vista+ 固定为 2）
            if (PInvoke.WlanOpenHandle(2, out _, (HANDLE*)handlePtr) != 0)
                return null;

            var handle = Marshal.PtrToStructure<HANDLE>(handlePtr);

            // 仅订阅 ACM（自动配置模块）通知，即连接/断开等用户可见事件
            var result = PInvoke.WlanRegisterNotification(handle,
                WLAN_NOTIFICATION_SOURCES.WLAN_NOTIFICATION_SOURCE_ACM,
                true,
                _wlanCallback,
                null,
                null,
                null);
            if (result != 0)
                return null;

            // 返回可释放对象，停止时取消注册并关闭句柄
            return new LambdaDisposable(() =>
            {
                _ = PInvoke.WlanRegisterNotification(handle,
                    WLAN_NOTIFICATION_SOURCES.WLAN_NOTIFICATION_SOURCE_NONE,
                    true,
                    _wlanCallback,
                    null,
                    null,
                    null);

                _ = PInvoke.WlanCloseHandle(handle, null);
            });
        }
        finally
        {
            Marshal.FreeHGlobal(handlePtr);
        }
    }

    // WLAN 通知回调：NotificationCode 为 wlan_notification_acm_connection_complete(0x0A) /
    // wlan_notification_acm_disconnected(0x15) 时分别上报连接与断开
    private unsafe void WlanCallback(L2_NOTIFICATION_DATA* param0, void* param1)
    {
        ref var data = ref Unsafe.AsRef<L2_NOTIFICATION_DATA>(param0);

        switch (data.NotificationCode)
        {
            case 0x0A: /* Connected */
                ref var notificationData = ref Unsafe.AsRef<WLAN_CONNECTION_NOTIFICATION_DATA>(data.pData);
                var dot11Ssid = notificationData.dot11Ssid;
                // SSID 为定长字节数组 + 实际长度，需按长度截取后再用 UTF-8 解码
                var ssid = Encoding.UTF8.GetString(dot11Ssid.ucSSID.Value, (int)dot11Ssid.uSSIDLength);

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"WiFi connected. [ssid={ssid}]");

                RaiseChanged(new ChangedEventArgs(true, ssid));
                break;
            case 0x15: /* Disconnected */
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"WiFi disconnected.");

                RaiseChanged(new ChangedEventArgs(false, null));
                break;
        }
    }
}

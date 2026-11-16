using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Utils;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.RemoteDesktop;

namespace LenovoLegionToolkit.Lib.Listeners;

/// <summary>
/// 会话锁定/解锁监听器：监听 <see cref="SystemEvents.SessionSwitch"/>，并结合
/// WTSQuerySessionInformation 精确判断当前会话是否已锁定。
/// </summary>
public class SessionLockUnlockListener : IListener<SessionLockUnlockListener.ChangedEventArgs>
{
    /// <summary>
    /// 会话锁定状态变更事件参数。
    /// </summary>
    public class ChangedEventArgs(bool locked) : EventArgs
    {
        /// <summary>
        /// 是否已锁定。
        /// </summary>
        public bool Locked { get; } = locked;
    }

    /// <summary>
    /// 会话锁定/解锁事件。仅在会话锁定或解锁时触发。
    /// </summary>
    public event EventHandler<ChangedEventArgs>? Changed;

    /// <summary>
    /// 当前会话是否已锁定；无法确定时为 null。
    /// </summary>
    public bool? IsLocked { get; private set; }

    /// <inheritdoc/>
    public Task StartAsync()
    {
        SystemEvents.SessionSwitch += SystemEvents_SessionSwitch;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync()
    {
        SystemEvents.SessionSwitch -= SystemEvents_SessionSwitch;
        return Task.CompletedTask;
    }

    private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        // 只关心锁定与解锁，登录/注销/远程连接等其他原因一律忽略
        if (e.Reason != SessionSwitchReason.SessionLock && e.Reason != SessionSwitchReason.SessionUnlock)
            return;

        // SessionSwitch 事件原因有时与实际状态不一致，这里再向系统查询一次权威状态
        var flags = GetActiveConsoleSessionFlags();
        if (flags == PInvoke.WTS_SESSIONSTATE_UNKNOWN)
        {
            IsLocked = null;
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Unknown error occured when getting active console session flags.");
            return;
        }
        var locked = (flags == PInvoke.WTS_SESSIONSTATE_LOCK);
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Session lock unlock state switched. [locked={locked}]");
        IsLocked = locked;
        Changed?.Invoke(this, new(locked));
    }

    /// <summary>
    /// 查询当前活动控制台会话的锁屏标志。
    /// </summary>
    /// <returns>返回会话状态标志；查询失败时返回 <c>WTS_SESSIONSTATE_UNKNOWN</c>。</returns>
    private static unsafe uint GetActiveConsoleSessionFlags()
    {
        const WTS_INFO_CLASS infoClass = WTS_INFO_CLASS.WTSSessionInfoEx;

        var sessionFlags = PInvoke.WTS_SESSIONSTATE_UNKNOWN;
        var dwSessionId = PInvoke.WTSGetActiveConsoleSessionId();

        if (!PInvoke.WTSQuerySessionInformation(HANDLE.WTS_CURRENT_SERVER_HANDLE, dwSessionId, infoClass, out var ppBuffer, out var pBytesReturned))
            return sessionFlags;

        if (pBytesReturned > 0)
        {
            // WTSSessionInfoEx 的大小随 Level 变化，只有 Level 1（当前唯一已知版本）才解析锁屏标志
            var info = Marshal.PtrToStructure<WTSINFOEXW>((IntPtr)ppBuffer.Value);
            if (info.Level == 1)
                sessionFlags = (uint)info.Data.WTSInfoExLevel1.SessionFlags;
        }

        // WTSQuerySessionInformation 分配的内存必须用 WTSFreeMemory 释放，否则会泄漏
        PInvoke.WTSFreeMemory(ppBuffer);
        return sessionFlags;
    }
}

using System;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using Microsoft.Win32;

namespace LenovoLegionToolkit.Lib.Listeners;

/// <summary>
/// 显示配置监听器：监听系统级显示设置变更（分辨率、显示器增减、HDR 开关等），
/// 并在 HDR 状态实际发生变化时通过事件参数告知订阅者。
/// </summary>
public class DisplayConfigurationListener : IListener<DisplayConfigurationListener.ChangedEventArgs>
{
    /// <summary>
    /// 显示配置变更事件参数。
    /// </summary>
    public class ChangedEventArgs : EventArgs
    {
        /// <summary>
        /// HDR 状态：仅当 HDR 开关实际发生变化时为 true/false；否则为 null（表示只是其他显示设置变了）。
        /// </summary>
        public bool? HDR { get; init; }
    }

    private bool _started;

    /// <summary>
    /// 当前 HDR 是否开启；状态未知（如查询失败）时为 null。
    /// </summary>
    public bool? IsHDROn { get; private set; }

    /// <summary>
    /// 显示设置变更事件。每次系统显示设置变化都会触发，事件参数中的 HDR 字段指示 HDR 是否实际切换。
    /// </summary>
    public event EventHandler<ChangedEventArgs>? Changed;

    /// <inheritdoc/>
    public Task StartAsync()
    {
        // 幂等启动，避免重复订阅 SystemEvents
        if (_started)
            return Task.CompletedTask;

        IsHDROn = GetHDRStatus();

        SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        _started = true;

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync()
    {
        SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        _started = false;

        IsHDROn = null;

        return Task.CompletedTask;
    }

    private void SystemEvents_DisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Event received.");

        // 显示配置变化后内部显示器的缓存信息可能失效，标记需要刷新
        InternalDisplay.SetNeedsRefresh();

        // 去重：只有 HDR 开关状态相对上次确实发生变化时，才在事件参数中携带 HDR 值
        var previousIsHDROn = IsHDROn;
        IsHDROn = GetHDRStatus();
        var changed = previousIsHDROn != IsHDROn;

        Changed?.Invoke(this, new() { HDR = changed ? IsHDROn : null });
    }

    /// <summary>
    /// 查询第一个显示器的 HDR 状态；查询失败（如设备不支持）时返回 null。
    /// </summary>
    /// <returns>HDR 是否开启；无法确定时为 null。</returns>
    private static bool? GetHDRStatus()
    {
        try
        {
            return Displays.Get().FirstOrDefault()?.GetAdvancedColorInfo().AdvancedColorEnabled;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to get HDR status. Assuming unavailable.", ex);
            return null;
        }
    }
}

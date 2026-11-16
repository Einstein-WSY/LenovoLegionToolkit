using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.WPF.Utils;

/// <summary>
/// 智能键（Fn+F9）处理帮助类：区分单双击，并执行对应的自动化操作或把主窗口置前。
/// </summary>
internal class SmartKeyHelper
{
    // 双击判定的时间窗口
    private readonly TimeSpan _smartKeyDoublePressInterval = TimeSpan.FromMilliseconds(500);

    private DateTime _lastSmartKeyPress = DateTime.MinValue;
    private CancellationTokenSource? _smartKeyDoublePressCancellationTokenSource;

    private readonly ApplicationSettings _settings = IoCContainer.Resolve<ApplicationSettings>();
    private readonly FnKeysDisabler _fnKeysDisabler = IoCContainer.Resolve<FnKeysDisabler>();
    private readonly SpecialKeyListener _specialKeyListener = IoCContainer.Resolve<SpecialKeyListener>();
    private readonly AutomationProcessor _automationProcessor = IoCContainer.Resolve<AutomationProcessor>();

    /// <summary>需要把主窗口置前时调用。</summary>
    public Action? BringToForeground { get; set; }

    private static SmartKeyHelper? _instance;

    /// <summary>单例实例。</summary>
    public static SmartKeyHelper Instance => _instance ??= new SmartKeyHelper();

    private SmartKeyHelper()
    {
        _specialKeyListener.Changed += SpecialKeyListener_Changed;
    }

    // 监听特殊键事件，仅处理 Fn+F9，并区分单击与双击
    private async void SpecialKeyListener_Changed(object? sender, SpecialKeyListener.ChangedEventArgs e)
    {
        if (e.SpecialKey != SpecialKey.FnF9)
            return;

        // 当系统 Fn 键已被禁用（由本应用接管）时忽略该事件
        if (await _fnKeysDisabler.GetStatusAsync() == SoftwareStatus.Enabled)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Ignoring Fn+F9 FnKeys are enabled.");

            return;
        }

        // 取消上一次等待中的判定，重新开始计时
        if (_smartKeyDoublePressCancellationTokenSource is not null)
            await _smartKeyDoublePressCancellationTokenSource.CancelAsync();
        _smartKeyDoublePressCancellationTokenSource = new CancellationTokenSource();

        var token = _smartKeyDoublePressCancellationTokenSource.Token;

        _ = Task.Run(async () =>
        {
            var now = DateTime.UtcNow;
            var diff = now - _lastSmartKeyPress;
            _lastSmartKeyPress = now;

            // 在时间窗口内再次按下视为双击，立即执行
            if (diff < _smartKeyDoublePressInterval)
            {
                await ProcessSpecialKey(true);
                return;
            }

            // 否则等待一个时间窗口确认不是双击，再按单击处理
            await Task.Delay(_smartKeyDoublePressInterval, token);
            await ProcessSpecialKey(false);
        }, token);
    }

    // 根据单击/双击读取对应设置并执行操作
    private async Task ProcessSpecialKey(bool isDoublePress)
    {
        var currentGuid = isDoublePress
            ? _settings.Store.SmartKeyDoublePressActionId
            : _settings.Store.SmartKeySinglePressActionId;
        var actionList = isDoublePress
            ? _settings.Store.SmartKeyDoublePressActionList
            : _settings.Store.SmartKeySinglePressActionList;

        // 未配置动作时，退化为把主窗口置前
        if (!currentGuid.HasValue)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Bringing to foreground after {(isDoublePress ? "double" : "single")} Fn+F9 press.");
            BringToForeground?.Invoke();
            return;
        }

        if (currentGuid.Value == Guid.Empty)
            return;

        if (actionList.IsEmpty())
            actionList.Add(currentGuid.Value);

        // 在动作列表中循环切换到下一个动作
        var currentIndex = Math.Max(0, actionList.IndexOf(currentGuid.Value));
        var nextIndex = (currentIndex + 1) % actionList.Count;

        currentGuid = actionList[currentIndex];

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Running action {currentGuid} after {(isDoublePress ? "double" : "single")} Fn+F9 press.");

        try
        {
            var pipelines = await _automationProcessor.GetPipelinesAsync();
            var pipeline = pipelines.FirstOrDefault(p => p.Id == currentGuid);
            if (pipeline is not null)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Running action {currentGuid} after {(isDoublePress ? "double" : "single")} Fn+F9 press.");

                await _automationProcessor.RunNowAsync(pipeline.Id);

                // 广播通知，让界面显示本次执行的动作
                MessagingCenter.Publish(new NotificationMessage(isDoublePress ? NotificationType.SmartKeyDoublePress : NotificationType.SmartKeySinglePress, pipeline.Name ?? string.Empty));
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Running action {currentGuid} after {(isDoublePress ? "double" : "single")} Fn+F9 press failed.", ex);
        }

        // 记录下一次要执行的动作，并持久化设置
        if (isDoublePress)
        {
            _settings.Store.SmartKeyDoublePressActionList = actionList;
            _settings.Store.SmartKeyDoublePressActionId = actionList[nextIndex];
        }
        else
        {
            _settings.Store.SmartKeySinglePressActionList = actionList;
            _settings.Store.SmartKeySinglePressActionId = actionList[nextIndex];
        }

        _settings.SynchronizeStore();
    }
}

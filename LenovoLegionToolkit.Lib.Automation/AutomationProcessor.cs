using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.AutoListeners;
using LenovoLegionToolkit.Lib.Automation.Pipeline;
using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;
using LenovoLegionToolkit.Lib.Automation.Utils;
using LenovoLegionToolkit.Lib.Controllers.GodMode;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Utils;
using NeoSmart.AsyncLock;

namespace LenovoLegionToolkit.Lib.Automation;

/// <summary>
/// 自动化流水线处理器：负责加载/持久化流水线、订阅各类系统监听器，
/// 并在监听到事件后按触发器条件依次执行匹配的流水线。
/// </summary>
public class AutomationProcessor(
    AutomationSettings settings,
    DisplayConfigurationListener displayConfigurationListener,
    NativeWindowsMessageListener nativeWindowsMessageListener,
    PowerStateListener powerStateListener,
    PowerModeListener powerModeListener,
    GodModeController godModeController,
    GameAutoListener gameAutoListener,
    ProcessAutoListener processAutoListener,
    SessionLockUnlockListener sessionLockUnlockListener,
    TimeAutoListener timeAutoListener,
    UserInactivityAutoListener userInactivityAutoListener,
    WiFiAutoListener wifiAutoListener)
{
    // _ioLock 保护 _pipelines 与配置读写的互斥；_runLock 保证同一时刻只有一次流水线运行，
    // 避免多个系统事件并发触发导致步骤交错执行。
    private readonly AsyncLock _ioLock = new();
    private readonly AsyncLock _runLock = new();

    // 内存中的流水线副本，所有对外返回均经 DeepCopy，防止外部修改内部状态。
    private List<AutomationPipeline> _pipelines = [];
    // 当前正在运行的流水线取消源，新事件到来时会取消上一轮尚未完成的运行。
    private CancellationTokenSource? _cts;

    /// <summary>
    /// 自动化功能是否已在设置中启用。
    /// </summary>
    public bool IsEnabled => settings.Store.IsEnabled;

    /// <summary>
    /// 流水线列表（或其启用状态）发生变化时触发，参数为深拷贝后的当前流水线集合。
    /// </summary>
    public event EventHandler<List<AutomationPipeline>>? PipelinesChanged;

    #region Initialization / pipeline reloading

    /// <summary>
    /// 初始化处理器：订阅常驻的系统级监听器，从设置加载流水线，并按需启停可动态订阅的 AutoListener。
    /// </summary>
    public async Task InitializeAsync()
    {
        using (await _ioLock.LockAsync().ConfigureAwait(false))
        {
            // 以下事件源本身开销较小且必须常驻，因此在此无条件订阅；
            // 游戏/进程/时间/空闲/Wi-Fi 等较重的监听器则交由 UpdateListenersAsync 按触发器需要动态订阅。
            displayConfigurationListener.Changed += DisplayConfigurationListener_Changed;
            nativeWindowsMessageListener.Changed += NativeWindowsMessageListener_Changed;
            powerStateListener.Changed += PowerStateListener_Changed;
            powerModeListener.Changed += PowerModeListener_Changed;
            godModeController.PresetChanged += GodModeController_PresetChanged;
            sessionLockUnlockListener.Changed += SessionLockUnlockListener_Changed;

            _pipelines = [.. settings.Store.Pipelines];

            RaisePipelinesChanged();

            await UpdateListenersAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 启用或停用自动化，并立即同步设置、刷新监听器订阅状态。
    /// </summary>
    /// <param name="enabled">是否启用自动化。</param>
    public async Task SetEnabledAsync(bool enabled)
    {
        using (await _ioLock.LockAsync().ConfigureAwait(false))
        {
            settings.Store.IsEnabled = enabled;
            settings.SynchronizeStore();

            await UpdateListenersAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 用新集合替换全部流水线：持久化到设置并重建内存副本，随后刷新监听器订阅。
    /// </summary>
    /// <param name="pipelines">新的流水线集合。</param>
    public async Task ReloadPipelinesAsync(List<AutomationPipeline> pipelines)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Pipelines reload pending...");

        using (await _ioLock.LockAsync().ConfigureAwait(false))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Pipelines reloading...");

            // 内存中保存深拷贝，界面持有一份独立副本，二者互不影响。
            _pipelines = pipelines.Select(p => p.DeepCopy()).ToList();

            settings.Store.Pipelines = pipelines;
            settings.SynchronizeStore();

            RaisePipelinesChanged();

            await UpdateListenersAsync().ConfigureAwait(false);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Pipelines reloaded.");
        }
    }

    /// <summary>
    /// 获取全部流水线的深拷贝，调用方可安全修改返回值。
    /// </summary>
    /// <returns>流水线列表副本。</returns>
    public async Task<List<AutomationPipeline>> GetPipelinesAsync()
    {
        using (await _ioLock.LockAsync().ConfigureAwait(false))
            return _pipelines.Select(p => p.DeepCopy()).ToList();
    }

    #endregion

    #region Run

    /// <summary>
    /// 在应用启动时触发一次“开机”事件，使监听启动条件的流水线得以执行。
    /// </summary>
    public void RunOnStartup()
    {
        if (!IsEnabled)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Not enabled. Pipeline run on startup ignored.");

            return;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Pipeline run on startup pending...");

        // 此处不等待结果：启动阶段不应阻塞调用方，异常由 ProcessEvent/RunAsync 内部记录。
        Task.Run(() => ProcessEvent(new StartupAutomationEvent()));
    }

    /// <summary>
    /// 立即手动执行指定流水线（忽略其触发器条件），执行期间独占运行锁。
    /// </summary>
    /// <param name="pipeline">要执行的流水线。</param>
    /// <exception cref="Exception">步骤执行失败时重新抛出原始异常。</exception>
    public async Task RunNowAsync(AutomationPipeline pipeline)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Pipeline run now pending...");

        using (await _runLock.LockAsync().ConfigureAwait(false))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Pipeline run starting...");

            try
            {
                List<AutomationPipeline> pipelines;
                using (await _ioLock.LockAsync().ConfigureAwait(false))
                    pipelines = _pipelines.ToList();

                var otherPipelines = pipelines.Where(p => p.Id != pipeline.Id).ToList();
                await pipeline.DeepCopy().RunAsync(otherPipelines).ConfigureAwait(false);

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Pipeline run finished successfully.");
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Pipeline run failed.", ex);

                throw;
            }
        }
    }

    /// <summary>
    /// 按 ID 立即执行流水线。仅对“无触发器”的手动流水线生效，带触发器的流水线会被忽略。
    /// </summary>
    /// <param name="pipelineId">流水线 ID。</param>
    public async Task RunNowAsync(Guid pipelineId)
    {
        AutomationPipeline? pipeline;
        using (await _ioLock.LockAsync().ConfigureAwait(false))
            // 只允许手动执行没有触发器的流水线，避免手动触发干扰自动规则。
            pipeline = _pipelines.Where(p => p.Trigger is null).FirstOrDefault(p => p.Id == pipelineId);

        if (pipeline is null)
            return;

        await RunNowAsync(pipeline).ConfigureAwait(false);
    }

    /// <summary>
    /// 处理一个自动化事件：按顺序执行所有触发器匹配的流水线。
    /// 新事件会取消上一轮未完成的运行，且遇到独占（IsExclusive）流水线执行完即停止。
    /// </summary>
    /// <param name="automationEvent">待处理的事件。</param>
    private async Task RunAsync(IAutomationEvent automationEvent)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Run pending...");

        using (await _runLock.LockAsync().ConfigureAwait(false))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Run starting...");

            // 取消上一轮仍在执行的流水线，保证“最新事件优先”，避免旧步骤与新状态冲突。
            if (_cts is not null)
                await _cts.CancelAsync().ConfigureAwait(false);

            if (!IsEnabled)
                return;

            List<AutomationPipeline> pipelines;
            using (await _ioLock.LockAsync().ConfigureAwait(false))
                pipelines = _pipelines.ToList();

            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            foreach (var pipeline in pipelines)
            {
                if (ct.IsCancellationRequested)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Run interrupted.");
                    break;
                }

                try
                {
                    // 无触发器的流水线只允许手动执行，不参与事件驱动的自动执行。
                    if (pipeline.Trigger is null || !await pipeline.Trigger.IsMatchingEvent(automationEvent).ConfigureAwait(false))
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Pipeline triggers not satisfied. [name={pipeline.Name}, trigger={pipeline.Trigger}, steps.Count={pipeline.Steps.Count}]");
                        continue;
                    }

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Running pipeline... [name={pipeline.Name}, trigger={pipeline.Trigger}, steps.Count={pipeline.Steps.Count}]");

                    var otherPipelines = pipelines.Where(p => p.Id != pipeline.Id).ToList();
                    await pipeline.RunAsync(otherPipelines, ct).ConfigureAwait(false);

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Pipeline completed successfully. [name={pipeline.Name}, trigger={pipeline.Trigger}]");
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Pipeline run failed. [name={pipeline.Name}, trigger={pipeline.Trigger}]", ex);
                }

                // 独占流水线视为本轮事件的最终处理者，执行后不再运行其余流水线，防止后续步骤覆盖其设置。
                if (pipeline.IsExclusive)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Pipeline is exclusive. Breaking. [name={pipeline.Name}, trigger={pipeline.Trigger}, steps.Count={pipeline.Steps.Count}]");
                    break;
                }
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Run finished successfully.");
        }
    }

    #endregion

    #region Listeners

    // 各监听器回调只负责把监听参数包装成 IAutomationEvent，是否执行由 ProcessEvent 统一判断。
    // 事件签名要求返回 void，因此使用 async void；真正的异常处理在 ProcessEvent/RunAsync 内部完成。
    private async void DisplayConfigurationListener_Changed(object? sender, DisplayConfigurationListener.ChangedEventArgs args)
    {
        var e = new HDRAutomationEvent(args.HDR);
        await ProcessEvent(e).ConfigureAwait(false);
    }

    private async void NativeWindowsMessageListener_Changed(object? sender, NativeWindowsMessageListener.ChangedEventArgs args)
    {
        var e = new NativeWindowsMessageEvent(args.Message, args.Data);
        await ProcessEvent(e).ConfigureAwait(false);
    }

    private async void PowerStateListener_Changed(object? sender, PowerStateListener.ChangedEventArgs args)
    {
        var e = new PowerStateAutomationEvent(args.PowerStateEvent, args.PowerAdapterStateChanged);
        await ProcessEvent(e).ConfigureAwait(false);
    }

    private async void PowerModeListener_Changed(object? sender, PowerModeListener.ChangedEventArgs args)
    {
        var e = new PowerModeAutomationEvent(args.State);
        await ProcessEvent(e).ConfigureAwait(false);
    }

    private async void GodModeController_PresetChanged(object? sender, Guid presetId)
    {
        var e = new CustomModePresetAutomationEvent(presetId);
        await ProcessEvent(e).ConfigureAwait(false);
    }

    private async void GameAutoListener_Changed(object? sender, GameAutoListener.ChangedEventArgs args)
    {
        var e = new GameAutomationEvent(args.Running);
        await ProcessEvent(e).ConfigureAwait(false);
    }

    private async void ProcessAutoListener_Changed(object? sender, ProcessAutoListener.ChangedEventArgs args)
    {
        var e = new ProcessAutomationEvent(args.Type, args.ProcessInfo);
        await ProcessEvent(e).ConfigureAwait(false);
    }

    private async void SessionLockUnlockListener_Changed(object? sender, SessionLockUnlockListener.ChangedEventArgs args)
    {
        var e = new SessionLockUnlockAutomationEvent(args.Locked);
        await ProcessEvent(e).ConfigureAwait(false);
    }

    private async void TimeAutoListener_Changed(object? sender, TimeAutoListener.ChangedEventArgs args)
    {
        var e = new TimeAutomationEvent(args.Time, args.Day);
        await ProcessEvent(e).ConfigureAwait(false);
    }

    private async void UserInactivityAutoListener_Changed(object? sender, UserInactivityAutoListener.ChangedEventArgs args)
    {
        var e = new UserInactivityAutomationEvent(args.TimerResolution * args.TickCount);
        await ProcessEvent(e).ConfigureAwait(false);
    }

    private async void WiFiAutoListener_Changed(object? sender, WiFiAutoListener.ChangedEventArgs args)
    {
        var e = new WiFiAutomationEvent(args.IsConnected, args.Ssid);
        await ProcessEvent(e).ConfigureAwait(false);
    }

    #endregion

    #region Event processing

    /// <summary>
    /// 事件入口：先做一次低成本的“是否存在潜在匹配”预判，命中后才进入完整的运行流程。
    /// </summary>
    /// <param name="e">待处理事件。</param>
    private async Task ProcessEvent(IAutomationEvent e)
    {
        // 预判只是筛选，真正是否执行仍由 RunAsync 中逐个流水线的 IsMatchingEvent 决定；
        // 目的是避免无关事件频繁进入加锁与日志分支。
        var potentialMatch = _pipelines.SelectMany(p => p.AllTriggers)
            .Select(async t => await t.IsMatchingEvent(e).ConfigureAwait(false))
            .Select(t => t.Result)
            .Where(t => t)
            .Any();

        if (!potentialMatch)
            return;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Processing event {e}... [type={e.GetType().Name}]");

        await RunAsync(e).ConfigureAwait(false);
    }

    #endregion

    #region Helper methods

    /// <summary>
    /// 根据当前流水线用到的触发器类型，重新计算需要订阅的 AutoListener 集合。
    /// 先全部退订再按需订阅，确保停用自动化或删除触发器后不会残留后台监听。
    /// </summary>
    private async Task UpdateListenersAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopping listeners...");

        await gameAutoListener.UnsubscribeChangedAsync(GameAutoListener_Changed).ConfigureAwait(false);
        await processAutoListener.UnsubscribeChangedAsync(ProcessAutoListener_Changed).ConfigureAwait(false);
        await timeAutoListener.UnsubscribeChangedAsync(TimeAutoListener_Changed).ConfigureAwait(false);
        await userInactivityAutoListener.UnsubscribeChangedAsync(UserInactivityAutoListener_Changed).ConfigureAwait(false);
        await wifiAutoListener.UnsubscribeChangedAsync(WiFiAutoListener_Changed).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopped listeners...");

        if (!IsEnabled)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Not enabled. Will not start listeners.");
            return;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Starting listeners...");

        // 汇总所有流水线的全部触发器（包括复合触发器的子项），用于判断哪类监听器是必需的。
        var triggers = _pipelines.SelectMany(p => p.AllTriggers).ToArray();

        if (triggers.OfType<IGameAutomationPipelineTrigger>().Any())
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Starting game listener...");

            await gameAutoListener.SubscribeChangedAsync(GameAutoListener_Changed).ConfigureAwait(false);
        }

        if (triggers.OfType<IProcessesAutomationPipelineTrigger>().Any())
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Starting process listener...");

            await processAutoListener.SubscribeChangedAsync(ProcessAutoListener_Changed).ConfigureAwait(false);
        }

        if (triggers.OfType<ITimeAutomationPipelineTrigger>().Any() || triggers.OfType<IPeriodicAutomationPipelineTrigger>().Any())
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Starting time listener...");

            await timeAutoListener.SubscribeChangedAsync(TimeAutoListener_Changed).ConfigureAwait(false);
        }

        if (triggers.OfType<IUserInactivityPipelineTrigger>().Any())
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Starting user inactivity listener...");

            await userInactivityAutoListener.SubscribeChangedAsync(UserInactivityAutoListener_Changed).ConfigureAwait(false);
        }

        if (triggers.OfType<IWiFiConnectedPipelineTrigger>().Any() || triggers.OfType<IWiFiDisconnectedPipelineTrigger>().Any())
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Starting WiFi listener...");

            await wifiAutoListener.SubscribeChangedAsync(WiFiAutoListener_Changed).ConfigureAwait(false);
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Started relevant listeners.");
    }

    /// <summary>
    /// 通知订阅者流水线集合已变化，参数为深拷贝，订阅者可安全持有。
    /// </summary>
    private void RaisePipelinesChanged()
    {
        PipelinesChanged?.Invoke(this, _pipelines.Select(p => p.DeepCopy()).ToList());
    }

    #endregion

}

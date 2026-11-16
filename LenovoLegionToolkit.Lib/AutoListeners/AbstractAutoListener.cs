using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Utils;
using NeoSmart.AsyncLock;

namespace LenovoLegionToolkit.Lib.AutoListeners;

/// <summary>
/// 自动化监听器基类：按"订阅者数量"懒启动/懒停止底层监听。
/// 只有当存在订阅者时才真正开始监听，最后一个订阅者移除后自动停止，避免常驻开销。
/// </summary>
/// <typeparam name="TEventArgs">事件参数类型。</typeparam>
public abstract class AbstractAutoListener<TEventArgs> : IAutoListener<TEventArgs> where TEventArgs : EventArgs
{
    private readonly AsyncLock _startStopLock = new(); // 串行化启动/停止，避免并发重复启停

    private bool _started; // 底层监听是否已启动

    private event EventHandler<TEventArgs>? Changed;

    /// <summary>
    /// 添加订阅者，并按需启动底层监听。
    /// </summary>
    /// <param name="eventHandler">事件处理委托。</param>
    public async Task SubscribeChangedAsync(EventHandler<TEventArgs> eventHandler)
    {
        Changed += eventHandler;
        await StartStopAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 移除订阅者，若无剩余订阅者则停止底层监听。
    /// </summary>
    /// <param name="eventHandler">要移除的事件处理委托。</param>
    public async Task UnsubscribeChangedAsync(EventHandler<TEventArgs> eventHandler)
    {
        Changed -= eventHandler;
        await StartStopAsync().ConfigureAwait(false);
    }

    private async Task StartStopAsync()
    {
        using (await _startStopLock.LockAsync().ConfigureAwait(false))
        {
            var subscribers = Changed?.GetInvocationList().Length ?? 0;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Subscribers: {subscribers}. [type={GetType().Name}]");

            // 有订阅者则启动，否则停止
            if (subscribers > 0)
                await StartInternalAsync().ConfigureAwait(false);
            else
                await StopInternalAsync().ConfigureAwait(false);
        }
    }

    private async Task StartInternalAsync()
    {
        if (_started)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Already started. [type={GetType().Name}]");

            return;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Starting... [type={GetType().Name}]");

        await StartAsync().ConfigureAwait(false);

        _started = true;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Started. [type={GetType().Name}]");
    }

    private async Task StopInternalAsync()
    {
        if (!_started)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Already stopped. [type={GetType().Name}]");

            return;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopping... [type={GetType().Name}]");

        await StopAsync().ConfigureAwait(false);

        _started = false;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopped. [type={GetType().Name}]");
    }

    /// <summary>
    /// 启动底层监听。子类在此注册系统事件或开启轮询。
    /// </summary>
    protected abstract Task StartAsync();

    /// <summary>
    /// 停止底层监听并释放相关订阅。
    /// </summary>
    protected abstract Task StopAsync();

    /// <summary>
    /// 向所有订阅者广播变化事件。
    /// </summary>
    /// <param name="value">事件参数。</param>
    protected void RaiseChanged(TEventArgs value) => Changed?.Invoke(this, value);
}

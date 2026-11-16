using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Listeners;

/// <summary>
/// WMI 监听器基类：通过工厂委托 <paramref name="listen"/> 建立底层订阅（通常是 WMI 事件查询），
/// 收到原始事件后依次转换为值与事件参数，执行派生类处理逻辑并触发 <see cref="Changed"/> 事件。
/// </summary>
/// <typeparam name="TEventArgs">对外触发的事件参数类型。</typeparam>
/// <typeparam name="TValue">由原始数据解析出的中间值类型。</typeparam>
/// <typeparam name="TRawValue">底层订阅回调提供的原始数据类型。</typeparam>
/// <param name="listen">订阅工厂：接收事件处理回调，返回用于退订的 <see cref="IDisposable"/>。</param>
public abstract class AbstractWMIListener<TEventArgs, TValue, TRawValue>(Func<Action<TRawValue>, IDisposable> listen)
    : IListener<TEventArgs>
    where TEventArgs : EventArgs
{
    private IDisposable? _disposable;

    /// <summary>
    /// 底层事件处理完成后触发。
    /// </summary>
    public event EventHandler<TEventArgs>? Changed;

    /// <inheritdoc/>
    public Task StartAsync()
    {
        try
        {
            // 幂等启动：已持有订阅句柄说明监听器已在运行，直接返回避免重复订阅
            if (_disposable is not null)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Already started. [listener={GetType().Name}]");
                return Task.CompletedTask;
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Starting... [listener={GetType().Name}]");

            // 建立底层订阅（如 ManagementEventWatcher），保存返回的句柄以便停止时退订
            _disposable = listen(Handler);
        }
        catch (Exception ex)
        {
            // 启动失败不向上抛出（例如 WMI 不可用），仅记录日志以保证应用其余功能可用
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't start listener. [listener={GetType().Name}]", ex);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync()
    {
        try
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Stopping... [listener={GetType().Name}]");

            // 释放订阅句柄以退订 WMI 事件，并置空以便下次可重新启动
            _disposable?.Dispose();
            _disposable = null;
        }
        catch (Exception ex)
        {
            // 停止失败同样吞掉异常，避免影响整体停机流程
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't stop listener. [listener={GetType().Name}]", ex);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 将底层原始数据解析为中间值。
    /// </summary>
    /// <param name="value">底层回调提供的原始数据。</param>
    /// <returns>解析后的中间值。</returns>
    protected abstract TValue GetValue(TRawValue value);

    /// <summary>
    /// 将中间值转换为对外事件参数。
    /// </summary>
    /// <param name="value">解析后的中间值。</param>
    /// <returns>事件参数实例。</returns>
    protected abstract TEventArgs GetEventArgs(TValue value);

    /// <summary>
    /// 派生类的业务处理逻辑，在 <see cref="Changed"/> 触发之前调用。
    /// </summary>
    /// <param name="value">解析后的中间值。</param>
    /// <returns>表示处理完成的任务。</returns>
    protected abstract Task OnChangedAsync(TValue value);

    /// <summary>
    /// 触发 <see cref="Changed"/> 事件，供派生类在合适时机（如防抖处理后）通知外部。
    /// </summary>
    /// <param name="value">解析后的中间值，用于构造事件参数。</param>
    protected void RaiseChanged(TValue value) => Changed?.Invoke(this, GetEventArgs(value));

    // async void 仅用于匹配订阅回调签名；异常统一吞掉并记录，防止 WMI 回调线程上的未处理异常导致进程退出
    private async void Handler(TRawValue properties)
    {
        try
        {
            var value = GetValue(properties);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Event received. [value={value}, listener={GetType().Name}]");

            await OnChangedAsync(value).ConfigureAwait(false);
            RaiseChanged(value);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to handle event.  [listener={GetType().Name}]", ex);
        }
    }
}

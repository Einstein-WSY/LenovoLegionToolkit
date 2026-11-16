using System;
using System.Threading.Tasks;
using NeoSmart.AsyncLock;

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// “首次触发”节流器：在给定时间间隔内只允许第一次调用真正执行，后续调用被丢弃。
/// </summary>
/// <param name="interval">两次允许执行之间的最小时间间隔。</param>
/// <param name="tag">用于日志标记的名称。</param>
public class ThrottleFirstDispatcher(TimeSpan interval, string? tag = null)
{
    private readonly AsyncLock _lock = new();

    private DateTime _lastEvent = DateTime.MinValue;

    /// <summary>
    /// 若距上次实际执行已超过间隔则执行任务，否则直接返回（被节流）。
    /// </summary>
    /// <param name="task">需要执行的异步任务。</param>
    public async Task DispatchAsync(Func<Task> task)
    {
        using (await _lock.LockAsync().ConfigureAwait(false))
        {
            var diff = DateTime.UtcNow - _lastEvent;

            // 距上次执行未超过间隔，丢弃本次调用。
            if (diff < interval)
            {
                if (tag is not null && Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Throttling... [tag={tag}, diff={diff.TotalMilliseconds}ms]");

                return;
            }

            if (tag is not null && Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Allowing... [tag={tag}, diff={diff.TotalMilliseconds}ms]");

            await task().ConfigureAwait(false);

            _lastEvent = DateTime.UtcNow;
        }
    }
}

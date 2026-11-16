using System;
using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// “最后一次触发”节流器：在静默一段时间后只执行最后一次调用，期间的中间调用会被取消。
/// </summary>
/// <param name="interval">要求的静默间隔。</param>
/// <param name="tag">用于日志标记的名称。</param>
public class ThrottleLastDispatcher(TimeSpan interval, string? tag = null)
{
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// 安排一次延迟执行；若在间隔内再次调用，前一次待执行任务会被取消，只保留最后一次。
    /// </summary>
    /// <param name="task">需要执行的异步任务。</param>
    public async Task DispatchAsync(Func<Task> task)
    {
        try
        {
            // 取消上一次尚未执行的任务，实现“只保留最后一次”的语义。
            if (_cancellationTokenSource is not null)
                await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);

            _cancellationTokenSource = new();

            var token = _cancellationTokenSource.Token;

            await Task.Delay(interval, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();

            if (tag is not null && Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Allowing... [tag={tag}]");

            await task().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (tag is not null && Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Throttling... [tag={tag}]");
        }
    }
}

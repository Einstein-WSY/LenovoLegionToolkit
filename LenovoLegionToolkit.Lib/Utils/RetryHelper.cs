using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// 在达到最大重试次数后仍失败时抛出的异常。
/// </summary>
public class MaximumRetriesReachedException : Exception;

/// <summary>
/// 提供带次数与间隔的异步重试辅助方法。
/// </summary>
public static class RetryHelper
{
    /// <summary>
    /// 反复执行给定异步操作，直到成功或达到最大重试次数。
    /// </summary>
    /// <param name="action">需要执行的异步操作。</param>
    /// <param name="maximumRetries">最大重试次数，未指定时默认 3 次。</param>
    /// <param name="timeout">每次重试之间的等待间隔，未指定时默认为零。</param>
    /// <param name="matchingException">判断异常是否需要重试；返回 false 时立即向上抛出。</param>
    /// <param name="tag">用于日志标记的调用方名称。</param>
    /// <exception cref="MaximumRetriesReachedException">重试次数用尽后仍未成功时抛出。</exception>
    public static async Task RetryAsync(Func<Task> action,
        int? maximumRetries = null,
        TimeSpan? timeout = null,
        Func<Exception, bool>? matchingException = null,
        [CallerMemberName] string? tag = null)
    {
        maximumRetries ??= 3;
        timeout ??= TimeSpan.Zero;
        matchingException ??= (_) => true;

        var retries = 0;
        var success = false;

        while (!success)
        {
            // 次数检查必须在 try 之外，否则该异常会被下面的 catch 捕获导致无限循环
            if (retries >= maximumRetries)
                throw new MaximumRetriesReachedException();

            try
            {
                await action().ConfigureAwait(false);
                success = true;
            }
            catch (Exception ex)
            {
                // 仅对匹配的异常进行重试，其余异常直接冒泡到调用方。
                if (!matchingException(ex))
                    throw;

                retries++;

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Retrying {retries}/{maximumRetries}... [tag={tag}]");

                await Task.Delay(timeout.Value).ConfigureAwait(false);
            }
        }
    }
}

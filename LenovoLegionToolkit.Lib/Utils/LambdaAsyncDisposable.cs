using System;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// 将一段异步清理逻辑包装为 <see cref="IAsyncDisposable"/>，便于在 using 语句中托管资源。
/// </summary>
/// <param name="action">释放时执行的异步清理委托。</param>
public class LambdaAsyncDisposable(Func<Task> action) : IAsyncDisposable
{
    /// <summary>
    /// 异步释放资源，执行构造时传入的清理委托。
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await action().ConfigureAwait(false);
    }
}

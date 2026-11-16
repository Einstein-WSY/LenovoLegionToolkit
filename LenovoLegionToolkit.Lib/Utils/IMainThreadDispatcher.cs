using System;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// 主线程调度器抽象，用于把回调切换到 UI 主线程执行。
/// </summary>
public interface IMainThreadDispatcher
{
    /// <summary>
    /// 将同步回调调度到主线程执行。
    /// </summary>
    /// <param name="callback">待执行的回调。</param>
    void Dispatch(Action callback);

    /// <summary>
    /// 将异步回调调度到主线程执行。
    /// </summary>
    /// <param name="callback">返回异步任务的回调。</param>
    /// <returns>回调完成时结束的任务。</returns>
    Task DispatchAsync(Func<Task> callback);
}

using System;
using System.Threading.Tasks;
using System.Windows;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.WPF.Utils;

/// <summary>
/// 主线程调度器的 WPF 实现，将回调切回应用 UI 线程执行。
/// </summary>
public class MainThreadDispatcher : IMainThreadDispatcher
{
    /// <summary>同步在 UI 线程执行回调。</summary>
    /// <param name="callback">待执行的回调。</param>
    public void Dispatch(Action callback) => Application.Current.Dispatcher.Invoke(callback);

    /// <summary>异步在 UI 线程执行回调。</summary>
    /// <param name="callback">返回任务的异步回调。</param>
    /// <returns>回调返回的任务。</returns>
    public Task DispatchAsync(Func<Task> callback) => Application.Current.Dispatcher.Invoke(callback);
}

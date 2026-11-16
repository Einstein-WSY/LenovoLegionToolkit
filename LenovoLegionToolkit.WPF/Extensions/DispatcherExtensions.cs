using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace LenovoLegionToolkit.WPF.Extensions;

/// <summary>
/// 提供 <see cref="Dispatcher"/> 相关的扩展方法。
/// </summary>
public static class DispatcherExtensions
{
    /// <summary>
    /// 在 UI 线程上以即发即忘的方式执行异步委托。
    /// </summary>
    /// <param name="dispatcher">目标调度器。</param>
    /// <param name="action">要执行的异步委托。</param>
    public static void InvokeTask(this Dispatcher dispatcher, Func<Task> action) => dispatcher.Invoke(async () => await action());
}

using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供 <see cref="Task"/> 的扩展方法。
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// 将 <see cref="Task"/> 包装为 <see cref="ValueTask"/>。
    /// <para>
    /// 常用于需要返回 <see cref="ValueTask"/> 的委托（如 Autofac 的激活回调）。
    /// </para>
    /// </summary>
    /// <param name="task">原始任务。</param>
    /// <returns>包装后的 <see cref="ValueTask"/>。</returns>
    public static ValueTask AsValueTask(this Task task) => new(task);

    /// <summary>
    /// 将任务结果转换为可空类型：任务成功则返回结果，否则（异常/取消）返回 <c>null</c>。
    /// </summary>
    /// <typeparam name="T">结果值类型。</typeparam>
    /// <param name="task">原始任务。</param>
    /// <returns>结果或 <c>null</c> 的任务。</returns>
    public static Task<T?> OrNullIfException<T>(this Task<T> task) where T : struct
    {
        // ContinueWith 会在任务完成后执行，根据是否成功决定返回结果还是 null，从而吞掉异常
        return task.ContinueWith(t => t.IsCompletedSuccessfully ? (T?)t.Result : null);
    }
}

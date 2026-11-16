using System;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Listeners;

/// <summary>
/// 可主动通知的监听器接口：除被动接收底层事件外，还允许外部主动推送一个值，
/// 使监听器按收到底层事件的方式处理（常用于外部组件强制刷新状态或模拟事件）。
/// </summary>
/// <typeparam name="TEventArgs">事件参数类型。</typeparam>
/// <typeparam name="TValue">外部主动通知时传入的原始值类型。</typeparam>
public interface INotifyingListener<TEventArgs, in TValue> : IListener<TEventArgs> where TEventArgs : EventArgs
{
    /// <summary>
    /// 由外部主动通知监听器某个值发生变化，监听器内部会按事件到达的路径处理该值。
    /// </summary>
    /// <param name="value">要通知的值。</param>
    /// <returns>表示通知处理完成的任务。</returns>
    Task NotifyAsync(TValue value);
}

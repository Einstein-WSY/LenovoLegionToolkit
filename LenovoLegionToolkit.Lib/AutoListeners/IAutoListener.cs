using System;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.AutoListeners;

/// <summary>
/// 自动化监听器的通用接口：订阅/取消订阅状态变化事件。
/// </summary>
/// <typeparam name="T">事件参数类型。</typeparam>
public interface IAutoListener<T> where T : EventArgs
{
    /// <summary>
    /// 订阅变化事件；首次订阅时自动启动底层监听。
    /// </summary>
    /// <param name="eventHandler">事件处理委托。</param>
    Task SubscribeChangedAsync(EventHandler<T> eventHandler);

    /// <summary>
    /// 取消订阅；取消最后一个订阅者时自动停止底层监听以节省资源。
    /// </summary>
    /// <param name="eventHandler">要移除的事件处理委托。</param>
    Task UnsubscribeChangedAsync(EventHandler<T> eventHandler);
}

using System;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using PubSub;

namespace LenovoLegionToolkit.Lib.Messaging;

/// <summary>
/// 应用内消息中心，基于 PubSub 库实现的轻量发布/订阅机制。
/// <para>
/// 各模块通过发布消息解耦通信，无需直接引用彼此；消息类型需实现 <see cref="IMessage"/>。
/// </para>
/// </summary>
public static class MessagingCenter
{
    /// <summary>
    /// 发布一条消息，所有订阅了该消息类型的处理器都会被调用。
    /// </summary>
    /// <typeparam name="T">消息类型。</typeparam>
    /// <param name="data">消息内容。</param>
    public static void Publish<T>(T data) where T : IMessage => Hub.Default.Publish(data);

    /// <summary>
    /// 订阅指定类型的消息，并注册带消息参数的处理器。
    /// </summary>
    /// <typeparam name="T">消息类型。</typeparam>
    /// <param name="subscriber">订阅者对象，通常在销毁时用于取消订阅。</param>
    /// <param name="handler">消息处理回调。</param>
    public static void Subscribe<T>(object subscriber, Action<T> handler) where T : IMessage => Hub.Default.Subscribe(subscriber, handler);

    /// <summary>
    /// 订阅指定类型的消息，并注册不关心消息内容的处理器。
    /// </summary>
    /// <typeparam name="T">消息类型。</typeparam>
    /// <param name="subscriber">订阅者对象。</param>
    /// <param name="handler">无参处理回调。</param>
    public static void Subscribe<T>(object subscriber, Action handler) where T : IMessage => Hub.Default.Subscribe<T>(subscriber, _ => handler());
}

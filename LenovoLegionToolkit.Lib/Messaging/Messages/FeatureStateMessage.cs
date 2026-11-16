namespace LenovoLegionToolkit.Lib.Messaging.Messages;

/// <summary>
/// 表示某项功能状态变化的消息。
/// <para>
/// 通过泛型 <typeparamref name="T"/> 区分不同功能的状态数据类型，
/// 例如 <c>FeatureStateMessage&lt;bool&gt;</c> 可用于广播开关状态的变化。
/// </para>
/// </summary>
/// <typeparam name="T">状态的数据类型。</typeparam>
public readonly struct FeatureStateMessage<T>(T state) : IMessage
{
    /// <summary>
    /// 当前的功能状态值。
    /// </summary>
    public T State { get; } = state;
}

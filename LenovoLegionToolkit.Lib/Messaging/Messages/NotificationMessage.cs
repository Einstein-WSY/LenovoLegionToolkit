namespace LenovoLegionToolkit.Lib.Messaging.Messages;

/// <summary>
/// 表示一条通知消息，携带通知类型及格式化参数。
/// </summary>
/// <param name="type">通知类型。</param>
/// <param name="args">用于生成通知文本的参数。</param>
public readonly struct NotificationMessage(NotificationType type, params object[] args) : IMessage
{
    /// <summary>
    /// 通知类型。
    /// </summary>
    public NotificationType Type { get; } = type;

    /// <summary>
    /// 生成通知文本所需的参数。
    /// </summary>
    public object[] Args { get; } = args;

    /// <summary>
    /// 返回便于日志输出的消息描述。
    /// </summary>
    /// <returns>包含通知类型与参数的字符串。</returns>
    public override string ToString() => $@"{nameof(Type)}: {Type}, {nameof(Args)}: [{string.Join(", ", Args)}]";
}

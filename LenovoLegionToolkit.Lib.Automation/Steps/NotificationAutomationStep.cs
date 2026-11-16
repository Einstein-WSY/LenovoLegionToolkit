using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：发送一条桌面通知；文本中的 <c>$RUN_OUTPUT$</c> 会被替换为上一步骤的输出。
/// </summary>
/// <param name="text">通知文本，可包含 <c>$RUN_OUTPUT$</c> 占位符。</param>
[method: JsonConstructor]
public class NotificationAutomationStep(string? text)
    : IAutomationStep
{
    /// <summary>通知文本。</summary>
    public string? Text { get; } = text;

    /// <summary>该步骤始终受支持。</summary>
    /// <returns>始终返回 <see langword="true"/>。</returns>
    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    /// <summary>执行步骤：文本非空时发布一条自动化通知消息。</summary>
    /// <param name="context">流水线执行上下文，提供上一步骤的输出。</param>
    /// <param name="environment">环境变量集合。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>表示操作已完成的任务。</returns>
    public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        // 空白文本不发送通知
        if (!string.IsNullOrWhiteSpace(Text))
        {
            // 把占位符替换为上一步骤（如脚本）的输出，便于反馈执行结果
            var text = Text.Replace("$RUN_OUTPUT$", context.LastRunOutput);
            MessagingCenter.Publish(new NotificationMessage(NotificationType.AutomationNotification, text));
        }

        return Task.CompletedTask;
    }

    /// <summary>创建该步骤的深拷贝，保留通知文本。</summary>
    /// <returns>新的步骤实例。</returns>
    IAutomationStep IAutomationStep.DeepCopy() => new NotificationAutomationStep(Text);
}

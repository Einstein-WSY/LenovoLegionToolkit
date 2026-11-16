using System.Media;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：播放指定路径的音频文件（例如自定义提示音）。
/// </summary>
/// <param name="path">音频文件路径；为 <see langword="null"/> 时该步骤直接跳过。</param>
[method: JsonConstructor]
public class PlaySoundAutomationStep(string? path) : IAutomationStep
{
    /// <summary>音频文件路径。</summary>
    public string? Path { get; } = path;

    /// <summary>该步骤始终受支持。</summary>
    /// <returns>始终返回 <see langword="true"/>。</returns>
    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    /// <summary>
    /// 执行步骤：在后台线程同步播放音频，播放结束后步骤才返回。
    /// </summary>
    /// <param name="context">流水线执行上下文。</param>
    /// <param name="environment">环境变量集合。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>播放完成（或跳过）的任务。</returns>
    public Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        // 未配置音频路径时静默跳过，不视为错误
        if (Path is null)
            return Task.CompletedTask;

        // 放到线程池执行，避免 PlaySync 阻塞流水线所在线程
        return Task.Run(() =>
        {
            var player = new SoundPlayer(Path);
            player.PlaySync();
        }, token);
    }

    /// <summary>创建该步骤的深拷贝，保留音频路径。</summary>
    /// <returns>新的步骤实例。</returns>
    public IAutomationStep DeepCopy() => new PlaySoundAutomationStep(Path);
}

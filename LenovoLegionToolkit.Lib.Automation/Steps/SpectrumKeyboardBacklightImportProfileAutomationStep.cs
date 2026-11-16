using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：从文件导入 Spectrum 键盘灯效配置并立即生效。
/// </summary>
/// <param name="path">灯效配置文件路径；为 <see langword="null"/> 时该步骤跳过。</param>
[method: JsonConstructor]
public class SpectrumKeyboardBacklightImportProfileAutomationStep(string? path)
    : IAutomationStep
{
    private readonly SpectrumKeyboardBacklightController _controller = IoCContainer.Resolve<SpectrumKeyboardBacklightController>();

    /// <summary>灯效配置文件路径。</summary>
    public string? Path { get; } = path;

    /// <summary>判断设备是否支持 Spectrum 键盘灯效。</summary>
    /// <returns>支持时返回 <see langword="true"/>。</returns>
    public Task<bool> IsSupportedAsync() => _controller.IsSupportedAsync();

    /// <summary>
    /// 执行步骤：读取当前灯效配置，把文件内容导入其中，然后通知界面刷新。
    /// </summary>
    /// <param name="context">流水线执行上下文。</param>
    /// <param name="environment">环境变量集合。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>导入完成的任务。</returns>
    public async Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        // 未配置路径或设备不支持 Spectrum 灯效时直接跳过
        if (Path is null || !await _controller.IsSupportedAsync().ConfigureAwait(false))
            return;

        // 导入基于现有配置进行，因此先读取当前配置
        var profile = await _controller.GetProfileAsync().ConfigureAwait(false);
        await _controller.ImportProfileDescription(profile, Path).ConfigureAwait(false);

        // 通知界面刷新灯效相关显示
        MessagingCenter.Publish(new SpectrumBacklightChangedMessage());
    }

    /// <summary>创建该步骤的深拷贝，保留配置文件路径。</summary>
    /// <returns>新的步骤实例。</returns>
    IAutomationStep IAutomationStep.DeepCopy() => new SpectrumKeyboardBacklightImportProfileAutomationStep(Path);
}

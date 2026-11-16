using System;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：切换 RGB 键盘背光灯效预设。
/// </summary>
/// <param name="state">目标灯效预设。</param>
public class RGBKeyboardBacklightAutomationStep(RGBKeyboardBacklightPreset state)
    : IAutomationStep<RGBKeyboardBacklightPreset>
{
    private readonly RGBKeyboardBacklightController _controller = IoCContainer.Resolve<RGBKeyboardBacklightController>();

    /// <summary>目标灯效预设。</summary>
    public RGBKeyboardBacklightPreset State { get; } = state;

    /// <summary>获取全部可选灯效预设。</summary>
    /// <returns>枚举中的所有预设值。</returns>
    public Task<RGBKeyboardBacklightPreset[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<RGBKeyboardBacklightPreset>());

    /// <summary>判断设备是否支持 RGB 键盘背光。</summary>
    /// <returns>支持时返回 <see langword="true"/>。</returns>
    public Task<bool> IsSupportedAsync() => _controller.IsSupportedAsync();

    /// <summary>
    /// 执行步骤：先声明灯效控制权，再写入预设，最后广播灯效变更消息。
    /// </summary>
    /// <param name="context">流水线执行上下文。</param>
    /// <param name="environment">环境变量集合。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>预设应用完成的任务。</returns>
    public async Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        // 设备不支持 RGB 键盘时跳过
        if (!await _controller.IsSupportedAsync().ConfigureAwait(false))
            return;

        // 先向固件声明灯效控制权，否则写入的预设可能不生效
        await _controller.SetLightControlOwnerAsync(true).ConfigureAwait(false);
        await _controller.SetPresetAsync(State).ConfigureAwait(false);

        MessagingCenter.Publish(new RGBKeyboardBacklightChangedMessage());
    }

    /// <summary>创建该步骤的深拷贝，保留目标预设。</summary>
    /// <returns>新的步骤实例。</returns>
    IAutomationStep IAutomationStep.DeepCopy() => new RGBKeyboardBacklightAutomationStep(State);
}

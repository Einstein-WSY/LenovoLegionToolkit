using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Steps;

/// <summary>
/// 自动化步骤：设置 Spectrum 键盘背光亮度档位（0-9）。
/// </summary>
/// <param name="state">目标亮度档位。</param>
[method: JsonConstructor]
public class SpectrumKeyboardBacklightBrightnessAutomationStep(int state)
    : IAutomationStep<int>
{
    private readonly SpectrumKeyboardBacklightController _controller = IoCContainer.Resolve<SpectrumKeyboardBacklightController>();

    // 亮度档位固定为 0-9
    private readonly int[] _allStates = Enumerable.Range(0, 10).ToArray();

    /// <summary>目标亮度档位。</summary>
    public int State { get; } = state;

    /// <summary>获取全部可选亮度档位。</summary>
    /// <returns>0-9 的档位数组。</returns>
    public Task<int[]> GetAllStatesAsync() => Task.FromResult(_allStates);

    /// <summary>判断设备是否支持 Spectrum 键盘灯效。</summary>
    /// <returns>支持时返回 <see langword="true"/>。</returns>
    public Task<bool> IsSupportedAsync() => _controller.IsSupportedAsync();

    /// <summary>
    /// 执行步骤：校验档位后写入亮度，并广播灯效变更消息。
    /// </summary>
    /// <param name="context">流水线执行上下文。</param>
    /// <param name="environment">环境变量集合。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>亮度设置完成的任务。</returns>
    /// <exception cref="InvalidOperationException">当状态值超出 0-9 范围时抛出。</exception>
    public async Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        // 设备不支持 Spectrum 灯效时跳过
        if (!await _controller.IsSupportedAsync().ConfigureAwait(false))
            return;

        // 越界校验：避免向固件写入非法档位
        if (!_allStates.Contains(State))
            throw new InvalidOperationException(nameof(State));

        await _controller.SetBrightnessAsync(State).ConfigureAwait(false);

        MessagingCenter.Publish(new SpectrumBacklightChangedMessage());
    }

    /// <summary>创建该步骤的深拷贝，保留目标档位。</summary>
    /// <returns>新的步骤实例。</returns>
    public IAutomationStep DeepCopy() => new SpectrumKeyboardBacklightBrightnessAutomationStep(State);
}

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
/// 自动化步骤：切换 Spectrum 键盘灯效配置档位（1-6）。
/// </summary>
/// <param name="state">目标配置档位。</param>
[method: JsonConstructor]
public class SpectrumKeyboardBacklightProfileAutomationStep(int state)
    : IAutomationStep<int>
{
    private readonly SpectrumKeyboardBacklightController _controller = IoCContainer.Resolve<SpectrumKeyboardBacklightController>();

    // 配置档位固定为 1-6
    private readonly int[] _allStates = Enumerable.Range(1, 6).ToArray();

    /// <summary>目标配置档位。</summary>
    public int State { get; } = state;

    /// <summary>获取全部可选配置档位。</summary>
    /// <returns>1-6 的档位数组。</returns>
    public Task<int[]> GetAllStatesAsync() => Task.FromResult(_allStates);

    /// <summary>判断设备是否支持 Spectrum 键盘灯效。</summary>
    /// <returns>支持时返回 <see langword="true"/>。</returns>
    public Task<bool> IsSupportedAsync() => _controller.IsSupportedAsync();

    /// <summary>
    /// 执行步骤：校验档位后应用配置，并广播灯效变更消息。
    /// </summary>
    /// <param name="context">流水线执行上下文。</param>
    /// <param name="environment">环境变量集合。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>档位切换完成的任务。</returns>
    /// <exception cref="InvalidOperationException">当状态值超出 1-6 范围时抛出。</exception>
    public async Task RunAsync(AutomationContext context, AutomationEnvironment environment, CancellationToken token)
    {
        // 设备不支持 Spectrum 灯效时跳过
        if (!await _controller.IsSupportedAsync().ConfigureAwait(false))
            return;

        // 越界校验：避免向固件写入非法档位
        if (!_allStates.Contains(State))
            throw new InvalidOperationException(nameof(State));

        await _controller.SetProfileAsync(State).ConfigureAwait(false);

        MessagingCenter.Publish(new SpectrumBacklightChangedMessage());
    }

    /// <summary>创建该步骤的深拷贝，保留目标档位。</summary>
    /// <returns>新的步骤实例。</returns>
    public IAutomationStep DeepCopy() => new SpectrumKeyboardBacklightProfileAutomationStep(State);
}

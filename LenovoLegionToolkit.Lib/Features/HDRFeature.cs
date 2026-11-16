using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// 内置显示屏 HDR 特性。
/// 通过 Windows Advanced Color API 读取/设置内屏 HDR（高动态范围）状态。
/// </summary>
public class HDRFeature : IFeature<HDRState>
{
    /// <inheritdoc/>
    public Task<bool> IsSupportedAsync()
    {
        try
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Checking HDR support...");

            var display = InternalDisplay.Get();
            if (display is null)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Built in display not found");

                return Task.FromResult(false);
            }

            // AdvancedColorSupported 表示该显示屏硬件与驱动支持高级颜色（HDR/WCG）
            var isSupported = display.GetAdvancedColorInfo().AdvancedColorSupported;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"HDR support: {isSupported}");

            return Task.FromResult(isSupported);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to check HDR support", ex);

            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// 判断 HDR 是否被外部因素强制禁用（例如由其他应用或驱动策略强制关闭）。
    /// </summary>
    /// <returns>被强制禁用返回 <c>true</c>。</returns>
    /// <exception cref="InvalidOperationException">找不到内置显示屏时抛出。</exception>
    public Task<bool> IsHdrBlockedAsync()
    {
        var display = InternalDisplay.Get() ?? throw new InvalidOperationException("Built in display not found");

        var result = display.GetAdvancedColorInfo().AdvancedColorForceDisabled;
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<HDRState[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<HDRState>());

    /// <inheritdoc/>
    public Task<HDRState> GetStateAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting current HDR state...");

        var display = InternalDisplay.Get() ?? throw new InvalidOperationException("Built in display not found");

        var result = display.GetAdvancedColorInfo().AdvancedColorEnabled ? HDRState.On : HDRState.Off;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"HDR is {result}");

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public async Task SetStateAsync(HDRState state)
    {
        var currentState = await GetStateAsync().ConfigureAwait(false);

        // 状态一致时不做任何操作，避免屏幕闪黑
        if (currentState == state)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"HDR already set to {state}");
            return;
        }

        var display = InternalDisplay.Get() ?? throw new InvalidOperationException("Built in display not found");

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting display HDR to {state}");

        display.SetAdvancedColorState(state == HDRState.On);
    }
}

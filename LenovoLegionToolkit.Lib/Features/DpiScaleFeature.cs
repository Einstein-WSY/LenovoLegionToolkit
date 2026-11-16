using System;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using WindowsDisplayAPI.Native.DisplayConfig;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// 内置显示屏 DPI 缩放特性。
/// 通过 Windows 显示配置 API（WindowsDisplayAPI）读写内屏的 DPI 缩放比例。
/// 仅操作内置显示器，外接屏不受影响。
/// </summary>
public class DpiScaleFeature : IFeature<DpiScale>
{
    /// <inheritdoc/>
    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    /// <inheritdoc/>
    public Task<DpiScale[]> GetAllStatesAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting all DPI scales...");

        var display = InternalDisplay.Get();
        var pds = display?.ToPathDisplaySource();
        if (pds is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Built in display not found");

            return Task.FromResult(Array.Empty<DpiScale>());
        }

        var max = (int)pds.MaximumDPIScale;

        // 枚举 Windows 预定义的缩放档位，过滤掉超过该屏幕最大值的档位，再升序返回
        var result = Enum.GetValues<DisplayConfigSourceDPIScale>()
            .Select(s => (int)s)
            .Where(s => s <= max)
            .OrderBy(s => s)
            .Select(s => new DpiScale(s))
            .ToArray();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Current DPI scale is {result}");

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<DpiScale> GetStateAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting current DPI scale...");

        var display = InternalDisplay.Get();
        var pds = display?.ToPathDisplaySource();
        if (pds is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Built in display not found");

            return Task.FromResult(default(DpiScale));
        }

        var result = (int)pds.CurrentDPIScale;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Current DPI scale is {result}");

        return Task.FromResult(new DpiScale(result));
    }

    /// <inheritdoc/>
    public Task SetStateAsync(DpiScale state)
    {
        var display = InternalDisplay.Get();
        var pds = display?.ToPathDisplaySource();
        if (pds is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Built in display not found");
            throw new InvalidOperationException("Built in display not found");
        }

        // 已是目标值则直接返回，避免触发不必要的显示重配
        if ((int)pds.CurrentDPIScale == state.Scale)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"DPI scale already set to {state.Scale}");
            return Task.CompletedTask;
        }

        // 校验目标值是否为 Windows 预设的有效档位，非法值静默忽略
        if (!Enum.IsDefined(typeof(DisplayConfigSourceDPIScale), (uint)state.Scale))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"DPI scale {state.Scale} not found");
            return Task.CompletedTask;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting DPI scale to {state.Scale}");

        pds.CurrentDPIScale = (DisplayConfigSourceDPIScale)state.Scale;
        return Task.CompletedTask;
    }
}

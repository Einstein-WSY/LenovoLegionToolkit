using System;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using WindowsDisplayAPI;
using WindowsDisplayAPI.Native.DeviceContext;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// 内置显示屏刷新率特性。
/// 通过 Windows 显示配置 API（WindowsDisplayAPI）读取并切换内屏刷新率；
/// 只在保持当前分辨率/色深/隔行方式不变的前提下切换频率。
/// </summary>
public class RefreshRateFeature : IFeature<RefreshRate>
{
    /// <inheritdoc/>
    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    /// <inheritdoc/>
    public Task<RefreshRate[]> GetAllStatesAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting all refresh rates...");

        var display = InternalDisplay.Get();
        if (display is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Built in display not found");

            return Task.FromResult(Array.Empty<RefreshRate>());
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Built in display found: {display}");

        var currentSettings = display.CurrentSetting;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Current built in display settings: {currentSettings.ToExtendedString()}");

        // 仅保留与当前分辨率/色深/隔行方式兼容的显示模式，取其刷新率并去重排序
        var result = display.GetPossibleSettings()
            .Where(dps => Match(dps, currentSettings))
            .Select(dps => dps.Frequency)
            .Distinct()
            .OrderBy(freq => freq)
            .Select(freq => new RefreshRate(freq))
            .ToArray();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Possible refresh rates are {string.Join(", ", result)}");

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<RefreshRate> GetStateAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting current refresh rate...");

        var display = InternalDisplay.Get();
        if (display is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Built in display not found");

            return Task.FromResult(default(RefreshRate));
        }

        var currentSettings = display.CurrentSetting;
        var result = new RefreshRate(currentSettings.Frequency);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Current refresh rate is {result} [currentSettings={currentSettings.ToExtendedString()}]");

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task SetStateAsync(RefreshRate state)
    {
        var display = InternalDisplay.Get();
        if (display is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Built in display not found");
            throw new InvalidOperationException("Built in display not found");
        }

        var currentSettings = display.CurrentSetting;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Current built in display settings: {currentSettings.ToExtendedString()}");

        // 频率已一致则无需切换，避免屏幕闪烁
        if (currentSettings.Frequency == state.Frequency)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Frequency already set to {state.Frequency}");

            return Task.CompletedTask;
        }

        // 在兼容当前分辨率/色深的模式中查找目标频率，并沿用当前位置与方向构造新设置
        var possibleSettings = display.GetPossibleSettings();
        var newSettings = possibleSettings
            .Where(dps => Match(dps, currentSettings))
            .Where(dps => dps.Frequency == state.Frequency)
            .Select(dps => new DisplaySetting(dps, currentSettings.Position, currentSettings.Orientation, DisplayFixedOutput.Default))
            .FirstOrDefault();

        if (newSettings is not null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Setting display to {newSettings.ToExtendedString()}...");

            display.SetSettingsUsingPathInfo(newSettings);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Display set to {newSettings.ToExtendedString()}");
        }
        else
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Could not find matching settings for frequency {state}");
        }

        return Task.CompletedTask;
    }

    // 判断候选显示模式与当前模式是否兼容：分辨率、色深、隔行方式必须一致，且尺寸不能过小
    private static bool Match(DisplayPossibleSetting dps, DisplayPossibleSetting ds)
    {
        if (dps.IsTooSmall())
            return false;

        var result = true;
        result &= dps.Resolution == ds.Resolution;
        result &= dps.ColorDepth == ds.ColorDepth;
        result &= dps.IsInterlaced == ds.IsInterlaced;
        return result;
    }
}

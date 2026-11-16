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
/// 内置显示屏分辨率特性。
/// 通过 Windows 显示配置 API（WindowsDisplayAPI）读取并切换内屏分辨率；
/// 只在保持当前刷新率/色深/隔行方式不变的前提下切换分辨率。
/// </summary>
public class ResolutionFeature : IFeature<Resolution>
{
    /// <inheritdoc/>
    public Task<bool> IsSupportedAsync() => Task.FromResult(true);

    /// <inheritdoc/>
    public Task<Resolution[]> GetAllStatesAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting all resolutions...");

        var display = InternalDisplay.Get();
        if (display is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Built in display not found");

            return Task.FromResult(Array.Empty<Resolution>());
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Built in display found: {display}");

        var currentSettings = display.CurrentSetting;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Current built in display settings: {currentSettings.ToExtendedString()}");

        // 只保留与当前刷新率/色深/隔行方式兼容的模式，取分辨率去重后由高到低排序
        var result = display.GetPossibleSettings()
            .Where(dps => Match(dps, currentSettings))
            .Select(dps => dps.Resolution)
            .Select(res => new Resolution(res))
            .Distinct()
            .OrderByDescending(res => res)
            .ToArray();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Possible resolutions are {string.Join(", ", result)}");

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task<Resolution> GetStateAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting current resolution...");

        var display = InternalDisplay.Get();
        if (display is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Built in display not found");

            return Task.FromResult(default(Resolution));
        }

        var currentSettings = display.CurrentSetting;
        var result = new Resolution(currentSettings.Resolution);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Current resolution is {result} [currentSettings={currentSettings.ToExtendedString()}]");

        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task SetStateAsync(Resolution state)
    {
        var display = InternalDisplay.Get();
        if (display is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Built in display not found");
            throw new InvalidOperationException("Built in display not found");
        }

        var currentSettings = display.CurrentSetting;

        // 分辨率已一致则直接返回
        if (currentSettings.Resolution == state)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Resolution already set to {state}");

            return Task.CompletedTask;
        }

        var possibleSettings = display.GetPossibleSettings();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Current built in display settings: {currentSettings.ToExtendedString()}");

        // 在兼容当前刷新率/色深的模式中查找目标分辨率，沿用位置与方向
        var newSettings = possibleSettings
            .Where(dps => Match(dps, currentSettings))
            .Where(dps => dps.Resolution == state)
            .Select(dps => new DisplaySetting(dps, currentSettings.Position, currentSettings.Orientation, DisplayFixedOutput.Default))
            .FirstOrDefault();

        if (newSettings is not null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Setting display to {newSettings.ToExtendedString()}");

            display.SetSettingsUsingPathInfo(newSettings);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Display set to {newSettings.ToExtendedString()}");
        }
        else
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Could not find matching settings for resolution {state}");
        }

        return Task.CompletedTask;
    }

    // 判断候选显示模式与当前模式是否兼容：刷新率、色深、隔行方式必须一致，且尺寸不能过小
    private static bool Match(DisplayPossibleSetting dps, DisplayPossibleSetting ds)
    {
        if (dps.IsTooSmall())
            return false;

        var result = true;
        result &= dps.Frequency == ds.Frequency;
        result &= dps.ColorDepth == ds.ColorDepth;
        result &= dps.IsInterlaced == ds.IsInterlaced;
        return result;
    }
}

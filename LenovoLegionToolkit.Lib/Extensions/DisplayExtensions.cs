using System.Linq;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Devices.Display;
using WindowsDisplayAPI;
using WindowsDisplayAPI.DisplayConfig;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供 <see cref="Display"/> 的扩展方法，用于设置分辨率/刷新率以及 HDR 等高级颜色状态。
/// </summary>
public static class DisplayExtensions
{
    /// <summary>
    /// 通过 Display Path API 应用显示设置（分辨率、刷新率等）。
    /// <para>
    /// 与 <c>Display.SetSettings()</c> 仅修改桌面模式不同，这里会同时修改活动信号模式（Active Signal Mode），
    /// 例如切换到 60Hz 时可让面板实际以 60Hz 工作，降低功耗、延长续航。
    /// </para>
    /// </summary>
    /// <param name="display">目标显示器。</param>
    /// <param name="displaySetting">要应用的显示设置。</param>
    public static void SetSettingsUsingPathInfo(this Display display, DisplaySetting displaySetting)
    {
        // Use display path APIs to change internal display resolution & refresh rate.
        // Compared to Display.SetSettings(), these APIs can change the Active Signal Mode and not just the Desktop mode.
        // Setting 60Hz will change the Active Signal Mode to 60Hz instead of leaving it at the max refresh rate,
        // which lets the display consume less power for more battery life.
        var displaySource = display.ToPathDisplaySource();
        // 获取当前所有活动的显示路径（源 + 目标组合）
        var pathInfos = PathInfo.GetActivePaths();

        for (var i = 0; i < pathInfos.Length; i++)
        {
            var pathInfo = pathInfos[i];

            // 找到属于当前显示器的路径后重建其目标信号信息
            if (pathInfo.DisplaySource == displaySource)
            {
                var targetsInfo = pathInfo.TargetsInfo;
                var pathTargetInfos = targetsInfo
                    .Select(targetInfo => new PathTargetInfo(targetInfo.DisplayTarget,
                        new PathTargetSignalInfo(displaySetting, displaySetting.Resolution),
                        targetInfo.Rotation,
                        targetInfo.Scaling))
                    .ToArray();

                pathInfos[i] = new PathInfo(
                    pathInfo.DisplaySource,
                    pathInfo.Position,
                    displaySetting.Resolution,
                    pathInfo.PixelFormat,
                    pathTargetInfos
                );
            }
        }

        PathInfo.ApplyPathInfos(pathInfos);
    }

    /// <summary>
    /// 查询显示器的 DISPLAYCONFIG 高级颜色信息（是否支持 HDR、是否启用 HDR 等）。
    /// </summary>
    /// <param name="display">目标显示器。</param>
    /// <returns>高级颜色信息；若无法获取适配器/目标信息则返回默认值。</returns>
    /// <exception cref="System.ComponentModel.Win32Exception">调用 Win32 API 失败时抛出。</exception>
    public static DisplayAdvancedColorInfo GetAdvancedColorInfo(this Display display)
    {
        var pathDisplayAdapter = display.Adapter.ToPathDisplayAdapter();
        var pathDisplayTarget = display.ToPathDisplayTarget();

        if (pathDisplayTarget is null || pathDisplayAdapter is null)
            return default;

        // 填充 DISPLAYCONFIG 设备信息请求头：类型、结构体大小、适配器 LUID 与目标 ID
        var getAdvancedColorInfo = new DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO();
        getAdvancedColorInfo.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_ADVANCED_COLOR_INFO;
        getAdvancedColorInfo.header.size = (uint)Marshal.SizeOf(typeof(DISPLAYCONFIG_GET_ADVANCED_COLOR_INFO));
        getAdvancedColorInfo.header.adapterId.HighPart = pathDisplayAdapter.AdapterId.HighPart;
        getAdvancedColorInfo.header.adapterId.LowPart = pathDisplayAdapter.AdapterId.LowPart;
        getAdvancedColorInfo.header.id = pathDisplayTarget.TargetId;

        if (PInvoke.DisplayConfigGetDeviceInfo(ref getAdvancedColorInfo.header) != 0)
            PInvokeExtensions.ThrowIfWin32Error("GetAdvancedColorInfo");

        // value 是按位标志：第 0 位表示是否支持高级颜色，第 1 位表示是否支持宽色域，第 2 位表示是否启用 HDR，第 3 位表示是否强制 HDR
        return new(getAdvancedColorInfo.Anonymous.value.GetNthBit(0),
            getAdvancedColorInfo.Anonymous.value.GetNthBit(1),
            getAdvancedColorInfo.Anonymous.value.GetNthBit(2),
            getAdvancedColorInfo.Anonymous.value.GetNthBit(3));
    }

    /// <summary>
    /// 设置显示器的 HDR（高级颜色）启用状态。
    /// </summary>
    /// <param name="display">目标显示器。</param>
    /// <param name="state">为 <c>true</c> 启用 HDR，为 <c>false</c> 关闭 HDR。</param>
    /// <exception cref="System.ComponentModel.Win32Exception">调用 Win32 API 失败时抛出。</exception>
    public static void SetAdvancedColorState(this Display display, bool state)
    {
        var pathDisplayAdapter = display.Adapter.ToPathDisplayAdapter();
        var pathDisplayTarget = display.ToPathDisplayTarget();

        if (pathDisplayTarget is null || pathDisplayAdapter is null)
            return;

        // 构造"设置高级颜色状态"请求，通过第 0 位标志位控制是否启用
        var setAdvancedColorState = new DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE();
        setAdvancedColorState.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_SET_ADVANCED_COLOR_STATE;
        setAdvancedColorState.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SET_ADVANCED_COLOR_STATE>();
        setAdvancedColorState.header.adapterId.HighPart = pathDisplayAdapter.AdapterId.HighPart;
        setAdvancedColorState.header.adapterId.LowPart = pathDisplayAdapter.AdapterId.LowPart;
        setAdvancedColorState.header.id = pathDisplayTarget.TargetId;
        setAdvancedColorState.Anonymous.value = setAdvancedColorState.Anonymous.value.SetNthBit(0, state);

        if (PInvoke.DisplayConfigSetDeviceInfo(setAdvancedColorState.header) != 0)
            PInvokeExtensions.ThrowIfWin32Error("SetAdvancedColorState");
    }
}

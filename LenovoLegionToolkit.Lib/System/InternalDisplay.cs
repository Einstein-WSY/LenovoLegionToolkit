using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.Devices.Display;
using Windows.Win32.Foundation;
using WindowsDisplayAPI;
using WindowsDisplayAPI.DisplayConfig;

namespace LenovoLegionToolkit.Lib.System;

/// <summary>
/// 内屏（笔记本自带屏幕）识别辅助类。
/// 依赖 Windows 显示配置 API（DisplayConfig）判断显示输出的物理连接类型。
/// </summary>
public static class InternalDisplay
{
    /// <summary>
    /// 显示器缓存包装结构。用于区分三种状态：未初始化（null 包装器）、
    /// 已确认无内屏（Empty）、已找到内屏（持有 Display）。
    /// </summary>
    private readonly struct DisplayHolder
    {
        /// <summary>表示"已查询过，但系统中不存在内屏"的哨兵值。</summary>
        public static readonly DisplayHolder Empty = new();

        private readonly Display? _display;

        private DisplayHolder(Display? display) => _display = display;

        /// <summary>
        /// 由 Display 隐式转换为包装器。
        /// </summary>
        /// <param name="s">显示器对象。</param>
        public static implicit operator DisplayHolder(Display? s) => new(s);

        /// <summary>
        /// 由包装器隐式取出 Display。
        /// </summary>
        /// <param name="s">包装器。</param>
        public static implicit operator Display?(DisplayHolder s) => s._display;
    }

    private static readonly object Lock = new();
    private static DisplayHolder? _displayHolder;

    /// <summary>
    /// 标记缓存失效。显示器插拔、分辨率/显卡模式切换（如独显直连）后需调用，使下次 Get 重新探测。
    /// </summary>
    public static void SetNeedsRefresh()
    {
        lock (Lock)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Resetting holder...");

            _displayHolder = null;
        }
    }

    /// <summary>
    /// 获取内屏显示器对象（结果带缓存）。
    /// </summary>
    /// <returns>内屏对象；系统中找不到内屏时返回 null。</returns>
    public static Display? Get()
    {
        lock (Lock)
        {
            if (_displayHolder is not null)
                return _displayHolder;

            var displays = Display.GetDisplays().ToArray();
            // 常规情况：输出类型为 INTERNAL / 内嵌 DisplayPort 的即为内屏
            var internalDisplay = FindInternalDisplay(displays);
            if (internalDisplay is not null)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Found internal display: {internalDisplay}");

                return (_displayHolder = internalDisplay);
            }

            // 特殊情况：Advanced Optimus 机型上内屏可能经独显以"外接 DP"形式暴露，需进一步推断
            var aoDisplay = FindInternalAdvancedOptimusDisplay(displays);
            if (aoDisplay is not null)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Found internal AO display: {aoDisplay}");

                return (_displayHolder = aoDisplay);
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"No internal displays found.");

            // 缓存 Empty，避免每次调用都全量枚举显示器
            return (_displayHolder = DisplayHolder.Empty);
        }
    }

    /// <summary>
    /// 在显示器集合中查找输出类型为内置面板的显示器。
    /// </summary>
    /// <param name="displays">待筛选的显示器集合。</param>
    /// <returns>内屏显示器；未找到返回 null。</returns>
    private static Display? FindInternalDisplay(IEnumerable<Display> displays)
    {
        return displays.Where(d => d.GetVideoOutputTechnology().IsInternalOutput()).FirstOrDefault();
    }

    /// <summary>
    /// 处理 Advanced Optimus 场景下的内屏识别。
    /// 思路：先找以"外接 DP"暴露的显示器，再用 EDID 制造商 ID 在其他显示适配器上
    /// 查找同型号、且输出类型为内置的面板，从而确认它其实是内屏。
    /// </summary>
    /// <param name="displays">待筛选的显示器集合。</param>
    /// <returns>被判定为内屏的显示器；无法判定返回 null。</returns>
    private static Display? FindInternalAdvancedOptimusDisplay(IEnumerable<Display> displays)
    {
        var exDpDisplays = displays.Where(di => di.GetVideoOutputTechnology().IsExternalDisplayPortOutput()).ToArray();

        if (exDpDisplays.Length < 1)
            return null;

        var exDpDisplay = exDpDisplays[0];
        var exDpPathDisplayTarget = exDpDisplay.ToPathDisplayTarget();
        // EDID 制造商 ID 可唯一标识面板厂商+型号组合
        var exDpPortDisplayEdid = exDpPathDisplayTarget.EDIDManufactureId;

        // 在其他显示适配器上查找 EDID 相同且输出类型为内置的设备
        var sameDeviceIsOnAnotherAdapter = DisplayAdapter.GetDisplayAdapters()
            .Where(da => da.DevicePath != exDpDisplay.Adapter.DevicePath)
            .SelectMany(da => da.GetDisplayDevices())
            .Select(dd => dd.ToPathDisplayTarget())
            .Any(pdt => pdt.EDIDManufactureId == exDpPortDisplayEdid && pdt.GetVideoOutputTechnology().IsInternalOutput());

        return sameDeviceIsOnAnotherAdapter ? exDpDisplay : null;
    }

    /// <summary>
    /// 获取显示设备的视频输出技术类型。
    /// </summary>
    /// <param name="displayDevice">显示设备。</param>
    /// <returns>输出技术枚举值。</returns>
    private static DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY GetVideoOutputTechnology(this DisplayDevice displayDevice)
    {
        return GetVideoOutputTechnology(displayDevice.ToPathDisplayTarget());
    }

    /// <summary>
    /// 通过 DisplayConfigGetDeviceInfo 查询指定显示目标的视频输出技术类型。
    /// 实现方式为手工封送（AllocHGlobal + StructureToPtr），因该 API 需先填 header 再取回结构体。
    /// </summary>
    /// <param name="pathDisplayTarget">显示路径目标。</param>
    /// <returns>输出技术类型；查询失败时降级为 OTHER。</returns>
    private static unsafe DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY GetVideoOutputTechnology(this PathDisplayTarget pathDisplayTarget)
    {
        var intPtr = IntPtr.Zero;
        try
        {
            // 构造请求头：type 指定要查询的信息种类，id 为显示目标 ID，adapterId 为适配器 LUID
            var deviceName = new DISPLAYCONFIG_TARGET_DEVICE_NAME
            {
                header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                {
                    type = DISPLAYCONFIG_DEVICE_INFO_TYPE.DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                    id = pathDisplayTarget.TargetId,
                    adapterId = new LUID
                    {
                        HighPart = pathDisplayTarget.Adapter.AdapterId.HighPart,
                        LowPart = pathDisplayTarget.Adapter.AdapterId.LowPart,
                    },
                    size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>()
                }
            };

            intPtr = Marshal.AllocHGlobal((int)deviceName.header.size);
            Marshal.StructureToPtr(deviceName, intPtr, false);

            var success = PInvoke.DisplayConfigGetDeviceInfo((DISPLAYCONFIG_DEVICE_INFO_HEADER*)intPtr.ToPointer());
            if (success != PInvokeExtensions.ERROR_SUCCESS)
                PInvokeExtensions.ThrowIfWin32Error("DisplayConfigGetDeviceInfo");

            // API 会就地填充同一块内存，直接按目标结构体读取
            var deviceNameResponse = Marshal.PtrToStructure<DISPLAYCONFIG_TARGET_DEVICE_NAME>(intPtr);
            return deviceNameResponse.outputTechnology;
        }
        catch
        {
            // 显示配置查询在 RDP、驱动异常等场景会失败，降级为 OTHER 不影响调用方判定
            return DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_OTHER;
        }
        finally
        {
            // 必须在 finally 中释放非托管内存，避免异常路径泄漏
            Marshal.FreeHGlobal(intPtr);
        }
    }

    /// <summary>
    /// 判断输出技术类型是否为内置面板。
    /// </summary>
    /// <param name="outputTechnology">输出技术枚举值。</param>
    /// <returns>内置面板返回 true。</returns>
    private static bool IsInternalOutput(this DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY outputTechnology)
    {
        // 普通内屏为 INTERNAL，部分机型内屏走 eDP 时报告为 DISPLAYPORT_EMBEDDED
        var result = outputTechnology is DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL;
        result |= outputTechnology is DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED;
        return result;
    }

    /// <summary>
    /// 判断输出技术类型是否为外接 DisplayPort。
    /// </summary>
    /// <param name="outputTechnology">输出技术枚举值。</param>
    /// <returns>外接 DP 返回 true。</returns>
    private static bool IsExternalDisplayPortOutput(this DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY outputTechnology)
    {
        return outputTechnology is DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EXTERNAL;
    }
}

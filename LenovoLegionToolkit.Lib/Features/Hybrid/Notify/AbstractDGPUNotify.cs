using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Foundation;

namespace LenovoLegionToolkit.Lib.Features.Hybrid.Notify;

/// <summary>
/// dGPU 状态通知的通用基类。
/// 负责：解析 dGPU 硬件 ID、通过 SetupAPI 枚举显示设备判断 dGPU 是否可用、
/// 以及"延迟通知"的防抖逻辑。具体的通知发送与硬件 ID 获取由子类实现。
/// </summary>
public abstract partial class AbstractDGPUNotify : IDGPUNotify
{
    // 从设备路径中提取 PCI 厂商 ID（ven_XXXX）与设备 ID（dev_XXXX）
    [GeneratedRegex("pci#ven_([0-9A-Fa-f]{4})|dev_([0-9A-Fa-f]{4})")]
    private static partial Regex HardwareIdRegex();

    private readonly object _lock = new();

    private CancellationTokenSource? _notifyLaterCancellationTokenSource;

    /// <inheritdoc/>
    public event EventHandler<bool>? Notified;

    /// <inheritdoc/>
    public abstract Task<bool> IsSupportedAsync();

    /// <inheritdoc/>
    public async Task<bool> IsDGPUAvailableAsync()
    {
        try
        {
            var dgpuHardwareId = await GetDGPUHardwareIdAsync().ConfigureAwait(false);
            var isAvailable = IsDGPUAvailable(dgpuHardwareId);
            return isAvailable;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to notify.", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task NotifyAsync(bool publish = true)
    {
        // 立即通知前取消尚未触发的延迟通知，避免重复执行
        lock (_lock)
        {
            _notifyLaterCancellationTokenSource?.Cancel();
            _notifyLaterCancellationTokenSource = null;
        }

        try
        {
            var dgpuHardwareId = await GetDGPUHardwareIdAsync().ConfigureAwait(false);
            var isAvailable = IsDGPUAvailable(dgpuHardwareId);
            await NotifyDGPUStatusAsync(isAvailable).ConfigureAwait(false);

            if (publish)
                Notified?.Invoke(this, isAvailable);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Notified: {isAvailable}");
        }
        catch (Exception ex)
        {
            // 通知失败通常因为硬件不支持该接口，忽略即可，不影响主流程
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to notify.", ex);
        }
    }

    /// <inheritdoc/>
    public Task NotifyLaterIfNeededAsync()
    {
        CancellationToken token;

        lock (_lock)
        {
            // 反复调用时重置定时器，只保留最后一次
            _notifyLaterCancellationTokenSource?.Cancel();
            _notifyLaterCancellationTokenSource = new();

            token = _notifyLaterCancellationTokenSource.Token;
        }

        // 延迟 5 秒执行一次通知；若期间被取消（模式再次切换）则跳过
        _ = Task.Delay(TimeSpan.FromSeconds(5), token)
            .ContinueWith(async t =>
            {
                if (!t.IsCompletedSuccessfully)
                    return;

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Event not received, notifying anyway...");

                await NotifyAsync(false).ConfigureAwait(false);
            }, token);

        return Task.CompletedTask;
    }

    /// <summary>向固件发送 dGPU 状态。<paramref name="state"/> 为 <c>true</c> 表示唤醒/可用。</summary>
    protected abstract Task NotifyDGPUStatusAsync(bool state);

    /// <summary>获取 dGPU 的硬件 ID（用于在设备枚举中定位独显）。</summary>
    protected abstract Task<HardwareId> GetDGPUHardwareIdAsync();

    // 通过 SetupAPI 枚举当前存在的显示设备接口，检查是否存在与 dGPU 硬件 ID 匹配且无问题标志的节点
    private unsafe bool IsDGPUAvailable(HardwareId dgpuHardwareId)
    {
        if (dgpuHardwareId == HardwareId.Empty)
            return false;

        var guidDisplayDeviceArrival = PInvoke.GUID_DISPLAY_DEVICE_ARRIVAL;
        // 只枚举"当前存在"的显示设备接口（DIGCF_PRESENT），避免把已弹出的独显当成可用；
        // 设备信息集句柄必须用 using 释放，否则泄漏
        using var deviceHandle = PInvoke.SetupDiGetClassDevs(guidDisplayDeviceArrival,
            null,
            HWND.Null,
            SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_DEVICEINTERFACE | SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_PRESENT | SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_PROFILE);

        uint index = 0;
        while (true)
        {
            var currentIndex = index;
            index++;

            var deviceInfoData = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
            var result1 = PInvoke.SetupDiEnumDeviceInfo(deviceHandle, currentIndex, ref deviceInfoData);
            if (!result1)
            {
                // ERROR_NO_MORE_ITEMS 表示枚举结束，其他错误才是真正的失败
                if (Marshal.GetLastWin32Error() == PInvokeExtensions.ERROR_NO_MORE_ITEMS)
                    break;

                PInvokeExtensions.ThrowIfWin32Error("SetupDiEnumDeviceInfo");
            }

            var deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };
            var result2 = PInvoke.SetupDiEnumDeviceInterfaces(deviceHandle, null, guidDisplayDeviceArrival, currentIndex, ref deviceInterfaceData);
            if (!result2)
                PInvokeExtensions.ThrowIfWin32Error("SetupDiEnumDeviceInterfaces");

            // 第一次调用只用于获取所需缓冲区大小
            var requiredSize = 0u;
            _ = PInvoke.SetupDiGetDeviceInterfaceDetail(deviceHandle, deviceInterfaceData, null, 0, &requiredSize, null);

            string devicePath;
            var output = IntPtr.Zero;
            try
            {
                output = Marshal.AllocHGlobal((int)requiredSize);
                var deviceDetailData = (SP_DEVICE_INTERFACE_DETAIL_DATA_W*)output.ToPointer();
                deviceDetailData->cbSize = (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DETAIL_DATA_W>();

                var result3 = PInvoke.SetupDiGetDeviceInterfaceDetail(deviceHandle, deviceInterfaceData, deviceDetailData, requiredSize, null, null);
                if (!result3)
                    PInvokeExtensions.ThrowIfWin32Error("SetupDiGetDeviceInterfaceDetail");

                fixed (char* e0Ptr = &deviceDetailData->DevicePath.e0)
                    devicePath = new string(e0Ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(output);
            }

            // 过滤掉非显示设备到达接口的设备
            if (!devicePath.Contains(guidDisplayDeviceArrival.ToString()))
                continue;

            // 硬件 ID 不匹配则不是目标独显
            if (dgpuHardwareId != HardwareIdFromDevicePath(devicePath))
                continue;

            if (PInvoke.CM_Get_DevNode_Status(out var status, out _, deviceInfoData.DevInst, 0) != 0)
                continue;

            // 带 DN_HAS_PROBLEM 标志说明设备处于异常状态（如代码 31/43），视为不可用
            if (status.HasFlag(CM_DEVNODE_STATUS_FLAGS.DN_HAS_PROBLEM))
                continue;

            return true;
        }

        return false;
    }

    // 从设备接口路径中解析 ven_XXXX / dev_XXXX 得到硬件 ID；格式不符时返回默认值
    private static HardwareId HardwareIdFromDevicePath(string devicePath)
    {
        try
        {
            var matches = HardwareIdRegex().Matches(devicePath);
            if (matches.Count != 2)
                return default;

            var vendor = matches[0].Groups[1].Value;
            var device = matches[1].Groups[2].Value;

            return new(vendor, device);
        }
        catch
        {
            return default;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Devices.DeviceAndDriverInstallation;
using Windows.Win32.Devices.HumanInterfaceDevice;
using Windows.Win32.Devices.Properties;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;

namespace LenovoLegionToolkit.Lib.System;

/// <summary>
/// 设备枚举与设备句柄管理辅助类。
/// 基于 SetupAPI（SetupDi*）枚举设备并读取设备属性，通过 CreateFile 打开设备接口句柄。
/// 电池与 RGB 键盘句柄均在进程内缓存为单例。
/// </summary>
public static class Devices
{
    private static readonly object Lock = new();

    private static SafeFileHandle? _battery;
    private static SafeFileHandle? _rgbKeyboard;
    private static SafeFileHandle? _spectrumRgbKeyboard;

    #region All devices

    /// <summary>
    /// 枚举本机所有设备节点。
    /// </summary>
    /// <returns>设备信息列表；无法创建设备信息集时返回空列表。</returns>
    public static List<Device> GetAll()
    {
        // DIGCF_ALLCLASSES 表示不限定设备类，枚举全部设备；第一个参数传 null 即代表所有类
        using var deviceInfoSet = PInvoke.SetupDiGetClassDevs(null as Guid?,
            null,
            HWND.Null,
            SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_ALLCLASSES);

        if (deviceInfoSet.IsInvalid)
            return [];

        var devices = new List<Device>();

        var index = 0u;
        while (true)
        {
            var currentIndex = index;
            index++;

            // cbSize 必须初始化，否则 SetupDiEnumDeviceInfo 会返回参数错误
            var deviceInfoData = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
            if (!PInvoke.SetupDiEnumDeviceInfo(deviceInfoSet, currentIndex, ref deviceInfoData))
            {
                // ERROR_NO_MORE_ITEMS 表示已枚举完毕，是正常退出条件
                if (Marshal.GetLastWin32Error() == PInvokeExtensions.ERROR_NO_MORE_ITEMS)
                    break;

                continue;
            }

            var (removable, isDisconnected) = GetFlags(deviceInfoData);

            // 已断开的设备无法从 DevNode 状态取到可移动标志，改用设备能力属性判断
            if (isDisconnected)
            {
                var capabilities = (CM_DEVCAP)GetUInt32Property(deviceInfoSet, deviceInfoData, PInvoke.DEVPKEY_Device_Capabilities);
                removable = capabilities.HasFlag(CM_DEVCAP.CM_DEVCAP_REMOVABLE);
            }

            var name = GetStringProperty(deviceInfoSet, deviceInfoData, PInvoke.DEVPKEY_NAME);
            var description = GetStringProperty(deviceInfoSet, deviceInfoData, PInvoke.DEVPKEY_Device_DeviceDesc);
            var busReportedDeviceDescription = GetStringProperty(deviceInfoSet, deviceInfoData, PInvoke.DEVPKEY_Device_BusReportedDeviceDesc);
            var deviceInstanceId = GetStringProperty(deviceInfoSet, deviceInfoData, PInvoke.DEVPKEY_Device_InstanceId);
            var classGuid = GetGuidProperty(deviceInfoSet, deviceInfoData, PInvoke.DEVPKEY_Device_ClassGuid);
            var className = GetClassName(classGuid);

            // 过滤掉 ROOT 枚举下的纯软件虚拟设备，界面上无需展示
            if (deviceInstanceId.Contains("ROOT"))
                continue;

            devices.Add(new(name, description, busReportedDeviceDescription, deviceInstanceId, classGuid, className, removable, isDisconnected));
        }

        return devices;
    }

    /// <summary>
    /// 由设备类 GUID 查询设备类名称。
    /// </summary>
    /// <param name="guid">设备类 GUID。</param>
    /// <returns>设备类名称；查询失败返回空字符串。</returns>
    private static unsafe string GetClassName(Guid guid)
    {
        // 先以空缓冲区调用一次，通过 requiredSize 获取所需字符数
        var requiredSize = 0u;
        PInvoke.SetupDiClassNameFromGuid(guid, Array.Empty<char>(), &requiredSize);

        var chars = new char[requiredSize];
        PInvoke.SetupDiClassNameFromGuid(guid, chars, null);
        return chars.ToString() ?? string.Empty;
    }

    /// <summary>
    /// 读取字符串类型的设备属性。
    /// </summary>
    /// <param name="deviceInfoSet">设备信息集句柄。</param>
    /// <param name="deviceInfoData">设备信息数据。</param>
    /// <param name="propertyKey">属性键。</param>
    /// <returns>属性字符串；属性为空时返回空字符串。</returns>
    /// <exception cref="InvalidOperationException">属性类型不是字符串时抛出。</exception>
    private static unsafe string GetStringProperty(SetupDiDestroyDeviceInfoListSafeHandle deviceInfoSet, SP_DEVINFO_DATA deviceInfoData, DEVPROPKEY propertyKey)
    {
        // 第一次调用只取属性类型与所需缓冲区大小
        var requiredSize = 0u;
        PInvoke.SetupDiGetDeviceProperty(deviceInfoSet, deviceInfoData, propertyKey, out var propertyType, null, &requiredSize, 0);

        if (propertyType == DEVPROPTYPE.DEVPROP_TYPE_EMPTY)
            return string.Empty;

        if (propertyType != DEVPROPTYPE.DEVPROP_TYPE_STRING)
            throw new InvalidOperationException("Device property is not a string");

        var buffer = new byte[requiredSize];
        var propertyBuffer = new Span<byte>(buffer);
        PInvoke.SetupDiGetDeviceProperty(deviceInfoSet, deviceInfoData, propertyKey, out _, propertyBuffer, null, 0);

        // 属性值以 UTF-16 存储且带结尾 NUL，需去掉
        return Encoding.Unicode.GetString(buffer).TrimEnd('\0');
    }

    /// <summary>
    /// 读取 UInt32 类型的设备属性。
    /// </summary>
    /// <param name="deviceInfoSet">设备信息集句柄。</param>
    /// <param name="deviceInfoData">设备信息数据。</param>
    /// <param name="propertyKey">属性键。</param>
    /// <returns>属性值；属性为空时返回 0。</returns>
    /// <exception cref="InvalidOperationException">属性类型不是 UInt32 时抛出。</exception>
    private static unsafe uint GetUInt32Property(SetupDiDestroyDeviceInfoListSafeHandle deviceInfoSet, SP_DEVINFO_DATA deviceInfoData, DEVPROPKEY propertyKey)
    {
        var requiredSize = 0u;
        PInvoke.SetupDiGetDeviceProperty(deviceInfoSet, deviceInfoData, propertyKey, out var propertyType, null, &requiredSize, 0);

        if (propertyType == DEVPROPTYPE.DEVPROP_TYPE_EMPTY)
            return 0;

        if (propertyType != DEVPROPTYPE.DEVPROP_TYPE_UINT32)
            throw new InvalidOperationException("Device property is not a string");

        var buffer = new byte[requiredSize];
        var propertyBuffer = new Span<byte>(buffer);
        PInvoke.SetupDiGetDeviceProperty(deviceInfoSet, deviceInfoData, propertyKey, out _, propertyBuffer, null, 0);

        return BitConverter.ToUInt32(buffer);
    }

    /// <summary>
    /// 读取 GUID 类型的设备属性。
    /// </summary>
    /// <param name="deviceInfoSet">设备信息集句柄。</param>
    /// <param name="deviceInfoData">设备信息数据。</param>
    /// <param name="propertyKey">属性键。</param>
    /// <returns>属性 GUID；属性为空时返回 Guid.Empty。</returns>
    /// <exception cref="InvalidOperationException">属性类型不是 GUID 时抛出。</exception>
    private static unsafe Guid GetGuidProperty(SetupDiDestroyDeviceInfoListSafeHandle deviceInfoSet, SP_DEVINFO_DATA deviceInfoData, DEVPROPKEY propertyKey)
    {
        var requiredSize = 0u;
        PInvoke.SetupDiGetDeviceProperty(deviceInfoSet, deviceInfoData, propertyKey, out var propertyType, null, &requiredSize, 0);

        if (propertyType == DEVPROPTYPE.DEVPROP_TYPE_EMPTY)
            return Guid.Empty;

        if (propertyType != DEVPROPTYPE.DEVPROP_TYPE_GUID)
            throw new InvalidOperationException("Device property is not a GUID");

        var buffer = new byte[requiredSize];
        var propertyBuffer = new Span<byte>(buffer);
        PInvoke.SetupDiGetDeviceProperty(deviceInfoSet, deviceInfoData, propertyKey, out _, propertyBuffer, null, 0);

        return new Guid(buffer);
    }

    /// <summary>
    /// 查询设备节点的状态标志。
    /// </summary>
    /// <param name="deviceInfoData">设备信息数据。</param>
    /// <returns>元组：设备是否可移动、设备是否已断开（CR_NO_SUCH_DEVINST 表示设备实例不存在）。</returns>
    private static (bool noShow, bool disconnected) GetFlags(SP_DEVINFO_DATA deviceInfoData)
    {
        var result = PInvoke.CM_Get_DevNode_Status(out var flags, out _, deviceInfoData.DevInst, 0);
        return (flags.HasFlag(CM_DEVNODE_STATUS_FLAGS.DN_REMOVABLE), result == CONFIGRET.CR_NO_SUCH_DEVINST);
    }

    #endregion

    #region Battery

    /// <summary>
    /// 获取电池设备句柄（进程内缓存）。
    /// </summary>
    /// <param name="forceRefresh">为 true 时忽略缓存并重新枚举打开设备（电池更换/热插拔后使用）。</param>
    /// <returns>电池设备句柄。</returns>
    public static unsafe SafeFileHandle GetBattery(bool forceRefresh = false)
    {
        if (_battery is not null && !forceRefresh)
            return _battery;

        lock (Lock)
        {
            if (_battery is not null && !forceRefresh)
                return _battery;

            var devClassBatteryGuid = PInvoke.GUID_DEVCLASS_BATTERY;
            // DIGCF_PRESENT 只枚举当前存在的设备，DIGCF_DEVICEINTERFACE 表示按设备接口枚举
            using var deviceHandle = PInvoke.SetupDiGetClassDevs(devClassBatteryGuid,
                null,
                HWND.Null,
                SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_PRESENT | SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_DEVICEINTERFACE);

            // ReSharper disable once StringLiteralTypo
            if (deviceHandle.IsInvalid)
                PInvokeExtensions.ThrowIfWin32Error("SetupDiGetClassDevs");

            var deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };

            var result1 = PInvoke.SetupDiEnumDeviceInterfaces(deviceHandle, null, devClassBatteryGuid, 0, ref deviceInterfaceData);
            if (!result1)
                PInvokeExtensions.ThrowIfWin32Error("SetupDiEnumDeviceInterfaces");

            // 第一次调用取设备接口详情所需缓冲区大小
            var requiredSize = 0u;
            _ = PInvoke.SetupDiGetDeviceInterfaceDetail(deviceHandle, deviceInterfaceData, null, 0, &requiredSize, null);

            string devicePath;
            var output = IntPtr.Zero;
            try
            {
                output = Marshal.AllocHGlobal((int)requiredSize);
                var deviceDetailData = (SP_DEVICE_INTERFACE_DETAIL_DATA_W*)output.ToPointer();
                // cbSize 只需填结构体头大小（不含变长路径），系统会按此解释布局
                deviceDetailData->cbSize = (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DETAIL_DATA_W>();

                var result3 = PInvoke.SetupDiGetDeviceInterfaceDetail(deviceHandle, deviceInterfaceData, deviceDetailData, requiredSize, null, null);
                if (!result3)
                    PInvokeExtensions.ThrowIfWin32Error("SetupDiEnumDeviceInterfaces");

                // DevicePath 是内联在结构体尾部的宽字符数组，取其首地址构造字符串
                fixed (char* e0Ptr = &deviceDetailData->DevicePath.e0)
                    devicePath = new string(e0Ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(output);
            }

            var fileHandle = PInvoke.CreateFile(devicePath,
                (uint)FILE_ACCESS_RIGHTS.FILE_READ_DATA | (uint)FILE_ACCESS_RIGHTS.FILE_WRITE_DATA,
                FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
                null,
                FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL,
                null);

            if (fileHandle.IsInvalid)
                PInvokeExtensions.ThrowIfWin32Error("CreateFile");

            _battery = fileHandle;
        }

        return _battery;
    }

    #endregion

    #region Keyboard

    /// <summary>
    /// 获取 RGB 键盘的 HID 设备句柄（进程内缓存）。
    /// </summary>
    /// <param name="forceRefresh">为 true 时忽略缓存重新查找。</param>
    /// <returns>HID 设备句柄；未找到对应键盘时返回 null。</returns>
    public static SafeFileHandle? GetRGBKeyboard(bool forceRefresh = false)
    {
        if (_rgbKeyboard is not null && !forceRefresh)
            return _rgbKeyboard;

        lock (Lock)
        {
            if (_rgbKeyboard is not null && !forceRefresh)
                return _rgbKeyboard;

            // VendorID 0x048D 为 ITE 科技；ProductID 高字节 0xC9 为 Legion RGB 键盘系列，用掩码匹配
            const ushort vendorId = 0x048D;
            const ushort productIdMasked = 0xC900;
            const ushort productIdMask = 0xFF00;
            // 0x21 字节的 Feature Report 对应经典 RGB 键盘协议
            const ushort descriptorLength = 0x21;

            _rgbKeyboard = FindHidDevice(vendorId, productIdMask, productIdMasked, descriptorLength);
        }

        return _rgbKeyboard;
    }

    /// <summary>
    /// 获取 Spectrum（四分区灯效）RGB 键盘的 HID 设备句柄（进程内缓存）。
    /// </summary>
    /// <param name="forceRefresh">为 true 时忽略缓存重新查找。</param>
    /// <returns>HID 设备句柄；未找到对应键盘时返回 null。</returns>
    public static SafeFileHandle? GetSpectrumRGBKeyboard(bool forceRefresh = false)
    {
        if (_spectrumRgbKeyboard is not null && !forceRefresh)
            return _spectrumRgbKeyboard;

        lock (Lock)
        {
            if (_spectrumRgbKeyboard is not null && !forceRefresh)
                return _spectrumRgbKeyboard;

            // 与经典 RGB 键盘同厂商/同产品系列，仅通过 Feature Report 长度区分协议版本
            const ushort vendorId = 0x048D;
            const ushort productIdMasked = 0xC900;
            const ushort productIdMask = 0xFF00;
            const ushort descriptorLength = 0x03C0;

            _spectrumRgbKeyboard = FindHidDevice(vendorId, productIdMask, productIdMasked, descriptorLength);
        }

        return _spectrumRgbKeyboard;
    }

    /// <summary>
    /// 在 HID 设备类中查找符合条件（厂商 ID、产品 ID 掩码、Feature Report 长度）的设备。
    /// </summary>
    /// <param name="vendorId">目标厂商 ID。</param>
    /// <param name="productIdMask">产品 ID 掩码。</param>
    /// <param name="productIdMasked">掩码后的期望产品 ID。</param>
    /// <param name="descriptorLength">期望的 Feature Report 字节长度。</param>
    /// <returns>匹配的设备句柄；未找到返回 null。</returns>
    private static unsafe SafeFileHandle? FindHidDevice(ushort vendorId, ushort productIdMask, ushort productIdMasked, ushort descriptorLength)
    {
        // HidD_GetHidGuid 返回 HID 设备类 GUID，用于 SetupDi 枚举
        PInvoke.HidD_GetHidGuid(out var devClassHidGuid);

        using var deviceHandle = PInvoke.SetupDiGetClassDevs(devClassHidGuid,
            null,
            HWND.Null,
            SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_PRESENT | SETUP_DI_GET_CLASS_DEVS_FLAGS.DIGCF_DEVICEINTERFACE);

        uint index = 0;
        while (true)
        {
            var currentIndex = index;
            index++;

            var deviceInfoData = new SP_DEVINFO_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVINFO_DATA>() };
            var result1 = PInvoke.SetupDiEnumDeviceInfo(deviceHandle, currentIndex, ref deviceInfoData);
            if (!result1)
            {
                var errorCode = Marshal.GetLastWin32Error();
                if (errorCode == PInvokeExtensions.ERROR_NO_MORE_ITEMS)
                    break;

                PInvokeExtensions.ThrowIfWin32Error(errorCode, "SetupDiEnumDeviceInfo");
            }

            var deviceInterfaceData = new SP_DEVICE_INTERFACE_DATA { cbSize = (uint)Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>() };

            var result2 = PInvoke.SetupDiEnumDeviceInterfaces(deviceHandle, null, devClassHidGuid, currentIndex,
                ref deviceInterfaceData);
            if (!result2)
                PInvokeExtensions.ThrowIfWin32Error("SetupDiEnumDeviceInterfaces");

            // 两段式调用：先取所需缓冲区大小，再填充设备接口详情
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
                    PInvokeExtensions.ThrowIfWin32Error("SetupDiEnumDeviceInterfaces");

                fixed (char* e0Ptr = &deviceDetailData->DevicePath.e0)
                    devicePath = new string(e0Ptr);
            }
            finally
            {
                Marshal.FreeHGlobal(output);
            }

            var fileHandle = PInvoke.CreateFile(devicePath,
                (uint)FILE_ACCESS_RIGHTS.FILE_READ_DATA | (uint)FILE_ACCESS_RIGHTS.FILE_WRITE_DATA,
                FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE,
                null,
                FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_NORMAL,
                null);

            if (!PInvoke.HidD_GetAttributes(fileHandle, out var hidAttributes))
                continue;

            PHIDP_PREPARSED_DATA preParsedData = default;
            try
            {
                // 预解析数据用于调用 HidP_GetCaps 获取该设备的 HID 能力（如 Feature Report 长度）
                PInvoke.HidD_GetPreparsedData(fileHandle, out preParsedData);
                PInvoke.HidP_GetCaps(preParsedData, out var caps);

                if (hidAttributes.VendorID == vendorId && (hidAttributes.ProductID & productIdMask) == productIdMasked && caps.FeatureReportByteLength == descriptorLength)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Found device. [vendorId={hidAttributes.VendorID:X2}, productId={hidAttributes.ProductID:X2}, descriptorLength={caps.FeatureReportByteLength}]");

                    return fileHandle;
                }
            }
            finally
            {
                // 预解析数据由系统分配，必须显式释放，否则会泄漏非托管内存
                PInvoke.HidD_FreePreparsedData(preParsedData);
            }
        }

        return null;
    }

    #endregion
}

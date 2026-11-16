using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming
// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System.Management;

public static partial class WMI
{
    /// <summary>
    /// 标准 Win32/CIM 类的封装（命名空间为 root\CIMV2），用于进程监控与基础硬件/系统信息查询。
    /// </summary>
    public static class Win32
    {
        /// <summary>
        /// Win32_ProcessStartTrace 事件封装：进程启动时通知。
        /// </summary>
        public static class ProcessStartTrace
        {
            /// <summary>
            /// 订阅进程启动事件。
            /// </summary>
            /// <param name="handler">回调，参数为进程 ID 与不含扩展名的进程名。</param>
            /// <returns>可释放对象，释放后停止监听。</returns>
            public static IDisposable Listen(Action<int, string> handler) => WMI.Listen("root\\CIMV2",
                $"SELECT * FROM Win32_ProcessStartTrace",
                pdc =>
                {
                    var processId = Convert.ToInt32(pdc["ProcessID"].Value);
                    var processName = (string)pdc["ProcessName"].Value;
                    // 事件返回的是可执行文件名（含 .exe），去掉扩展名便于与配置中的进程名比较
                    handler(processId, Path.GetFileNameWithoutExtension(processName));
                });
        }

        /// <summary>
        /// Win32_ProcessStopTrace 事件封装：进程退出时通知。
        /// </summary>
        public static class ProcessStopTrace
        {
            /// <summary>
            /// 订阅进程退出事件。
            /// </summary>
            /// <param name="handler">回调，参数为进程 ID 与不含扩展名的进程名。</param>
            /// <returns>可释放对象，释放后停止监听。</returns>
            public static IDisposable Listen(Action<int, string> handler) => WMI.Listen("root\\CIMV2",
                $"SELECT * FROM Win32_ProcessStopTrace",
                pdc =>
                {
                    var processId = Convert.ToInt32(pdc["ProcessID"].Value);
                    var processName = (string)pdc["ProcessName"].Value;
                    handler(processId, Path.GetFileNameWithoutExtension(processName));
                });
        }

        /// <summary>
        /// Win32_ComputerSystemProduct 类封装：读取机型（厂商、型号、版本、序列号）。
        /// </summary>
        public static class ComputerSystemProduct
        {
            /// <summary>
            /// 读取本机产品信息。
            /// </summary>
            /// <returns>元组：厂商、名称（机型）、版本、标识号（序列号）。</returns>
            public static async Task<(string vendor, string name, string version, string identifyingNumber)> ReadAsync()
            {
                var result = await WMI.ReadAsync("root\\CIMV2",
                    $"SELECT * FROM Win32_ComputerSystemProduct",
                    pdc =>
                    {
                        var vendor = (string)pdc["Vendor"].Value;
                        var name = (string)pdc["Name"].Value;
                        var version = (string)pdc["Version"].Value;
                        var identifyingNumber = (string)pdc["IdentifyingNumber"].Value;
                        return (vendor, name, version, identifyingNumber);
                    }).ConfigureAwait(false);
                // 该 WMI 类在正常系统中只有一条实例
                return result.First();
            }
        }

        /// <summary>
        /// Win32_Processor 类封装：读取 CPU 信息。
        /// </summary>
        public static class Processor
        {
            /// <summary>
            /// 读取 CPU 地址宽度（32 或 64），用于判断操作系统/进程位数。
            /// </summary>
            /// <returns>地址宽度（位）。</returns>
            public static async Task<int> GetAddressWidthAsync()
            {
                var result = await ReadAsync("root\\CIMV2",
                    $"SELECT * FROM Win32_Processor",
                    pdc => Convert.ToInt32(pdc["AddressWidth"].Value)).ConfigureAwait(false);
                return result.First();
            }
        }

        /// <summary>
        /// Win32_OperatingSystem 类封装：读取操作系统信息。
        /// </summary>
        public static class OperatingSystem
        {
            /// <summary>
            /// 读取操作系统内部版本号（如 22631）。
            /// </summary>
            /// <returns>版本号字符串。</returns>
            public static async Task<string> GetBuildNumberAsync()
            {
                var result = await ReadAsync("root\\CIMV2",
                    $"SELECT * FROM Win32_OperatingSystem",
                    pdc => (string)pdc["BuildNumber"].Value).ConfigureAwait(false);
                return result.First();
            }
        }

        /// <summary>
        /// Win32_PnpEntity 类封装：按设备 ID 前缀查询即插即用设备。
        /// </summary>
        public static class PnpEntity
        {
            /// <summary>
            /// 按设备 ID 前缀查找设备。
            /// </summary>
            /// <param name="pnpDeviceIdPart">设备 ID 前缀，例如 PCI\VEN_10DE。</param>
            /// <returns>首个匹配的设备 ID；未找到返回 null。</returns>
            public static async Task<string?> GetDeviceIDAsync(string pnpDeviceIdPart)
            {
                // LIKE 前缀匹配，需由调用方保证参数不含 WQL 特殊字符
                var results = await ReadAsync("root\\CIMV2",
                    $"SELECT * FROM Win32_PnpEntity WHERE DeviceID LIKE '{pnpDeviceIdPart}%'",
                    pdc => (string)pdc["DeviceID"].Value).ConfigureAwait(false);
                return results.FirstOrDefault();
            }
        }

        /// <summary>
        /// Win32_PnPSignedDriver 类封装：枚举已签名的驱动程序信息。
        /// </summary>
        public static class PnpSignedDriver
        {
            /// <summary>
            /// 读取全部已签名驱动信息。
            /// </summary>
            /// <returns>驱动信息集合（设备 ID、硬件 ID、版本、日期）。</returns>
            public static Task<IEnumerable<DriverInfo>> ReadAsync() => WMI.ReadAsync("root\\CIMV2",
                $"SELECT * FROM Win32_PnPSignedDriver",
                pdc =>
                {
                    var deviceId = pdc["DeviceID"].Value as string ?? string.Empty;
                    var hardwareId = pdc["HardWareId"].Value as string ?? string.Empty;
                    var driverVersionString = pdc["DriverVersion"].Value as string;
                    var driverDateString = pdc["DriverDate"].Value as string;

                    // 驱动版本格式可能不规范（如含厂商自定义后缀），解析失败时保留 null
                    Version? driverVersion = null;
                    if (Version.TryParse(driverVersionString, out var v))
                        driverVersion = v;

                    DateTime? driverDate = null;
                    if (driverDateString is not null)
                        // WMI 日期为 CIM_DATETIME 字符串，需转换为 DateTime 并只保留日期部分
                        driverDate = ManagementDateTimeConverter.ToDateTime(driverDateString).Date;

                    return new DriverInfo(deviceId, hardwareId, driverVersion, driverDate);
                });
        }
    }
}

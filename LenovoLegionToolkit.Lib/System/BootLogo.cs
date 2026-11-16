using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;
using Windows.Win32;

namespace LenovoLegionToolkit.Lib.System;

/// <summary>
/// 无法获取修改 UEFI 变量所需特权时抛出。
/// </summary>
public class CantSetUEFIPrivilegeException : Exception;

/// <summary>
/// 无法挂载 EFI 系统分区时抛出。
/// </summary>
public class CantMountUEFIPartitionException : Exception;

/// <summary>
/// EFI 分区剩余空间不足以存放开机 Logo 时抛出。
/// </summary>
public class NotEnoughSpaceOnUEFIPartitionException : Exception;

/// <summary>
/// 开机 Logo 图片格式不受固件支持时抛出。
/// </summary>
public class InvalidBootLogoImageFormatException : Exception;

/// <summary>
/// 开机 Logo 图片尺寸与固件要求不符时抛出。
/// </summary>
public class InvalidBootLogoImageSizeException : Exception;

/// <summary>
/// 开机 Logo（开机画面）管理辅助类。
/// 实现方式：通过 UEFI 变量 LBLDESP（配置信息）与 LBLDVC（CRC 校验值）告知固件是否启用自定义 Logo，
/// 并把图片文件写入 EFI 系统分区的 EFI\Lenovo\Logo 目录。
/// </summary>
public static class BootLogo
{
    // LBLDESP：固件中的开机 Logo 配置变量（是否启用、支持的分辨率与格式）
    private const string LBLDESP = "LBLDESP";
    private const string LBLDESP_GUID = "{871455D0-5576-4FB8-9865-AF0824463B9E}";
    // LBLDVC：固件中的 Logo 文件 CRC32 校验变量，固件据此校验 EFI 分区中的图片
    private const string LBLDVC = "LBLDVC";
    private const string LBLDVC_GUID = "{871455D1-5576-4FB8-9865-AF0824463C9F}";

    // 变量属性：非易失 + BootService 可访问 + 运行时（OS）可访问
    private const uint SCOPE_ATTR = PInvokeExtensions.VARIABLE_ATTRIBUTE_NON_VOLATILE |
                                    PInvokeExtensions.VARIABLE_ATTRIBUTE_BOOTSERVICE_ACCESS |
                                    PInvokeExtensions.VARIABLE_ATTRIBUTE_RUNTIME_ACCESS;

    /// <summary>
    /// 判断当前机型是否支持修改开机 Logo。
    /// </summary>
    /// <returns>支持返回 true；机型不支持或变量无法读取时返回 false。</returns>
    public static async Task<bool> IsSupportedAsync()
    {
        try
        {
            var mi = await Compatibility.GetMachineInformationAsync().ConfigureAwait(false);
            if (!mi.Properties.SupportBootLogoChange)
                return false;

            // 机型标记支持还不够，需确认两个 UEFI 变量确实可读
            _ = GetInfo();
            _ = GetChecksum();
            return true;
        }
        catch
        {
            // 读取 UEFI 变量失败（如缺少特权、固件变量不存在）视为不支持
            return false;
        }
    }

    /// <summary>
    /// 获取开机 Logo 的当前状态与固件支持能力。
    /// </summary>
    /// <returns>元组：是否已启用、固件要求的分辨率、支持的图片格式数组、支持的扩展名数组。</returns>
    public static (bool, Resolution, ImageFormat[], string[]) GetStatus()
    {
        var info = GetInfo();
        return (info.Enabled == 1, new(info.SupportedWidth, info.SupportedHeight), info.SupportedFormat.ImageFormats().ToArray(), info.SupportedFormat.ExtensionFilters().ToArray());
    }

    /// <summary>
    /// 启用自定义开机 Logo。
    /// 执行顺序：校验图片 → 删除旧 Logo → 复制新文件并计算 CRC → 写入 CRC 与启用标志。
    /// </summary>
    /// <param name="sourcePath">源图片路径。</param>
    public static async Task EnableAsync(string sourcePath)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Enabling logo... [sourcePath={sourcePath}]");

        var info = GetInfo();

        ThrowIfImageInvalid(info, sourcePath);

        await DeleteMyLogoAsync().ConfigureAwait(false);
        var crc = await CopyMyLogoAsync(info, sourcePath).ConfigureAwait(false);

        // 必须先写入文件再写 CRC 与启用标志，否则固件可能校验到不完整的文件
        SetChecksum(crc);
        SetInfo(info with { Enabled = 1 });

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Enabled logo. [sourcePath={sourcePath}]");
    }

    /// <summary>
    /// 禁用自定义开机 Logo，恢复为固件默认画面。
    /// </summary>
    public static async Task DisableAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Disabling logo...");

        await DeleteMyLogoAsync().ConfigureAwait(false);

        // CRC 归零 + 清除启用标志，两者缺一可能导致固件仍尝试加载 Logo
        SetChecksum(0);
        SetInfo(GetInfo() with { Enabled = 0 });

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Disabled logo.");
    }

    /// <summary>
    /// 读取 LBLDESP 变量，获取开机 Logo 配置信息。
    /// </summary>
    /// <returns>开机 Logo 配置结构。</returns>
    /// <exception cref="CantSetUEFIPrivilegeException">无法启用 SE_SYSTEM_ENVIRONMENT 特权时抛出。</exception>
    private static unsafe BootLogoInfo GetInfo()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting info...");

        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<BootLogoInfo>());

        try
        {
            // 访问固件环境变量需要 SeSystemEnvironmentPrivilege，普通权限下 API 会失败
            if (!TokenManipulator.AddPrivileges(TokenManipulator.SE_SYSTEM_ENVIRONMENT_PRIVILEGE))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Cannot set UEFI privileges.");

                throw new CantSetUEFIPrivilegeException();
            }

            var ptrSize = (uint)Marshal.SizeOf<BootLogoInfo>();

            // 返回值是实际读取的字节数，与结构体大小不符说明变量缺失或类型不匹配
            var size = PInvoke.GetFirmwareEnvironmentVariableEx(LBLDESP, LBLDESP_GUID, ptr.ToPointer(), ptrSize, null);
            if (size != ptrSize)
                PInvokeExtensions.ThrowIfWin32Error("GetFirmwareEnvironmentVariableEx");

            var str = Marshal.PtrToStructure<BootLogoInfo>(ptr);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Retrieved info. [enabled={str.Enabled}, supportedWidth={str.SupportedWidth}, supportedHeight={str.SupportedHeight}, supportedFormat={(int)str.SupportedFormat}]");

            return str;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);

            // 特权必须及时撤销，避免长期持有系统级权限
            TokenManipulator.RemovePrivileges(TokenManipulator.SE_SYSTEM_ENVIRONMENT_PRIVILEGE);
        }
    }

    /// <summary>
    /// 写入 LBLDESP 变量，保存开机 Logo 配置信息。
    /// </summary>
    /// <param name="info">要写入的配置结构。</param>
    /// <exception cref="CantSetUEFIPrivilegeException">无法启用 SE_SYSTEM_ENVIRONMENT 特权时抛出。</exception>
    private static unsafe void SetInfo(BootLogoInfo info)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting info... [enabled={info.Enabled}, supportedWidth={info.SupportedWidth}, supportedHeight={info.SupportedHeight}, supportedFormat={(int)info.SupportedFormat}]");

        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<BootLogoInfo>());

        try
        {
            if (!TokenManipulator.AddPrivileges(TokenManipulator.SE_SYSTEM_ENVIRONMENT_PRIVILEGE))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Cannot set UEFI privileges.");

                throw new CantSetUEFIPrivilegeException();
            }

            Marshal.StructureToPtr(info, ptr, false);
            var ptrSize = (uint)Marshal.SizeOf<BootLogoInfo>();

            if (!PInvoke.SetFirmwareEnvironmentVariableEx(LBLDESP, LBLDESP_GUID, ptr.ToPointer(), ptrSize, SCOPE_ATTR))
                PInvokeExtensions.ThrowIfWin32Error("SetFirmwareEnvironmentVariableEx");

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Info set. [enabled={info.Enabled}, supportedWidth={info.SupportedWidth}, supportedHeight={info.SupportedHeight}, supportedFormat={(int)info.SupportedFormat}]");
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);

            TokenManipulator.RemovePrivileges(TokenManipulator.SE_SYSTEM_ENVIRONMENT_PRIVILEGE);
        }
    }

    /// <summary>
    /// 读取 LBLDVC 变量中的 Logo 文件 CRC32 校验值。
    /// </summary>
    /// <returns>CRC32 校验值。</returns>
    /// <exception cref="CantSetUEFIPrivilegeException">无法启用 SE_SYSTEM_ENVIRONMENT 特权时抛出。</exception>
    private static unsafe uint GetChecksum()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting checksum...");

        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<BootLogoChecksum>());

        try
        {
            if (!TokenManipulator.AddPrivileges(TokenManipulator.SE_SYSTEM_ENVIRONMENT_PRIVILEGE))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Cannot set UEFI privileges.");

                throw new CantSetUEFIPrivilegeException();
            }

            var ptrSize = (uint)Marshal.SizeOf<BootLogoChecksum>();

            var size = PInvoke.GetFirmwareEnvironmentVariableEx(LBLDVC, LBLDVC_GUID, ptr.ToPointer(), ptrSize, null);
            if (size != ptrSize)
                PInvokeExtensions.ThrowIfWin32Error("GetFirmwareEnvironmentVariableEx");

            var checksum = Marshal.PtrToStructure<BootLogoChecksum>(ptr).Crc;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Retrieved checksum. [checksum={checksum:X2}]");

            return checksum;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);

            TokenManipulator.RemovePrivileges(TokenManipulator.SE_SYSTEM_ENVIRONMENT_PRIVILEGE);
        }
    }

    /// <summary>
    /// 写入 LBLDVC 变量，保存 Logo 文件的 CRC32 校验值。
    /// </summary>
    /// <param name="checksum">CRC32 校验值；传 0 表示清除。</param>
    /// <exception cref="CantSetUEFIPrivilegeException">无法启用 SE_SYSTEM_ENVIRONMENT 特权时抛出。</exception>
    private static unsafe void SetChecksum(uint checksum)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting checksum... [checksum={checksum:X2}]");

        var str = new BootLogoChecksum { Crc = checksum };
        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<BootLogoChecksum>());

        try
        {
            if (!TokenManipulator.AddPrivileges(TokenManipulator.SE_SYSTEM_ENVIRONMENT_PRIVILEGE))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Cannot set UEFI privileges.");

                throw new CantSetUEFIPrivilegeException();
            }

            Marshal.StructureToPtr(str, ptr, false);
            var ptrSize = (uint)Marshal.SizeOf<BootLogoChecksum>();

            if (!PInvoke.SetFirmwareEnvironmentVariableEx(LBLDVC, LBLDVC_GUID, ptr.ToPointer(), ptrSize, SCOPE_ATTR))
                PInvokeExtensions.ThrowIfWin32Error("SetFirmwareEnvironmentVariableEx");

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Checksum set. [checksum={checksum:X2}]");
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);

            TokenManipulator.RemovePrivileges(TokenManipulator.SE_SYSTEM_ENVIRONMENT_PRIVILEGE);
        }
    }

    /// <summary>
    /// 把图片复制到 EFI 分区的 Logo 目录，并计算其 CRC32。
    /// 临时挂载 EFI 分区，操作完成后在 finally 中卸载。
    /// </summary>
    /// <param name="info">开机 Logo 配置（用于确定文件名中的分辨率）。</param>
    /// <param name="sourcePath">源图片路径。</param>
    /// <returns>复制后的文件 CRC32 值。</returns>
    /// <exception cref="CantMountUEFIPartitionException">EFI 分区挂载失败时抛出。</exception>
    /// <exception cref="NotEnoughSpaceOnUEFIPartitionException">EFI 分区空间不足时抛出。</exception>
    private static async Task<uint> CopyMyLogoAsync(BootLogoInfo info, string sourcePath)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Copying logo... [sourcePath={sourcePath}, enabled={info.Enabled}, supportedWidth={info.SupportedWidth}, supportedHeight={info.SupportedHeight}, supportedFormat={(int)info.SupportedFormat}]");

        char? drive = null;

        try
        {
            drive = await MountEfiPartitionAsync().ConfigureAwait(false);
            if (!drive.HasValue)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Cannot mount EFI partition.");

                throw new CantMountUEFIPartitionException();
            }

            // 提前检查剩余空间，避免复制到一半失败留下损坏文件
            if (new DriveInfo($"{drive}:").AvailableFreeSpace < new FileInfo(sourcePath).Length)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Not enough free space on EFI partition.");

                throw new NotEnoughSpaceOnUEFIPartitionException();
            }

            var destinationDirectory = Path.Combine($"{drive}:", "EFI", "Lenovo", "Logo");
            // 文件名需包含分辨率，固件按 LBLDESP 中记录的分辨率拼接同名文件
            var filename = $"mylogo_{info.SupportedWidth}x{info.SupportedHeight}{Path.GetExtension(sourcePath)}";
            var destinationPath = Path.Combine(destinationDirectory, filename);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Destination path: {destinationPath}");

            Directory.CreateDirectory(destinationDirectory);
            File.Copy(sourcePath, destinationPath, true);

            // 必须使用与固件一致的 CRC32（Adler）算法，否则固件校验失败仍显示默认画面
            var checksum = Crc32Adler.Calculate(destinationPath);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Logo copied. [checksum={checksum:X2}]");

            return checksum;
        }
        finally
        {
            // 无论成功失败都要卸载，避免占用某个盘符
            if (drive.HasValue)
                await UnMountEfiPartitionAsync(drive.Value).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 删除 EFI 分区中所有由本程序写入的 Logo 文件（mylogo*）。
    /// </summary>
    private static async Task DeleteMyLogoAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Deleting logos...");

        char? drive = null;

        try
        {
            drive = await MountEfiPartitionAsync().ConfigureAwait(false);
            if (!drive.HasValue)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Cannot mount EFI partition");

                throw new CantMountUEFIPartitionException();
            }

            var directoryPath = $@"{drive}:\EFI\Lenovo\Logo";
            if (!Directory.Exists(directoryPath))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"No logos to delete.");

                return;
            }

            // 只删除本程序生成的文件，保留固件原有的 Logo
            var files = Directory.EnumerateFiles(directoryPath, "mylogo*").ToArray();
            files.ForEach(File.Delete);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Logos deleted. [count={files.Length}]");
        }
        finally
        {
            if (drive.HasValue)
                await UnMountEfiPartitionAsync(drive.Value).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// 校验图片尺寸与格式是否被固件接受。
    /// </summary>
    /// <param name="info">开机 Logo 配置，提供固件要求的分辨率与格式。</param>
    /// <param name="sourcePath">待校验的图片路径。</param>
    /// <exception cref="InvalidBootLogoImageSizeException">尺寸不符时抛出。</exception>
    /// <exception cref="InvalidBootLogoImageFormatException">格式不符时抛出。</exception>
    private static void ThrowIfImageInvalid(BootLogoInfo info, string sourcePath)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Validating image... [sourcePath={sourcePath}, sourcePath={sourcePath}, enabled={info.Enabled}, supportedWidth={info.SupportedWidth}, supportedHeight={info.SupportedHeight}, supportedFormat={(int)info.SupportedFormat}]");

        using var image = Image.FromFile(sourcePath);

        if (info.SupportedWidth != image.Width || info.SupportedHeight != image.Height)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Invalid image size.");

            throw new InvalidBootLogoImageSizeException();
        }

        if (!info.SupportedFormat.ImageFormats().Contains(image.RawFormat))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Invalid image format.");

            throw new InvalidBootLogoImageFormatException();
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Image valid. [sourcePath={sourcePath}, sourcePath={sourcePath}, enabled={info.Enabled}, supportedWidth={info.SupportedWidth}, supportedHeight={info.SupportedHeight}, supportedFormat={(int)info.SupportedFormat}]");
    }

    /// <summary>
    /// 从字母表末尾向前查找一个未被占用的盘符，用于临时挂载 EFI 分区。
    /// </summary>
    /// <returns>可用盘符。</returns>
    private static char GetUnusedDriveLetter()
    {
        // ReSharper disable once StringLiteralTypo
        var letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
        var usedLetters = DriveInfo.GetDrives().Select(di => di.Name.First()).ToArray();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Used drive letters: {string.Join(",", usedLetters)}");

        // 从 Z 往前取，尽量避开系统常用的靠前盘符
        var letter = letters.Last(c => !usedLetters.Contains(c));

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Using '{letter}' letter.");

        return letter;
    }

    /// <summary>
    /// 通过 mountvol 把 EFI 系统分区挂载到空闲盘符。
    /// </summary>
    /// <returns>挂载成功返回盘符；失败返回 null。</returns>
    private static async Task<char?> MountEfiPartitionAsync()
    {
        var drive = GetUnusedDriveLetter();
        // /s 表示挂载 EFI 系统分区（需要管理员权限）
        var (result, _) = await CMD.RunAsync("mountvol", $"{drive}: /s").ConfigureAwait(false);
        if (result != 0)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to mount EFI partition at {drive}:");

            return null;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"EFI partition mounted at {drive}:");

        return drive;
    }

    /// <summary>
    /// 通过 mountvol 卸载指定盘符上的 EFI 系统分区。
    /// </summary>
    /// <param name="letter">盘符。</param>
    private static async Task UnMountEfiPartitionAsync(char letter)
    {
        // /d 表示删除挂载点
        var (result, _) = await CMD.RunAsync("mountvol", $"{letter}: /d").ConfigureAwait(false);

        if (result != 0)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to un-mount EFI partition from {letter}:");
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"EFI partition un-mounted from {letter}:.");
    }
}

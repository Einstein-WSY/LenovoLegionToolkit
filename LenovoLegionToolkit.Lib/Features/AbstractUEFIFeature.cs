using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Utils;
using Windows.Win32;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// 基于 UEFI 固件环境变量的特性基类。
/// 通过 Win32 API GetFirmwareEnvironmentVariableEx / SetFirmwareEnvironmentVariableEx
/// 直接读写指定 GUID 命名空间下的固件变量，需要 SE_SYSTEM_ENVIRONMENT_PRIVILEGE 权限。
/// </summary>
/// <typeparam name="T">特性状态枚举类型。</typeparam>
/// <param name="guid">UEFI 变量所属的命名空间 GUID（厂商自定义）。</param>
/// <param name="scopeName">UEFI 变量名称。</param>
/// <param name="scopeAttribute">写入变量时使用的固件属性标志。</param>
public abstract class AbstractUEFIFeature<T>(string guid, string scopeName, uint scopeAttribute)
    : IFeature<T> where T : struct, Enum, IComparable
{
    /// <inheritdoc/>
    public async Task<bool> IsSupportedAsync()
    {
        try
        {
            // 支持性以"能否成功读取一次状态"来判定：读不到即视为不支持
            _ = await GetStateAsync().ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public Task<T[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<T>());

    /// <inheritdoc/>
    public abstract Task<T> GetStateAsync();

    /// <inheritdoc/>
    public abstract Task SetStateAsync(T state);

    /// <summary>
    /// 从 UEFI 固件变量中读取一个结构体。
    /// </summary>
    /// <typeparam name="TS">要读取的结构体类型，需与固件中的二进制布局一致。</typeparam>
    /// <returns>反序列化后的结构体。</returns>
    /// <exception cref="InvalidOperationException">无法获取特权或读取固件变量失败时抛出。</exception>
    protected unsafe Task<TS> ReadFromUefiAsync<TS>() where TS : struct => Task.Run(() =>
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Reading from UEFI... [feature={GetType().Name}]");

        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<TS>());

        try
        {
            // 读写固件环境变量必须先提升到 SE_SYSTEM_ENVIRONMENT_PRIVILEGE 特权
            if (!TokenManipulator.AddPrivileges(TokenManipulator.SE_SYSTEM_ENVIRONMENT_PRIVILEGE))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Cannot set UEFI privileges [feature={GetType().Name}]");

                throw new InvalidOperationException("Cannot set privileges UEFI");
            }

            var ptrSize = (uint)Marshal.SizeOf<TS>();
            // GetFirmwareEnvironmentVariableEx 返回 0 表示读取失败（变量不存在或权限不足）
            if (PInvoke.GetFirmwareEnvironmentVariableEx(scopeName, guid, ptr.ToPointer(), ptrSize, null) != 0)
            {
                var result = Marshal.PtrToStructure<TS>(ptr);

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Read from UEFI successful [feature={GetType().Name}]");

                return result;
            }
            else
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Cannot read variable {scopeName} from UEFI [feature={GetType().Name}]");

                throw new InvalidOperationException($"Cannot read variable {scopeName} from UEFI");
            }
        }
        finally
        {
            // 无论成败都要释放非托管内存并移除特权，避免权限残留
            Marshal.FreeHGlobal(ptr);
            TokenManipulator.RemovePrivileges(TokenManipulator.SE_SYSTEM_ENVIRONMENT_PRIVILEGE);
        }
    });

    /// <summary>
    /// 将一个结构体写入 UEFI 固件变量。
    /// </summary>
    /// <typeparam name="TS">要写入的结构体类型。</typeparam>
    /// <param name="structure">要写入的结构体值。</param>
    /// <exception cref="InvalidOperationException">无法获取特权或写入固件变量失败时抛出。</exception>
    protected unsafe Task WriteToUefiAsync<TS>(TS structure) where TS : struct => Task.Run(() =>
    {
        var ptr = Marshal.AllocHGlobal(Marshal.SizeOf<TS>());

        try
        {
            if (!TokenManipulator.AddPrivileges(TokenManipulator.SE_SYSTEM_ENVIRONMENT_PRIVILEGE))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Cannot set UEFI privileges [feature={GetType().Name}]");

                throw new InvalidOperationException("Cannot set UEFI privileges");
            }

            // 结构体 -> 非托管内存，再整体写入固件变量
            Marshal.StructureToPtr(structure, ptr, false);
            var ptrSize = (uint)Marshal.SizeOf<TS>();
            if (!PInvoke.SetFirmwareEnvironmentVariableEx(scopeName, guid, ptr.ToPointer(), ptrSize, scopeAttribute))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Cannot write variable {scopeName} to UEFI [feature={GetType().Name}]");

                throw new InvalidOperationException($"Cannot write variable {scopeName} to UEFI");
            }
            else
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"WriteAsync to UEFI successful [feature={GetType().Name}]");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
            TokenManipulator.RemovePrivileges(TokenManipulator.SE_SYSTEM_ENVIRONMENT_PRIVILEGE);
        }
    });
}

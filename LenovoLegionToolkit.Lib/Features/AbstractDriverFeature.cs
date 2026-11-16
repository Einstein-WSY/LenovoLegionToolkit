using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;
using Microsoft.Win32.SafeHandles;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// 基于内核驱动 IOCTL 的特性基类。
/// 通过 DeviceIoControl 向联想电源/能量管理驱动发送控制码读写特性状态，
/// 子类负责实现底层 32 位缓冲值与枚举值之间的映射。
/// </summary>
/// <typeparam name="T">特性状态枚举类型。</typeparam>
/// <param name="driverHandleHandle">获取驱动设备句柄的工厂方法（每次调用可能返回新句柄）。</param>
/// <param name="controlCode">该特性对应的 IOCTL 控制码。</param>
public abstract class AbstractDriverFeature<T>(Func<SafeFileHandle> driverHandleHandle, uint controlCode) : IFeature<T> where T : struct, Enum, IComparable
{
    protected readonly uint ControlCode = controlCode;
    protected readonly Func<SafeFileHandle> DriverHandle = driverHandleHandle;

    // 缓存最近一次读取/设置的状态，部分特性的写入指令依赖"上一次状态"做过渡
    protected T LastState;

    /// <inheritdoc/>
    public virtual async Task<bool> IsSupportedAsync()
    {
        try
        {
            // 能成功读取一次即视为支持
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
    public virtual async Task<T> GetStateAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting state... [feature={GetType().Name}]");

        // 发送查询指令：inBuffer 通常为子类约定的"读取"标记值
        var outBuffer = await SendCodeAsync(DriverHandle(), ControlCode, GetInBufferValue()).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Buffer value: {outBuffer} [feature={GetType().Name}]");

        var state = await FromInternalAsync(outBuffer).ConfigureAwait(false);
        LastState = state;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State is {state} [feature={GetType().Name}]");

        return state;
    }

    /// <inheritdoc/>
    public virtual async Task SetStateAsync(T state)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting state to {state}... [feature={GetType().Name}]");

        // 一个状态可能需要按顺序发送多条指令（如先退出旧模式再进入新模式）
        var codes = await ToInternalAsync(state).ConfigureAwait(false);
        foreach (var code in codes)
            await SendCodeAsync(DriverHandle(), ControlCode, code).ConfigureAwait(false);
        LastState = state;

        await VerifyStateSetAsync(state).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State set to {state} [feature={GetType().Name}]");
    }

    // 把驱动返回的原始缓冲值转换为状态枚举
    protected abstract Task<T> FromInternalAsync(uint state);

    // 返回查询状态时使用的 inBuffer 值
    protected abstract uint GetInBufferValue();

    // 把目标状态转换为需要依次发送的 inBuffer 序列
    protected abstract Task<uint[]> ToInternalAsync(T state);

    /// <summary>
    /// 通过 DeviceIoControl 向驱动发送一次控制码。
    /// </summary>
    /// <param name="handle">已打开的驱动设备句柄。</param>
    /// <param name="controlCode">IOCTL 控制码。</param>
    /// <param name="inBuffer">输入缓冲值（4 字节）。</param>
    /// <returns>驱动返回的输出缓冲值。</returns>
    /// <exception cref="InvalidOperationException">DeviceIoControl 失败时抛出，包含 Win32 错误码。</exception>
    protected Task<uint> SendCodeAsync(SafeFileHandle handle, uint controlCode, uint inBuffer) => Task.Run(() =>
    {
        if (PInvokeExtensions.DeviceIoControl(handle, controlCode, inBuffer, out uint outBuffer))
            return outBuffer;

        var error = Marshal.GetLastWin32Error();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"DeviceIoControl returned 0, last error: {error} [feature={GetType().Name}]");

        throw new InvalidOperationException($"DeviceIoControl returned 0, last error: {error}");
    });

    // 写入后轮询校验：部分硬件异步生效，最多重试 10 次、每次间隔 50ms
    private async Task VerifyStateSetAsync(T state)
    {
        var retries = 0;
        var verified = false;

        while (retries < 10)
        {
            if (state.Equals(await GetStateAsync().ConfigureAwait(false)))
            {
                verified = true;
                break;
            }

            retries++;

            await Task.Delay(50).ConfigureAwait(false);
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Verify state {state} set {(verified ? "succeeded" : "failed")}. [feature={GetType().Name}]");
    }
}

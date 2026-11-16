using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// 基于通用 WMI 读/写委托的特性基类。
/// 与 <see cref="AbstractCapabilityFeature{T}"/> 不同，本类不绑定固定 CapabilityID，
/// 而是注入具体的读取/写入回调，便于复用同一套状态转换与支持性检测逻辑。
/// </summary>
/// <typeparam name="T">特性状态枚举类型。</typeparam>
/// <param name="getValue">读取底层原始 int 值的委托（通常是 WMI 方法调用）。</param>
/// <param name="setValue">写入底层原始 int 值的委托。</param>
/// <param name="isSupported">可选的支持性检测委托，返回大于 0 表示支持；为 null 时默认视为支持。</param>
/// <param name="offset">枚举值与底层 WMI 值之间的偏移量（部分接口的取值不从 0 开始）。</param>
public abstract class AbstractWmiFeature<T>(Func<Task<int>> getValue, Func<int, Task> setValue, Func<Task<int>>? isSupported = null, int offset = 0)
    : IFeature<T> where T : struct, Enum, IComparable
{
    /// <inheritdoc/>
    public async Task<bool> IsSupportedAsync()
    {
        try
        {
            if (isSupported is null)
                return true;

            // 通过 WMI 检测接口返回值判断：大于 0 即认为硬件支持
            return await isSupported().ConfigureAwait(false) > 0;
        }
        catch
        {
            // WMI 调用失败（机型不支持对应方法）时静默视为不支持
            return false;
        }
    }

    /// <inheritdoc/>
    public virtual Task<T[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<T>());

    /// <inheritdoc/>
    public async Task<T> GetStateAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Getting state... [feature={GetType().Name}]");

        var internalResult = await getValue().ConfigureAwait(false);
        // 减去 offset 把底层值映射回枚举值
        var result = FromInternal(internalResult);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"State is {result} [feature={GetType().Name}]");

        return result;
    }

    /// <inheritdoc/>
    public virtual async Task SetStateAsync(T state)
    {

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting state to {state}... [feature={GetType().Name}]");

        // 枚举值加上 offset 后写入底层接口
        await setValue(ToInternal(state)).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Set state to {state} [feature={GetType().Name}]");
    }

    // 枚举 -> 底层 WMI 值的映射（加 offset）
    private int ToInternal(T state) => (int)(object)state + offset;

    // 底层 WMI 值 -> 枚举的映射（减 offset）
    private T FromInternal(int state) => (T)(object)(state - offset);
}

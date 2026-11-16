using System;
using System.Threading.Tasks;
using NeoSmart.AsyncLock;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// 组合特性基类。当同一能力在不同机型上由不同实现（Capability / Driver / UEFI / Lighting 等）承担时，
/// 本类按构造时传入的顺序依次探测，选中第一个被硬件支持的实现并转发调用。
/// 解析结果会被缓存，避免反复探测 WMI/驱动。
/// </summary>
/// <typeparam name="T">特性状态枚举类型。</typeparam>
/// <param name="features">候选特性实现列表，顺序即优先级。</param>
public abstract class AbstractCompositeFeature<T>(params IFeature<T>[] features) : IFeature<T> where T : struct
{
    // 保证并发调用时只解析一次
    private readonly AsyncLock _lock = new();

    private bool _resolved;
    private IFeature<T>? _feature;

    /// <inheritdoc/>
    public async Task<bool> IsSupportedAsync()
    {
        var feature = await ResolveInternalAsync().ConfigureAwait(false);
        if (feature is null)
            return false;
        return await feature.IsSupportedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<T[]> GetAllStatesAsync()
    {
        var feature = await ResolveInternalAsync().ConfigureAwait(false)
                      ?? throw new InvalidOperationException($"No supported feature found [type={GetType().Name}");
        return await feature.GetAllStatesAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<T> GetStateAsync()
    {
        var feature = await ResolveInternalAsync().ConfigureAwait(false)
                      ?? throw new InvalidOperationException($"No supported feature found [type={GetType().Name}");
        return await feature.GetStateAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SetStateAsync(T state)
    {
        var feature = await ResolveInternalAsync().ConfigureAwait(false)
                      ?? throw new InvalidOperationException($"No supported feature found [type={GetType().Name}");
        await feature.SetStateAsync(state).ConfigureAwait(false);
    }

    /// <summary>
    /// 依次探测候选实现，返回第一个支持的实现。
    /// 子类可重写以自定义优先级策略。
    /// </summary>
    /// <returns>被选中的实现；全部不支持时返回 <c>null</c>。</returns>
    protected virtual async Task<IFeature<T>?> ResolveAsync()
    {
        foreach (var feature in features)
        {
            if (!await feature.IsSupportedAsync().ConfigureAwait(false))
                continue;

            return feature;
        }

        return null;
    }

    // 带缓存的解析入口：加锁后仅在首次调用时执行实际探测
    private async Task<IFeature<T>?> ResolveInternalAsync()
    {
        using (await _lock.LockAsync().ConfigureAwait(false))
        {
            if (_resolved)
                return _feature;

            _feature = await ResolveAsync().ConfigureAwait(false);
            _resolved = true;
            return _feature;
        }
    }
}

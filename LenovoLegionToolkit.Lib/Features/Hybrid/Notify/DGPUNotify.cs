using System;
using System.Threading.Tasks;
using NeoSmart.AsyncLock;

namespace LenovoLegionToolkit.Lib.Features.Hybrid.Notify;

/// <summary>
/// dGPU 状态通知的组合实现。
/// 与 <see cref="IGPUModeFeature"/> 采用一致的策略：默认只使用稳定的 GameZone 实现，
/// 开启实验性 GPU 工作模式后才回退到 CapabilityData / FeatureFlags 实现。
/// 解析结果会被缓存（并发安全）。
/// </summary>
public class DGPUNotify(DGPUGamezoneNotify gamezoneNotify, DGPUCapabilityNotify capabilityNotify, DGPUFeatureFlagsNotify featureFlagsNotify) : IDGPUNotify
{
    // 保证并发调用时只解析一次
    private readonly AsyncLock _lock = new();

    private bool _resolved;
    private IDGPUNotify? _dgpuNotify;

    private DGPUCapabilityNotify CapabilityNotify => capabilityNotify;
    private DGPUFeatureFlagsNotify FeatureFlagsNotify => featureFlagsNotify;
    private DGPUGamezoneNotify GamezoneNotify => gamezoneNotify;

    /// <summary>
    /// 是否启用实验性的 GPU 工作模式实现（CapabilityData / FeatureFlags）。
    /// 为 <c>false</c> 时仅使用 GameZone 实现。
    /// </summary>
    public bool ExperimentalGPUWorkingMode { get; set; }

    /// <summary>
    /// 汇总事件：无论最终使用哪个底层实现，订阅者都能收到其通知。
    /// 订阅时同时挂到三套实现上，取消订阅时同样从三处移除。
    /// </summary>
    public event EventHandler<bool>? Notified
    {
        add
        {
            CapabilityNotify.Notified += value;
            FeatureFlagsNotify.Notified += value;
            GamezoneNotify.Notified += value;
        }
        remove
        {
            CapabilityNotify.Notified -= value;
            FeatureFlagsNotify.Notified -= value;
            GamezoneNotify.Notified -= value;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsSupportedAsync()
    {
        var dgpuNotify = await ResolveInternalAsync().ConfigureAwait(false);
        if (dgpuNotify is null)
            return false;
        return await dgpuNotify.IsSupportedAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<bool> IsDGPUAvailableAsync()
    {
        var dgpuNotify = await ResolveInternalAsync().ConfigureAwait(false) ?? throw new InvalidOperationException($"No supported feature found [type={GetType().Name}]");
        return await dgpuNotify.IsDGPUAvailableAsync().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task NotifyAsync(bool publish = true)
    {
        var dgpuNotify = await ResolveInternalAsync().ConfigureAwait(false) ?? throw new InvalidOperationException($"No supported feature found [type={GetType().Name}]");
        await dgpuNotify.NotifyAsync(publish).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task NotifyLaterIfNeededAsync()
    {
        var dgpuNotify = await ResolveInternalAsync().ConfigureAwait(false) ?? throw new InvalidOperationException($"No supported feature found [type={GetType().Name}]");
        await dgpuNotify.NotifyLaterIfNeededAsync().ConfigureAwait(false);
    }

    // 按优先级选择一个可用的底层实现
    private async Task<IDGPUNotify?> ResolveAsync()
    {
        // 非实验模式：只接受 GameZone 实现
        if (!ExperimentalGPUWorkingMode)
            return await GamezoneNotify.IsSupportedAsync().ConfigureAwait(false) ? GamezoneNotify : null;

        // 实验模式：优先 CapabilityData，再退到 FeatureFlags
        if (await CapabilityNotify.IsSupportedAsync().ConfigureAwait(false))
            return CapabilityNotify;

        if (await FeatureFlagsNotify.IsSupportedAsync().ConfigureAwait(false))
            return FeatureFlagsNotify;

        return null;
    }

    // 带缓存的解析入口：加锁后仅在首次调用时执行实际探测
    private async Task<IDGPUNotify?> ResolveInternalAsync()
    {
        using (await _lock.LockAsync().ConfigureAwait(false))
        {
            if (_resolved)
                return _dgpuNotify;

            _dgpuNotify = await ResolveAsync().ConfigureAwait(false);
            _resolved = true;
            return _dgpuNotify;
        }
    }
}

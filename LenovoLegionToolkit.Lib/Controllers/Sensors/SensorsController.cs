using System;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Controllers.Sensors;

/// <summary>
/// 传感器控制器门面：按优先级（V3 → V2 → V1）探测并缓存可用的具体实现。
/// 探测结果会缓存，避免每次读取数据都重新做支持性检测。
/// </summary>
public class SensorsController(
    SensorsControllerV1 controllerV1,
    SensorsControllerV2 controllerV2,
    SensorsControllerV3 controllerV3)
    : ISensorsController
{
    private ISensorsController? _controller; // 已探测到的可用实现（缓存）

    /// <summary>
    /// 判断本机是否有可用的传感器实现。
    /// </summary>
    public async Task<bool> IsSupportedAsync() => await GetControllerAsync().ConfigureAwait(false) is not null;

    /// <summary>
    /// 初始化已探测到的实现。
    /// </summary>
    /// <exception cref="InvalidOperationException">无可用实现时抛出。</exception>
    public async Task PrepareAsync()
    {
        var controller = await GetControllerAsync().ConfigureAwait(false) ?? throw new InvalidOperationException("No supported controller found");
        await controller.PrepareAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 读取传感器数据。
    /// </summary>
    /// <exception cref="InvalidOperationException">无可用实现时抛出。</exception>
    public async Task<SensorsData> GetDataAsync()
    {
        var controller = await GetControllerAsync().ConfigureAwait(false) ?? throw new InvalidOperationException("No supported controller found");
        return await controller.GetDataAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// 读取 CPU/GPU 风扇转速。
    /// </summary>
    /// <exception cref="InvalidOperationException">无可用实现时抛出。</exception>
    public async Task<(int cpuFanSpeed, int gpuFanSpeed)> GetFanSpeedsAsync()
    {
        var controller = await GetControllerAsync().ConfigureAwait(false) ?? throw new InvalidOperationException("No supported controller found");
        return await controller.GetFanSpeedsAsync().ConfigureAwait(false);
    }

    // 按新到旧的顺序探测，优先使用信息最全的新版本实现
    private async Task<ISensorsController?> GetControllerAsync()
    {
        if (_controller is not null)
            return _controller;

        if (await controllerV3.IsSupportedAsync().ConfigureAwait(false))
            return _controller = controllerV3;

        if (await controllerV2.IsSupportedAsync().ConfigureAwait(false))
            return _controller = controllerV2;

        if (await controllerV1.IsSupportedAsync().ConfigureAwait(false))
            return _controller = controllerV1;

        return null;
    }
}

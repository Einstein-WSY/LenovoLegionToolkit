using System;

namespace LenovoLegionToolkit.Lib.Features.Hybrid;

/// <summary>
/// iGPU 模式切换失败时抛出的异常。
/// 通常表示固件在切换后执行 dGPU 断电检查失败（例如独显仍被占用），
/// 此时系统可能需要在重启后才会真正生效。
/// </summary>
/// <param name="igpuMode">切换失败的目标 iGPU 模式。</param>
public class IGPUModeChangeException(IGPUModeState igpuMode) : Exception
{
    /// <summary>切换失败的目标 iGPU 模式。</summary>
    public IGPUModeState IGPUMode { get; } = igpuMode;
}

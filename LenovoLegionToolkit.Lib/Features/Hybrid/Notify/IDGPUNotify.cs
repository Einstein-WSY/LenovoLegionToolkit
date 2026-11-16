using System;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Features.Hybrid.Notify;

/// <summary>
/// dGPU（独立显卡）状态通知接口。
/// 在切换到纯核显模式时，需要通知固件"弹出"独显以使其断电省电；
/// 切回混合模式时则需要重新唤醒独显。不同机型通过不同 WMI 接口实现该能力。
/// </summary>
public interface IDGPUNotify
{
    /// <summary>
    /// dGPU 可用性发生变化时触发。<c>true</c> 表示独显可用（已唤醒/存在）。
    /// </summary>
    public event EventHandler<bool>? Notified;

    /// <summary>异步检测当前机型是否支持 dGPU 状态通知。</summary>
    Task<bool> IsSupportedAsync();

    /// <summary>异步判断 dGPU 当前是否可被系统识别（已上电且无错误状态）。</summary>
    Task<bool> IsDGPUAvailableAsync();

    /// <summary>执行一次 dGPU 状态通知。</summary>
    /// <param name="publish">是否触发 <see cref="Notified"/> 事件。</param>
    Task NotifyAsync(bool publish = true);

    /// <summary>延迟 5 秒后（若期间未收到其他事件）再执行通知，用于等待硬件状态稳定。</summary>
    Task NotifyLaterIfNeededAsync();
}

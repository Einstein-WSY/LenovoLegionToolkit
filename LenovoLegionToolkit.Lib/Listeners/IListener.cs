using System;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Listeners;

/// <summary>
/// 通用监听器接口：用于订阅系统或硬件层面的状态变更事件（如电源状态、键盘灯效、热模式等）。
/// </summary>
/// <typeparam name="TEventArgs">监听器触发事件时携带的事件参数类型。</typeparam>
public interface IListener<TEventArgs> where TEventArgs : EventArgs
{
    /// <summary>
    /// 状态变更事件。当底层来源（WMI、事件日志、Windows 消息等）检测到变更并处理完成后触发。
    /// </summary>
    event EventHandler<TEventArgs>? Changed;

    /// <summary>
    /// 启动监听，开始接收底层事件。
    /// </summary>
    /// <returns>表示启动操作完成的任务。</returns>
    Task StartAsync();

    /// <summary>
    /// 停止监听并释放底层资源（如 WMI 订阅、事件句柄）。
    /// </summary>
    /// <returns>表示停止操作完成的任务。</returns>
    Task StopAsync();
}

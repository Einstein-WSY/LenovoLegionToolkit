using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System.Management;

namespace LenovoLegionToolkit.Lib.AutoListeners;

/// <summary>
/// 进程结束监听器（基于 WMI 的 Win32_ProcessStopTrace 事件）。
/// </summary>
public class InstanceStoppedEventAutoAutoListener : AbstractAutoListener<InstanceStoppedEventAutoAutoListener.ChangedEventArgs>
{
    /// <summary>
    /// 进程结束事件参数。
    /// </summary>
    public class ChangedEventArgs(int processId, string processName) : EventArgs
    {
        public int ProcessId { get; } = processId; // 结束的进程 ID
        public string ProcessName { get; } = processName; // 结束的进程名称
    }

    private IDisposable? _disposable; // WMI 事件订阅句柄

    protected override Task StartAsync()
    {
        _disposable = WMI.Win32.ProcessStopTrace.Listen(Handle);
        return Task.CompletedTask;
    }


    protected override Task StopAsync()
    {
        _disposable?.Dispose();
        _disposable = null;
        return Task.CompletedTask;
    }

    private void Handle(int processId, string processName) => RaiseChanged(new ChangedEventArgs(processId, processName));
}

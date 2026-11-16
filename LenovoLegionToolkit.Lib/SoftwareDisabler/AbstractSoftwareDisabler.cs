using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;
using TaskService = Microsoft.Win32.TaskScheduler.TaskService;

namespace LenovoLegionToolkit.Lib.SoftwareDisabler;

/// <summary>
/// 软件禁用操作的异常：封装注册计划任务或更改服务启停失败的原因，便于上层提示用户。
/// </summary>
public class SoftwareDisablerException(string message, Exception innerException) : Exception(message, innerException);

/// <summary>
/// 联想后台软件禁用器基类：通过停止服务、禁用计划任务、结束进程三步，
/// 让 Lenovo Vantage / Legion Zone / FnKeys 等官方软件停止与硬件通信，避免与本工具争抢硬件控制权或重复响应快捷键。
/// </summary>
public abstract class AbstractSoftwareDisabler
{
    /// <summary>
    /// 状态刷新事件参数。
    /// </summary>
    public class AbstractSoftwareDisablerEventArgs : EventArgs
    {
        /// <summary>
        /// 刷新后的软件状态。
        /// </summary>
        public SoftwareStatus Status { get; init; }
    }

    /// <summary>
    /// 需要启用/禁用的计划任务文件夹路径集合。
    /// </summary>
    protected abstract IEnumerable<string> ScheduledTasksPaths { get; }

    /// <summary>
    /// 需要启用/停止的 Windows 服务名集合。
    /// </summary>
    protected abstract IEnumerable<string> ServiceNames { get; }

    /// <summary>
    /// 需要结束的进程名集合（按前缀匹配）。
    /// </summary>
    protected abstract IEnumerable<string> ProcessNames { get; }

    /// <summary>
    /// 每次状态被重新计算后触发，携带最新状态。
    /// </summary>
    public event EventHandler<AbstractSoftwareDisablerEventArgs>? OnRefreshed;

    /// <summary>
    /// 查询软件当前状态：通过检查相关服务/进程是否在运行判断是否启用，通过服务是否存在判断是否安装。
    /// </summary>
    /// <returns>软件状态：启用、已禁用或未安装。</returns>
    public Task<SoftwareStatus> GetStatusAsync() => Task.Run(() =>
    {
        bool isEnabled;
        bool isInstalled;

        try
        {
            var services = RunningServices().ToArray();
            var processes = RunningProcesses().ToArray();

            if (Log.Instance.IsTraceEnabled)
            {
                Log.Instance.Trace($"Running services count: {services.Length}. [type={GetType().Name}, services={string.Join(",", services)}]");
                Log.Instance.Trace($"Running processes count: {processes.Length}. [type={GetType().Name}, processes={string.Join(",", processes)}]");
            }

            isEnabled = services.Length != 0 || processes.Length != 0;
            isInstalled = IsInstalled();
        }
        catch (Exception ex)
        {
            // 查询过程可能因权限或服务管理 API 异常失败，此时保守地视为未启用且未安装
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Exception while getting status. [type={GetType().Name}]", ex);

            isEnabled = false;
            isInstalled = false;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Status: {isEnabled},{isInstalled} [type={GetType().Name}]");

        SoftwareStatus status;

        if (isEnabled)
            status = SoftwareStatus.Enabled;
        else if (!isInstalled)
            status = SoftwareStatus.NotFound;
        else
            status = SoftwareStatus.Disabled;

        OnRefreshed?.Invoke(this, new() { Status = status });

        return status;
    });

    /// <summary>
    /// 启用官方软件：恢复计划任务与服务，使其重新随系统运行。
    /// </summary>
    public virtual Task EnableAsync() => Task.Run(async () =>
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Enabling... [type={GetType().Name}]");

        SetScheduledTasksEnabled(true);
        SetServicesEnabled(true);

        // 重新计算状态以触发 OnRefreshed，让界面同步最新状态
        _ = await GetStatusAsync().ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Enabled [type={GetType().Name}]");
    });

    /// <summary>
    /// 禁用官方软件：禁用计划任务、停止服务并结束残留进程。
    /// </summary>
    public virtual Task DisableAsync() => Task.Run(async () =>
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Disabling... [type={GetType().Name}]");

        SetScheduledTasksEnabled(false);
        SetServicesEnabled(false);
        // 顺序很重要：先停服务再杀进程，否则服务可能自动重启被杀的进程
        await KillProcessesAsync().ConfigureAwait(false);

        _ = await GetStatusAsync().ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Disabled [type={GetType().Name}]");
    });

    /// <summary>
    /// 判断软件是否已安装（以目标服务是否存在为准）。
    /// </summary>
    /// <returns>存在任一目标服务时返回 true。</returns>
    private bool IsInstalled() => ServiceController.GetServices().Any(s => ServiceNames.Contains(s.ServiceName));

    /// <summary>
    /// 枚举当前处于运行状态的目标服务。
    /// </summary>
    /// <returns>正在运行的服务名集合。</returns>
    private IEnumerable<string> RunningServices()
    {
        var services = ServiceController.GetServices();
        return ServiceNames.Where(s => IsServiceEnabled(s, services));
    }

    /// <summary>
    /// 枚举当前运行的目标进程（按进程名前缀匹配）。
    /// </summary>
    /// <returns>匹配到的进程名集合。</returns>
    protected virtual IEnumerable<string> RunningProcesses()
    {
        foreach (var process in Process.GetProcesses())
        {
            foreach (var processName in ProcessNames)
            {
                var name = string.Empty;

                try
                {
                    name = process.ProcessName;
                    if (!name.StartsWith(processName, StringComparison.InvariantCultureIgnoreCase))
                        continue;
                }
                // 部分系统进程（如权限不足或已退出）读取 ProcessName 会抛异常，逐项忽略即可
                catch {  /* Ignored. */ }

                if (!string.IsNullOrEmpty(name))
                    yield return name;
            }
        }
    }

    /// <summary>
    /// 判断指定服务当前是否未处于停止状态（即视为"在运行"）。
    /// </summary>
    /// <param name="serviceName">服务名。</param>
    /// <param name="services">已枚举的服务集合，避免重复查询。</param>
    /// <returns>服务存在且未停止时返回 true。</returns>
    private static bool IsServiceEnabled(string serviceName, IEnumerable<ServiceController> services)
    {
        try
        {
            var service = services.FirstOrDefault(s => s.ServiceName == serviceName);
            if (service is null)
                return false;

            return service.Status is not ServiceControllerStatus.Stopped;
        }
        catch (InvalidOperationException)
        {
            // 服务在枚举与访问之间被卸载/删除时会抛此异常
            return false;
        }
    }

    /// <summary>
    /// 批量设置计划任务文件夹下所有任务的启用状态。
    /// </summary>
    /// <param name="enabled">true 为启用，false 为禁用。</param>
    private void SetScheduledTasksEnabled(bool enabled)
    {
        var taskService = TaskService.Instance;
        foreach (var path in ScheduledTasksPaths)
            SetTasksInFolderEnabled(taskService, path, enabled);
    }

    /// <summary>
    /// 设置指定文件夹内所有计划任务的启用状态；文件夹不存在时跳过。
    /// </summary>
    /// <param name="taskService">任务计划服务实例。</param>
    /// <param name="path">任务文件夹路径。</param>
    /// <param name="enabled">目标启用状态。</param>
    /// <exception cref="SoftwareDisablerException">注册任务更改失败时抛出。</exception>
    private void SetTasksInFolderEnabled(TaskService taskService, string path, bool enabled)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Setting tasks in folder {path} to {enabled}. [type={GetType().Name}]");

        var folder = taskService.GetFolder(path);
        if (folder is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Folder not found [path={path}, type={GetType().Name}]]");

            return;
        }

        foreach (var task in folder.Tasks.ToArray())
        {
            task.Definition.Settings.Enabled = enabled;
            try
            {
                task.RegisterChanges();
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to register changes on task {task.Name} in {task.Path}.", ex);

                throw new SoftwareDisablerException($"Failed to register changes on task {task.Name} in {task.Path} [type={GetType().Name}]", ex);
            }
        }
    }

    /// <summary>
    /// 批量设置目标服务的启动类型与运行状态。
    /// </summary>
    /// <param name="enabled">true 为自动启动并启动服务；false 为禁用启动并停止服务。</param>
    private void SetServicesEnabled(bool enabled)
    {
        foreach (var serviceName in ServiceNames)
            SetServiceEnabled(serviceName, enabled);
    }

    /// <summary>
    /// 设置单个服务的启动类型（自动/禁用），并按需启动或停止该服务。
    /// </summary>
    /// <param name="serviceName">服务名。</param>
    /// <param name="enabled">true 为启用，false 为禁用。</param>
    /// <exception cref="SoftwareDisablerException">服务不存在或无法修改时抛出。</exception>
    private void SetServiceEnabled(string serviceName, bool enabled)
    {
        try
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Setting service {serviceName} to {enabled}. [type={GetType().Name}]");

            if (!ServiceController.GetServices().Any(s => s.ServiceName == serviceName))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Service {serviceName} not found. [type={GetType().Name}]");

                return;
            }

            var service = new ServiceController(serviceName);

            try
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Changing service {serviceName} start mode to {enabled}.  [type={GetType().Name}]");

                service.ChangeStartMode(enabled);

                if (enabled)
                {
                    if (service.Status != ServiceControllerStatus.Running)
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Starting service {serviceName}... [type={GetType().Name}]");
                        service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running);
                    }
                    else
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Will not start service {serviceName}. [status={service.Status}, type={GetType().Name}]]");
                    }
                }
                else
                {
                    if (service.CanStop)
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Stopping service {serviceName}... [type={GetType().Name}]");
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped);
                    }
                    else
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Will not stop service {serviceName}. [status={service.Status}, canStop={service.CanStop}, type={GetType().Name}]]");
                    }
                }
            }
            finally
            {
                service.Close();
            }
        }
        catch (InvalidOperationException ex) when (ex.InnerException is Win32Exception { NativeErrorCode: 1060 })
        {
            // 错误码 1060 表示服务不存在，转换为业务异常供上层提示
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Service {serviceName} could not be set to {enabled}");

            throw new SoftwareDisablerException(serviceName, ex);
        }
    }

    /// <summary>
    /// 结束所有匹配目标名称的进程（含子进程），供禁用时清理残留。
    /// </summary>
    protected virtual async Task KillProcessesAsync()
    {
        foreach (var process in Process.GetProcesses())
            foreach (var processName in ProcessNames)
            {
                try
                {
                    if (process.ProcessName.StartsWith(processName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        process.Kill(true);
                        await process.WaitForExitAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Couldn't kill process.", ex);
                }
            }
    }
}

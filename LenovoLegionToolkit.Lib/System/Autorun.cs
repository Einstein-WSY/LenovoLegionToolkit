using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using LenovoLegionToolkit.Lib.Utils;
using Microsoft.Win32.TaskScheduler;

namespace LenovoLegionToolkit.Lib.System;

/// <summary>
/// 开机自启管理辅助类。基于 Windows 任务计划程序（Task Scheduler）实现，
/// 相比注册表 Run 键可使用最高权限运行并支持登录延迟启动。
/// </summary>
public static class Autorun
{
    // 固定 GUID 后缀的任务名，保证多次注册/删除指向同一个计划任务
    private const string TASK_NAME = "LenovoLegionToolkit_Autorun_6efcc882-924c-4cbc-8fec-f45c25696f98";

    /// <summary>
    /// 获取当前开机自启状态。
    /// </summary>
    public static AutorunState State
    {
        get
        {
            var task = TaskService.Instance.GetTask(TASK_NAME);
            if (task is null)
                return AutorunState.Disabled;

            // 登录触发器设置了正的 Delay 即视为"延迟启动"
            var delayed = task.Definition.Triggers.OfType<LogonTrigger>().FirstOrDefault()?.Delay > TimeSpan.Zero;
            return delayed ? AutorunState.EnabledDelayed : AutorunState.Enabled;
        }
    }

    /// <summary>
    /// 校验计划任务是否与当前程序版本一致，不一致时按原有延迟设置重新注册。
    /// 用于程序升级后自动修正过期任务定义。
    /// </summary>
    public static void Validate()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Validating autorun...");

        var currentTask = TaskService.Instance.GetTask(TASK_NAME);
        if (currentTask is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Autorun is not enabled.");
            return;
        }

        var mainModule = Process.GetCurrentProcess().MainModule;
        if (mainModule is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Main module is null.");
            return;
        }

        var fileVersion = mainModule.FileVersionInfo.FileVersion;
        if (fileVersion is null)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"File version is null.");
            return;
        }

        // 用任务定义的 Data 字段记录写入时的版本号，与当前文件版本比对
        if (currentTask.Definition.Data == fileVersion)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Autorun settings seems to be fine.");
            return;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Enabling autorun again...");

        var delayed = currentTask.Definition.Triggers.OfType<LogonTrigger>().FirstOrDefault()?.Delay > TimeSpan.Zero;

        Enable(delayed);
    }

    /// <summary>
    /// 设置开机自启状态。
    /// </summary>
    /// <param name="state">目标状态。</param>
    public static void Set(AutorunState state)
    {
        if (state == AutorunState.Disabled)
            Disable();
        else
            Enable(state == AutorunState.EnabledDelayed);
    }

    /// <summary>
    /// 创建（或重建）开机自启计划任务。
    /// </summary>
    /// <param name="delayed">是否延迟 30 秒启动，用于避免登录时与系统组件争抢资源。</param>
    private static void Enable(bool delayed)
    {
        // 先删除旧任务，保证注册时是干净的定义
        Disable();

        var mainModule = Process.GetCurrentProcess().MainModule ?? throw new InvalidOperationException("Main Module cannot be null");
        var filename = mainModule.FileName ?? throw new InvalidOperationException("Current process file name cannot be null");
        var fileVersion = mainModule.FileVersionInfo.FileVersion ?? throw new InvalidOperationException("Current process file version cannot be null");
        var currentUser = WindowsIdentity.GetCurrent().Name;

        var ts = TaskService.Instance;
        var td = ts.NewTask();
        // Data 字段仅用于存放版本号，供 Validate 判断任务是否需要重建
        td.Data = fileVersion;
        td.Principal.UserId = currentUser;
        // Highest 表示以最高权限运行，避免 UAC 提示或权限不足
        td.Principal.RunLevel = TaskRunLevel.Highest;
        td.Triggers.Add(new LogonTrigger { UserId = currentUser, Delay = new TimeSpan(0, 0, delayed ? 30 : 0) });
        // 以 --minimized 参数启动，使程序直接进入托盘
        td.Actions.Add($"\"{filename}\"", "--minimized");
        // 允许电池供电时启动/继续运行，避免笔记本未接电源时不自动启动
        td.Settings.DisallowStartIfOnBatteries = false;
        td.Settings.StopIfGoingOnBatteries = false;
        // 不限执行时长，防止任务被计划程序在默认 3 天后终止
        td.Settings.ExecutionTimeLimit = TimeSpan.Zero;
        ts.RootFolder.RegisterTaskDefinition(TASK_NAME, td);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Autorun enabled");
    }

    /// <summary>
    /// 删除开机自启计划任务。任务不存在时静默忽略。
    /// </summary>
    private static void Disable()
    {
        try
        {
            TaskService.Instance.RootFolder.DeleteTask(TASK_NAME);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Autorun disabled");
        }
        catch
        {
            // 任务本就不存在时 DeleteTask 会抛异常，此处属预期情况
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Autorun was not enabled");
        }
    }
}

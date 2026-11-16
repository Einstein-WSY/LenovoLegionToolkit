using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NvAPIWrapper.GPU;
using NvAPIWrapper.Native;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 基于 NVAPI 的物理 GPU 扩展方法，用于查询正在使用该 GPU 的进程。
/// </summary>
public static class NVAPIExtensions
{
    /// <summary>
    /// 需要排除的进程名：桌面窗口管理器与资源管理器常驻占用 GPU，属于系统进程而非实际使用者。
    /// </summary>
    private static readonly string[] Exclusions =
    [
        "dwm.exe",
        "explorer.exe",
    ];

    /// <summary>
    /// 获取当前正在使用指定物理 GPU 的活动进程列表（已排除系统常驻进程）。
    /// </summary>
    /// <param name="gpu">目标物理 GPU。</param>
    /// <returns>使用该 GPU 的进程列表；进程在查询期间已退出则自动跳过。</returns>
    public static List<Process> GetActiveProcesses(PhysicalGPU gpu)
    {
        var processes = new List<Process>();
        var apps = GPUApi.QueryActiveApps(gpu.Handle).Where(app => !Exclusions.Contains(app.ProcessName, StringComparer.InvariantCultureIgnoreCase));

        foreach (var app in apps)
        {
            try
            {
                var process = Process.GetProcessById(app.ProcessId);
                processes.Add(process);
            }
            // 进程可能在查询与获取之间刚好退出，按进程 ID 获取会抛 ArgumentException，忽略即可
            catch (ArgumentException) { }
        }

        return processes;
    }
}

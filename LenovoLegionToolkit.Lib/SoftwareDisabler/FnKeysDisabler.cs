using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System;

namespace LenovoLegionToolkit.Lib.SoftwareDisabler;

/// <summary>
/// Lenovo Fn 快捷键软件禁用器：停止 LenovoFnAndFunctionKeys 服务、结束 LenovoUtility 相关进程，
/// 并额外通过注册表控制 UWP 版 LenovoUtility 的开机自启，避免与本工具的快捷键处理冲突。
/// </summary>
public class FnKeysDisabler : AbstractSoftwareDisabler
{
    // Fn 快捷键软件不使用计划任务，故为空集合
    protected override IEnumerable<string> ScheduledTasksPaths => [];
    protected override IEnumerable<string> ServiceNames => ["LenovoFnAndFunctionKeys"];
    protected override IEnumerable<string> ProcessNames => ["LenovoUtilityUI", "LenovoUtilityService", "LenovoSmartKey"];

    /// <summary>
    /// 启用 Fn 快捷键软件，并恢复 UWP 版 LenovoUtility 的开机自启。
    /// </summary>
    public override async Task EnableAsync()
    {
        await base.EnableAsync().ConfigureAwait(false);
        SetUwpStartup("LenovoUtility", "LenovoUtilityID", true);
    }

    /// <summary>
    /// 禁用 Fn 快捷键软件，并关闭 UWP 版 LenovoUtility 的开机自启。
    /// </summary>
    public override async Task DisableAsync()
    {
        await base.DisableAsync().ConfigureAwait(false);
        SetUwpStartup("LenovoUtility", "LenovoUtilityID", false);
    }

    /// <summary>
    /// 在基类进程匹配基础上追加"Lenovo Hotkeys"：其进程名统一为 utility，
    /// 只能通过文件描述识别，避免误杀其他同名程序。
    /// </summary>
    protected override IEnumerable<string> RunningProcesses()
    {
        var result = base.RunningProcesses().ToList();

        try
        {
            foreach (var process in Process.GetProcessesByName("utility"))
            {
                var description = process.MainModule?.FileVersionInfo.FileDescription;
                if (description is null)
                    continue;

                if (description.Equals("Lenovo Hotkeys", StringComparison.InvariantCultureIgnoreCase))
                    result.Add(process.ProcessName);
            }
        }
        // 访问其他用户/受保护进程的 MainModule 可能抛异常，忽略该进程即可
        catch {  /* Ignored. */ }

        return result;
    }

    /// <summary>
    /// 在基类结束进程的基础上，额外结束描述为"Lenovo Hotkeys"的 utility 进程。
    /// </summary>
    protected override async Task KillProcessesAsync()
    {
        await base.KillProcessesAsync().ConfigureAwait(false);

        try
        {
            foreach (var process in Process.GetProcessesByName("utility"))
            {
                var description = process.MainModule?.FileVersionInfo.FileDescription;
                if (description is null)
                    continue;

                if (!description.Equals("Lenovo Hotkeys", StringComparison.InvariantCultureIgnoreCase))
                    continue;

                process.Kill();
                await process.WaitForExitAsync().ConfigureAwait(false);
            }
        }
        catch {  /* Ignored. */ }
    }

    /// <summary>
    /// 通过注册表修改 UWP 应用的启动状态：State 值 0x2 表示启用自启，0x1 表示禁用。
    /// </summary>
    /// <param name="appPattern">用于匹配应用包目录名的关键字。</param>
    /// <param name="subKeyName">应用数据子键名（通常为应用 ID）。</param>
    /// <param name="enabled">true 为启用自启，false 为禁用。</param>
    private static void SetUwpStartup(string appPattern, string subKeyName, bool enabled)
    {
        const string hive = "HKEY_CURRENT_USER";
        const string subKey = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData";
        const string valueName = "State";

        // UWP 应用包目录名包含应用标识，需先找到实际目录名再拼接子键
        var startupKey = Registry.GetSubKeys(hive, subKey).FirstOrDefault(s => s.Contains(appPattern, StringComparison.CurrentCultureIgnoreCase));
        if (startupKey is null)
            return;

        startupKey = Path.Combine(startupKey, subKeyName);

        Registry.SetValue(hive, startupKey, valueName, enabled ? 0x2 : 0x1);
    }
}

using System;
using System.Linq;
using LenovoLegionToolkit.Lib.Utils;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace LenovoLegionToolkit.Lib.System;

/// <summary>
/// 系统 PATH 环境变量管理辅助类，用于把 CLI 目录加入或移出用户 PATH。
/// </summary>
public static class SystemPath
{
    /// <summary>CLI 可执行文件所在目录（程序安装目录）。</summary>
    private static string CLIPath => Folders.Program;

    /// <summary>
    /// 判断当前用户 PATH 环境变量中是否已包含 CLI 目录。
    /// </summary>
    /// <returns>已包含返回 true，否则返回 false。</returns>
    public static bool HasCLI()
    {
        // 从注册表读取原始 PATH（未做环境变量展开，避免误判），再按分号拆分比对
        return Registry.GetValue("HKEY_CURRENT_USER", "Environment", "PATH", string.Empty, true)
            .Split(';')
            .Contains(CLIPath);
    }

    /// <summary>
    /// 将 CLI 目录加入或移出当前用户的 PATH 环境变量，并广播设置变更通知。
    /// </summary>
    /// <param name="enabled">true 表示加入 PATH，false 表示移除。</param>
    public static void SetCLI(bool enabled)
    {
        var value = Registry.GetValue("HKEY_CURRENT_USER", "Environment", "PATH", string.Empty, true)
            .Split(';')
            .ToList();

        if (enabled)
        {
            if (value.Contains(CLIPath))
                return;

            value.Add(CLIPath);
        }
        else
        {
            value.Remove(CLIPath);
        }

        // 必须以 ExpandString 类型写入，才能保留 PATH 中形如 %USERPROFILE% 的可展开变量
        Registry.SetValue("HKEY_CURRENT_USER",
            "Environment",
            "PATH",
            string.Join(';', value),
            valueKind: RegistryValueKind.ExpandString);

        Notify();
    }

    /// <summary>
    /// 广播 WM_SETTINGCHANGE 消息，通知已运行的进程重新读取环境变量。
    /// 需使用 fixed 固定字符串指针，把托管字符串以 LPARAM 形式传给窗口过程。
    /// </summary>
    private static unsafe void Notify()
    {
        const string environment = "Environment";
        fixed (void* ptr = environment)
        {
            PInvoke.SendNotifyMessage(HWND.HWND_BROADCAST, PInvoke.WM_SETTINGCHANGE, 0, new IntPtr(ptr));
        }
    }
}

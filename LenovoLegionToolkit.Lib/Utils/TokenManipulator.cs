using System.Linq;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Security;

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// 进程访问令牌权限（Privilege）调整工具，用于在需要时临时提升当前进程的权限。
/// </summary>
public static class TokenManipulator
{
    /// <summary>备份文件与目录所需的权限名称。</summary>
    public const string SE_BACKUP_PRIVILEGE = "SeBackupPrivilege";
    /// <summary>还原文件与目录所需的权限名称。</summary>
    public const string SE_RESTORE_PRIVILEGE = "SeRestorePrivilege";
    /// <summary>取得文件或其他对象所有权所需的权限名称。</summary>
    public const string SE_TAKE_OWNERSHIP_PRIVILEGE = "SeTakeOwnershipPrivilege";
    /// <summary>修改系统环境变量所需的权限名称。</summary>
    public const string SE_SYSTEM_ENVIRONMENT_PRIVILEGE = "SeSystemEnvironmentPrivilege";

    /// <summary>
    /// 为当前进程令牌启用指定的一个或多个权限。
    /// </summary>
    /// <param name="privileges">权限名称集合，例如 <see cref="SE_BACKUP_PRIVILEGE"/>。</param>
    /// <returns>全部权限均调整成功时返回 true。</returns>
    public static bool AddPrivileges(params string[] privileges) => AdjustPrivileges(privileges, true);

    /// <summary>
    /// 调整当前进程令牌中的指定权限（移除语义，即禁用该权限）。
    /// </summary>
    /// <param name="privileges">权限名称集合。</param>
    /// <returns>全部权限均调整成功时返回 true。</returns>
    public static bool RemovePrivileges(params string[] privileges) => AdjustPrivileges(privileges, false);

    /// <summary>
    /// 打开当前进程令牌并逐个调整指定权限。
    /// </summary>
    /// <param name="privileges">权限名称集合。</param>
    /// <param name="enable">true 表示启用权限，false 表示禁用。</param>
    /// <returns>全部权限均调整成功时返回 true。</returns>
    private static bool AdjustPrivileges(string[] privileges, bool enable)
    {
        SafeProcessHandle? safeHandle = null;
        SafeFileHandle? safeTokenHandle = null;

        try
        {
            safeHandle = new SafeProcessHandle(PInvoke.GetCurrentProcess(), true);
            if (!PInvoke.OpenProcessToken(safeHandle, TOKEN_ACCESS_MASK.TOKEN_ADJUST_PRIVILEGES | TOKEN_ACCESS_MASK.TOKEN_QUERY, out safeTokenHandle) && safeTokenHandle is null)
                return false;

            return privileges.All(p => AdjustPrivilege(safeTokenHandle, p, enable));
        }
        finally
        {
            // 句柄均在 finally 中释放，确保异常路径下也不会泄漏。
            safeTokenHandle?.Dispose();
            safeHandle?.Dispose();
        }
    }

    /// <summary>
    /// 调整单个权限：先查询 LUID，再按启用/禁用状态写入令牌。
    /// </summary>
    /// <param name="safeTokenHandle">已打开的进程令牌句柄。</param>
    /// <param name="privilegeName">权限名称。</param>
    /// <param name="enable">true 表示启用，false 表示禁用。</param>
    /// <returns>调整是否成功。</returns>
    private static unsafe bool AdjustPrivilege(SafeFileHandle safeTokenHandle, string privilegeName, bool enable)
    {
        PInvoke.LookupPrivilegeValue(null, privilegeName, out var luid);

        var state = new TOKEN_PRIVILEGES { PrivilegeCount = 1 };
        state.Privileges[0] = new LUID_AND_ATTRIBUTES
        {
            Luid = luid,
            // 启用时置位 SE_PRIVILEGE_ENABLED，否则写入 0 表示移除该权限。
            Attributes = enable ? TOKEN_PRIVILEGES_ATTRIBUTES.SE_PRIVILEGE_ENABLED : 0
        };

        return PInvoke.AdjustTokenPrivileges(safeTokenHandle, false, &state, 0, null, null);
    }
}

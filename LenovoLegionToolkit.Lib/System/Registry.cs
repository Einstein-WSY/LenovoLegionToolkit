using System;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Registry;

// ReSharper disable StringLiteralTypo

namespace LenovoLegionToolkit.Lib.System;

/// <summary>
/// 注册表读写与监听辅助类。
/// 统一以"hive 字符串 + 子键路径"的形式访问注册表，并在权限不足时提供可选的权限修复逻辑。
/// </summary>
public static class Registry
{
    /// <summary>
    /// 基于 Win32 RegNotifyChangeKeyValue 监听注册表键变化（阻塞式循环检测）。
    /// </summary>
    /// <param name="hive">根键名，如 HKEY_CURRENT_USER。</param>
    /// <param name="subKey">子键路径。</param>
    /// <param name="includeSubtreeChanges">是否同时监听子键的变化。</param>
    /// <param name="handler">检测到变化时执行的回调。</param>
    /// <returns>异步可释放对象，释放时取消监听并等待后台线程结束。</returns>
    public static IAsyncDisposable ObserveKey(string hive, string subKey, bool includeSubtreeChanges, Action handler)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var task = Task.Run(() => Handler(cancellationTokenSource.Token), cancellationTokenSource.Token);

        return new LambdaAsyncDisposable(async () =>
        {
            await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
            await task.ConfigureAwait(false);
        });

        void Handler(CancellationToken token)
        {
            try
            {
                using var baseKey = GetBaseKey(hive);
                using var key = baseKey.OpenSubKey(subKey) ?? throw new InvalidOperationException($"Key {subKey} could not be opened");

                var resetEvent = new ManualResetEvent(false);

                while (true)
                {
                    // REG_NOTIFY_THREAD_AGNOSTIC 允许在任意线程等待，避免线程亲和性问题；
                    // 最后一个参数 true 表示异步通知（事件对象由系统置位）
                    var regNotifyChangeKeyValueResult = PInvoke.RegNotifyChangeKeyValue(key.Handle,
                        includeSubtreeChanges,
                        REG_NOTIFY_FILTER.REG_NOTIFY_CHANGE_LAST_SET | REG_NOTIFY_FILTER.REG_NOTIFY_THREAD_AGNOSTIC,
                        resetEvent.SafeWaitHandle,
                        true);
                    if (regNotifyChangeKeyValueResult != WIN32_ERROR.NO_ERROR)
                        PInvokeExtensions.ThrowIfWin32Error("RegNotifyChangeKeyValue");

                    // 同时等待"注册表变化"与"取消令牌"，任一触发即返回
                    WaitHandle.WaitAny([resetEvent, token.WaitHandle]);
                    token.ThrowIfCancellationRequested();

                    handler();

                    resetEvent.Reset();
                }
            }
            catch (OperationCanceledException) { }
            catch (ThreadAbortException) { }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Unknown error.", ex);
            }
        }
    }

    /// <summary>
    /// 基于 WMI 的 RegistryValueChangeEvent 监听单个注册表值的变化。
    /// </summary>
    /// <param name="hive">根键名；传入 HKCU 时会被转换为当前用户 SID，因为 WMI 事件只支持 HKEY_USERS 形式。</param>
    /// <param name="path">子键路径。</param>
    /// <param name="valueName">要监听的值名称。</param>
    /// <param name="handler">值变化时执行的回调。</param>
    /// <returns>可释放对象，释放后停止监听（ManagementEventWatcher 实现 IDisposable）。</returns>
    public static IDisposable ObserveValue(string hive, string path, string valueName, Action handler)
    {
        // WMI 注册表事件位于 HKEY_USERS 命名空间下，需把 HKCU 换成对应用户 SID
        if (hive is "HKEY_CURRENT_USER" or "HKCU")
            hive = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("Current user value is null");

        // WQL 查询字符串中路径分隔符需转义为双反斜杠
        var pathFormatted = @$"SELECT * FROM RegistryValueChangeEvent WHERE Hive = 'HKEY_USERS' AND KeyPath = '{hive}\\{path.Replace(@"\", @"\\")}' AND ValueName = '{valueName}'";

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Starting listener... [hive={hive}, pathFormatted={pathFormatted}, key={valueName}]");

        var watcher = new ManagementEventWatcher(pathFormatted);
        watcher.EventArrived += (_, e) =>
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Event arrived [classPath={e.NewEvent.ClassPath}, hive={hive}, pathFormatted={pathFormatted}, key={valueName}]");

            handler();
        };
        watcher.Start();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Started listener [hive={hive}, pathFormatted={pathFormatted}, key={valueName}]");

        return watcher;
    }

    /// <summary>
    /// 判断注册表键是否存在。
    /// </summary>
    /// <param name="hive">根键名。</param>
    /// <param name="subKey">子键路径。</param>
    /// <returns>存在返回 true；访问被拒绝等异常情况下返回 false。</returns>
    public static bool KeyExists(string hive, string subKey)
    {
        try
        {
            using var baseKey = GetBaseKey(hive);
            using var registryKey = baseKey.OpenSubKey(subKey);
            return registryKey is not null;
        }
        catch
        {
            // 权限不足或根键名称非法时视为不存在，调用方无需再处理异常
            return false;
        }
    }

    /// <summary>
    /// 判断注册表值是否存在。
    /// </summary>
    /// <param name="hive">根键名。</param>
    /// <param name="subKey">子键路径。</param>
    /// <param name="valueName">值名称。</param>
    /// <returns>存在返回 true；读取异常返回 false。</returns>
    public static bool ValueExists(string hive, string subKey, string valueName)
    {
        try
        {
            var keyName = Path.Combine(hive, subKey);
            var value = Microsoft.Win32.Registry.GetValue(keyName, valueName, null);
            return value is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 获取指定键下的所有子键名称（含父路径前缀）。
    /// </summary>
    /// <param name="hive">根键名。</param>
    /// <param name="subKey">子键路径。</param>
    /// <returns>子键相对路径数组；键不存在时返回空数组。</returns>
    public static string[] GetSubKeys(string hive, string subKey)
    {
        using var baseKey = GetBaseKey(hive);
        return baseKey.OpenSubKey(subKey)?.GetSubKeyNames().Select(s => Path.Combine(subKey, s)).ToArray() ?? [];
    }

    /// <summary>
    /// 读取注册表值。
    /// </summary>
    /// <typeparam name="T">期望的值类型。</typeparam>
    /// <param name="hive">根键名。</param>
    /// <param name="subKey">子键路径。</param>
    /// <param name="valueName">值名称。</param>
    /// <param name="defaultValue">值不存在或类型不匹配时返回的默认值。</param>
    /// <param name="doNotExpand">是否禁止展开值中的环境变量（如 %SystemRoot%）。</param>
    /// <returns>读取到的值；无法转换时返回 defaultValue。</returns>
    public static T GetValue<T>(string hive, string subKey, string valueName, T defaultValue, bool doNotExpand = false)
    {
        using var baseKey = GetBaseKey(hive);
        var value = baseKey.OpenSubKey(subKey)?.GetValue(valueName, defaultValue, doNotExpand ? RegistryValueOptions.DoNotExpandEnvironmentNames : RegistryValueOptions.None);

        // 注册表实际类型可能与泛型参数不同（如 REG_SZ 读成 int），此时安全降级
        if (value is not T t)
            return defaultValue;

        return t;
    }

    /// <summary>
    /// 写入注册表值。
    /// </summary>
    /// <typeparam name="T">值类型。</typeparam>
    /// <param name="hive">根键名。</param>
    /// <param name="subKey">子键路径。</param>
    /// <param name="valueName">值名称。</param>
    /// <param name="value">要写入的值。</param>
    /// <param name="fixPermissions">遇到权限不足时是否尝试取得所有权并授予完全控制权限后重试。</param>
    /// <param name="valueKind">注册表值类型；Unknown 表示由运行时按值类型推断。</param>
    /// <exception cref="UnauthorizedAccessException">无写入权限且未启用或未能完成权限修复时抛出。</exception>
    public static void SetValue<T>(string hive, string subKey, string valueName, T value, bool fixPermissions = false, RegistryValueKind valueKind = RegistryValueKind.Unknown) where T : notnull
    {
        try
        {
            Microsoft.Win32.Registry.SetValue(@$"{hive}\{subKey}", valueName, value, valueKind);
        }
        catch (UnauthorizedAccessException)
        {
            // 该键常被 OEM 锁权限（如 HKLM 下的 Legion 相关键），此处按需提升后重试一次
            if (fixPermissions && AddPermissions(hive, subKey))
                SetValue(hive, subKey, valueName, value, false, valueKind);
            else
                throw;
        }
    }

    /// <summary>
    /// 删除整个注册表子树。
    /// </summary>
    /// <param name="hive">根键名。</param>
    /// <param name="subKey">子键路径。</param>
    public static void Delete(string hive, string subKey)
    {
        using var baseKey = GetBaseKey(hive);
        using var key = baseKey.OpenSubKey(subKey);
        if (key is null)
            return;
        baseKey.DeleteSubKeyTree(subKey);
    }

    /// <summary>
    /// 为指定注册表键添加当前用户的完全控制权限。
    /// 流程：取得所有权 → 打开键 → 添加 Allow 规则 → 恢复原所有者。
    /// </summary>
    /// <param name="hive">根键名。</param>
    /// <param name="subKey">子键路径。</param>
    /// <returns>成功授予权限返回 true。</returns>
    private static bool AddPermissions(string hive, string subKey)
    {
        IdentityReference? originalOwner = null;

        try
        {
            var current = WindowsIdentity.GetCurrent();
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (current is null)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Could not get current user.");

                return false;
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Attempting to add permissions to {hive}\\{subKey} for {current.Name}...");

            var user = current.User;
            if (user is null)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Could not get current security identifier of user {current.Name}.");

                return false;
            }

            // 只有成为所有者后才能修改该键的 DACL，故先取得所有权
            if (!TakeOwnership(hive, subKey, user, out originalOwner))
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Could not take ownership of {hive}\\{subKey}. [user={user}, originalOwner={originalOwner}]");

                return false;
            }

            using var baseKey = GetBaseKey(hive);
            using var key = baseKey.OpenSubKey(subKey, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.ChangePermissions | RegistryRights.ReadKey);
            if (key is null)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to open key {hive}\\{subKey} for {current.Name}.");

                return false;
            }

            var accessControl = key.GetAccessControl();

            const RegistryRights rights = RegistryRights.FullControl;
            const AccessControlType type = AccessControlType.Allow;
            accessControl.AddAccessRule(new(user, rights, type));
            key.SetAccessControl(accessControl);

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Permissions added on {hive}\\{subKey} for {current.Name}. [rights={rights}, type={type}]");

            return true;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to add permissions for {hive}\\{subKey}.", ex);

            throw;
        }
        finally
        {
            // 无论成功与否都尝试把所有者还原，避免改变系统原有安全配置
            if (originalOwner is not null)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Restoring ownership of {hive}\\{subKey} to {originalOwner}...");

                if (TakeOwnership(hive, subKey, originalOwner, out _))
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Ownership of {hive}\\{subKey} restored to {originalOwner}.");
                }
                else
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Ownership of {hive}\\{subKey} NOT restored {originalOwner}.");
                }
            }
        }
    }

    /// <summary>
    /// 取得注册表键的所有权。
    /// 需临时启用 SE_BACKUP / SE_RESTORE / SE_TAKE_OWNERSHIP 特权，操作结束后立即撤销。
    /// </summary>
    /// <param name="hive">根键名。</param>
    /// <param name="subKey">子键路径。</param>
    /// <param name="reference">新的所有者身份。</param>
    /// <param name="previousIdentityReference">输出原所有者身份，便于调用方后续还原。</param>
    /// <returns>取得所有权成功返回 true。</returns>
    private static bool TakeOwnership(string hive, string subKey, IdentityReference reference, out IdentityReference? previousIdentityReference)
    {
        previousIdentityReference = null;

        try
        {
            if (!TokenManipulator.AddPrivileges(TokenManipulator.SE_BACKUP_PRIVILEGE, TokenManipulator.SE_RESTORE_PRIVILEGE, TokenManipulator.SE_TAKE_OWNERSHIP_PRIVILEGE))
                return false;

            using var baseKey = GetBaseKey(hive);
            using var key = baseKey.OpenSubKey(subKey, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.TakeOwnership);
            if (key is null)
                return false;

            var accessControl = key.GetAccessControl();

            // 以 NTAccount 形式读取原所有者，便于后续原样还原
            previousIdentityReference = accessControl.GetOwner(typeof(NTAccount));
            if (previousIdentityReference is null)
                return false;

            accessControl.SetOwner(reference);
            key.SetAccessControl(accessControl);

            return true;
        }
        finally
        {
            // 特权只在本方法期间需要，务必撤销以降低安全风险
            _ = TokenManipulator.RemovePrivileges(TokenManipulator.SE_BACKUP_PRIVILEGE, TokenManipulator.SE_RESTORE_PRIVILEGE, TokenManipulator.SE_TAKE_OWNERSHIP_PRIVILEGE);
        }
    }

    /// <summary>
    /// 把根键名称（支持缩写与全称）映射为对应的 RegistryKey。
    /// </summary>
    /// <param name="hive">根键名称。</param>
    /// <returns>对应的注册表根键。</returns>
    /// <exception cref="ArgumentException">根键名称无法识别时抛出。</exception>
    private static RegistryKey GetBaseKey(string hive) => hive switch
    {
        "HKLM" or "HKEY_LOCAL_MACHINE" => Microsoft.Win32.Registry.LocalMachine,
        "HKCU" or "HKEY_CURRENT_USER" => Microsoft.Win32.Registry.CurrentUser,
        "HKU" or "HKEY_USERS" => Microsoft.Win32.Registry.Users,
        "HKCR" or "HKEY_CLASSES_ROOT" => Microsoft.Win32.Registry.ClassesRoot,
        "HKCC" or "HKEY_CURRENT_CONFIG" => Microsoft.Win32.Registry.CurrentConfig,
        _ => throw new ArgumentException(@"Unknown hive.", nameof(hive))
    };
}

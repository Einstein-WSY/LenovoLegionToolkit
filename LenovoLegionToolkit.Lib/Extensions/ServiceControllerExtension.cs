using System.Runtime.InteropServices;
using System.ServiceProcess;
using Windows.Win32;
using Windows.Win32.System.Services;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供 <see cref="ServiceController"/> 的扩展方法，用于修改服务的启动类型。
/// </summary>
internal static class ServiceControllerExtension
{
    /// <summary>
    /// 修改 Windows 服务的启动类型：启用时设为自动启动，禁用时设为禁用。
    /// <para>
    /// 通过 SCM（服务控制管理器）句柄直接调用 <c>ChangeServiceConfig</c>，
    /// 因为 .NET 的 <see cref="ServiceController"/> 不提供修改启动类型的托管 API。
    /// </para>
    /// </summary>
    /// <param name="svc">目标服务。</param>
    /// <param name="enabled">为 <c>true</c> 设为自动启动，为 <c>false</c> 设为禁用。</param>
    /// <exception cref="ExternalException">打开服务管理器或服务失败时抛出。</exception>
    public static unsafe void ChangeStartMode(this ServiceController svc, bool enabled)
    {
        using var scManagerHandle = PInvoke.OpenSCManager(null as string, null, PInvoke.SC_MANAGER_ALL_ACCESS);
        if (scManagerHandle.IsInvalid)
            throw new ExternalException("Open Service Manager Error");

        using var serviceHandle = PInvoke.OpenService(scManagerHandle, svc.ServiceName, PInvoke.SERVICE_CHANGE_CONFIG);
        if (serviceHandle.IsInvalid)
            throw new ExternalException("Open Service Error");

        var result = PInvoke.ChangeServiceConfig(serviceHandle,
            (ENUM_SERVICE_TYPE)PInvoke.SERVICE_NO_CHANGE,
            enabled ? SERVICE_START_TYPE.SERVICE_AUTO_START : SERVICE_START_TYPE.SERVICE_DISABLED,
            SERVICE_ERROR.SERVICE_ERROR_NORMAL,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        if (result)
            return;

        PInvokeExtensions.ThrowIfWin32Error($"Could not change service: {svc.ServiceName}");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Power;

namespace LenovoLegionToolkit.Lib.Controllers;

/// <summary>
/// Windows 电源计划（Power Plan）控制器：枚举系统电源计划、按映射切换计划，并可写入计划内的亮度参数。
/// </summary>
public class WindowsPowerPlanController(ApplicationSettings settings, VantageDisabler vantageDisabler)
{
    // 默认（平衡）电源计划的固定 GUID
    private static readonly Guid DefaultPowerPlan = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e");

    /// <summary>
    /// 枚举所有可用电源计划，并标记当前激活项。
    /// </summary>
    /// <returns>电源计划序列。</returns>
    public IEnumerable<WindowsPowerPlan> GetPowerPlans()
    {
        var activePowerPlanGuid = GetActivePowerPlanGuid();
        foreach (var powerPlanGuid in GetPowerPlanGuids())
        {
            var powerPlaneName = GetPowerPlanName(powerPlanGuid);
            yield return new WindowsPowerPlan(powerPlanGuid, powerPlaneName, powerPlanGuid == activePowerPlanGuid);
        }
    }

    /// <summary>
    /// 按用户映射切换到对应电源计划；未配置时回退到默认（平衡）计划。
    /// </summary>
    /// <param name="powerModeState">要映射的 Legion 电源模式。</param>
    /// <param name="alwaysActivateDefaults">为 true 时即使使用默认计划也强制激活。</param>
    public async Task SetPowerPlanAsync(PowerModeState powerModeState, bool alwaysActivateDefaults = false)
    {
        // 仅在"映射到 Windows 电源计划"模式下生效
        if (settings.Store.PowerModeMappingMode is not PowerModeMappingMode.WindowsPowerPlan)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Ignoring... [powerModeMappingMode={settings.Store.PowerModeMappingMode}]");
            return;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Activating... [powerModeState={powerModeState}, alwaysActivateDefaults={alwaysActivateDefaults}]");

        var powerPlanId = settings.Store.PowerPlans.GetValueOrDefault(powerModeState);
        var isDefault = false;

        if (powerPlanId == Guid.Empty)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Power plan for power mode {powerModeState} was not found in settings");

            powerPlanId = DefaultPowerPlan;
            isDefault = true;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Power plan to be activated is {powerPlanId} [isDefault={isDefault}]");

        if (!await ShouldSetPowerPlanAsync(alwaysActivateDefaults, isDefault).ConfigureAwait(false))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Power plan {powerPlanId} will not be activated [isDefault={isDefault}]");

            return;
        }

        var powerPlans = GetPowerPlans().ToArray();

        if (Log.Instance.IsTraceEnabled)
        {
            Log.Instance.Trace($"Available power plans:");
            foreach (var powerPlan in powerPlans)
                Log.Instance.Trace($" - {powerPlan}");
        }

        var powerPlanToActivate = powerPlans.FirstOrDefault(pp => pp.Guid == powerPlanId);
        if (powerPlanToActivate.Equals(default(WindowsPowerPlan)))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Power plan {powerPlanId} was not found");
            return;
        }

        if (powerPlanToActivate.IsActive)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Power plan {powerPlanToActivate.Guid} is already active. [name={powerPlanToActivate.Name}]");
            return;
        }

        SetActivePowerPlan(powerPlanToActivate.Guid);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Power plan {powerPlanToActivate.Guid} activated. [name={powerPlanToActivate.Name}]");
    }

    /// <summary>
    /// 把亮度值写入指定电源计划的 AC 与 DC 设置（视频子组下的"显示亮度"）。
    /// </summary>
    /// <param name="windowsPowerPlan">目标电源计划。</param>
    /// <param name="brightness">亮度值（0-255）。</param>
    public void SetPowerPlanParameter(WindowsPowerPlan windowsPowerPlan, Brightness brightness)
    {
        PInvoke.PowerWriteACValueIndex(NullSafeHandle.Null, windowsPowerPlan.Guid, PInvoke.GUID_VIDEO_SUBGROUP, PInvokeExtensions.DISPLAY_BRIGTHNESS_SETTING_GUID, brightness.Value);
        PInvoke.PowerWriteDCValueIndex(NullSafeHandle.Null, windowsPowerPlan.Guid, PInvoke.GUID_VIDEO_SUBGROUP, PInvokeExtensions.DISPLAY_BRIGTHNESS_SETTING_GUID, brightness.Value);
    }

    // 判断是否应真正切换计划：默认计划仅在显式要求时才切换，其余情况需 Vantage 未占用电源管理
    private async Task<bool> ShouldSetPowerPlanAsync(bool alwaysActivateDefaults, bool isDefault)
    {
        if (isDefault && alwaysActivateDefaults)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Power plan is default and always active defaults is set");

            return true;
        }

        var status = await vantageDisabler.GetStatusAsync().ConfigureAwait(false);
        if (status is SoftwareStatus.NotFound or SoftwareStatus.Disabled)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Vantage is active [status={status}]");

            return true;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Criteria for activation not met [isDefault={isDefault}, alwaysActivateDefaults={alwaysActivateDefaults}, status={status}]");

        return false;
    }

    // 通过 PowerEnumerate 逐项读取所有电源计划 GUID，直到返回失败视为枚举结束
    private static unsafe List<Guid> GetPowerPlanGuids()
    {
        var list = new List<Guid>();

        var bufferSize = (uint)Marshal.SizeOf<Guid>();
        var buffer = new byte[bufferSize];

        fixed (byte* bufferPtr = buffer)
        {
            uint index = 0;
            while (PInvoke.PowerEnumerate(null, null, null, POWER_DATA_ACCESSOR.ACCESS_SCHEME, index, bufferPtr, ref bufferSize) == WIN32_ERROR.ERROR_SUCCESS)
            {
                list.Add(new Guid(buffer));
                index++;
            }
        }

        return list;
    }

    // 读取电源计划的友好名称（UTF-16 字符串）；失败时退回 GUID 字符串
    private static unsafe string GetPowerPlanName(Guid powerPlanGuid)
    {
        var nameSize = 2048u; // 预分配 2KB 缓冲区，足够容纳本地化的计划名称
        var namePtr = Marshal.AllocHGlobal((int)nameSize);

        try
        {
            if (PInvoke.PowerReadFriendlyName(null, powerPlanGuid, null, null, (byte*)namePtr.ToPointer(), ref nameSize) != WIN32_ERROR.ERROR_SUCCESS)
                PInvokeExtensions.ThrowIfWin32Error("PowerReadFriendlyName");

            return Marshal.PtrToStringUni(namePtr) ?? string.Empty;
        }
        catch
        {
            return powerPlanGuid.ToString();
        }
        finally
        {
            Marshal.FreeHGlobal(namePtr);
        }
    }

    private static unsafe Guid GetActivePowerPlanGuid()
    {
        if (PInvoke.PowerGetActiveScheme(null, out var guid) != WIN32_ERROR.ERROR_SUCCESS)
            PInvokeExtensions.ThrowIfWin32Error("PowerGetActiveScheme");

        try
        {
            return *guid;
        }
        finally
        {
            // PowerGetActiveScheme 分配的缓冲区需调用方用 LocalFree 释放（FreeHGlobal 即调用 LocalFree）
            Marshal.FreeHGlobal(new IntPtr(guid));
        }
    }

    private static void SetActivePowerPlan(Guid powerPlanGuid)
    {
        if (PInvoke.PowerSetActiveScheme(null, powerPlanGuid) != WIN32_ERROR.ERROR_SUCCESS)
            PInvokeExtensions.ThrowIfWin32Error("PowerSetActiveScheme");
    }
}

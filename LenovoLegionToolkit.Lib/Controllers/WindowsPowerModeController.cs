using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace LenovoLegionToolkit.Lib.Controllers;

/// <summary>
/// Windows 电源模式（"电源模式"覆盖层，即 overlay scheme）控制器。
/// 通过 PowerSetActiveOverlayScheme 与注册表双写，确保设置在系统 UI 中正确显示。
/// </summary>
public partial class WindowsPowerModeController(ApplicationSettings settings, IMainThreadDispatcher mainThreadDispatcher)
{
    private const string POWER_SCHEMES_HIVE = "HKEY_LOCAL_MACHINE";
    private const string POWER_SCHEMES_SUBKEY = "SYSTEM\\CurrentControlSet\\Control\\Power\\User\\PowerSchemes";
    private const string ACTIVE_OVERLAY_AC_POWER_SCHEME_KEY = "ActiveOverlayAcPowerScheme"; // 接通电源时的覆盖方案
    private const string ACTIVE_OVERLAY_DC_POWER_SCHEME_KEY = "ActiveOverlayDcPowerScheme"; // 使用电池时的覆盖方案

    // Windows 内置的电源模式 GUID（系统固定值）
    private static readonly Guid DefaultPowerPlan = Guid.Parse("381b4222-f694-41f0-9685-ff5bb260df2e"); // 平衡（基础方案）
    private static readonly Guid BestPowerEfficiency = Guid.Parse("961cc777-2547-4f9d-8174-7d86181b8a7a"); // 最佳能效
    private static readonly Guid BestPerformance = Guid.Parse("ded574b5-45a0-4f42-8737-46345c09c238"); // 最佳性能

    // 节流调度器：2 秒内多次请求只执行最后一次，避免频繁切换导致系统抖动
    private readonly ThrottleLastDispatcher _dispatcher = new(TimeSpan.FromSeconds(2), nameof(WindowsPowerModeController));

    /// <summary>
    /// 按用户映射把 Legion 电源模式同步到 Windows 电源模式。
    /// </summary>
    /// <param name="powerModeState">要激活的 Legion 电源模式。</param>
    public async Task SetPowerModeAsync(PowerModeState powerModeState)
    {
        // 仅在用户选择"映射到 Windows 电源模式"时才处理
        if (settings.Store.PowerModeMappingMode is not PowerModeMappingMode.WindowsPowerMode)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Ignoring... [powerModeMappingMode={settings.Store.PowerModeMappingMode}]");

            return;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Activating... [powerModeState={powerModeState}]");

        var powerMode = settings.Store.PowerModes.GetValueOrDefault(powerModeState, WindowsPowerMode.Balanced);
        var powerModeGuid = GuidForWindowsPowerMode(powerMode);

        // 节电模式开启时系统会强制覆盖电源模式，此时写入无效，直接跳过
        if (Power.IsBatterySaverEnabled())
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Battery saver is on - will not set overlay scheme.");

            return;
        }

        await _dispatcher.DispatchAsync(() =>
        {
            // 覆盖方案只在"平衡"基础方案上生效，先确保基础方案为默认计划
            ActivateDefaultPowerPlanIfNeeded();

            mainThreadDispatcher.Dispatch(() =>
            {
                // overlay scheme 的 API 对调用线程有要求，需切到主线程执行
                var result = PowerSetActiveOverlayScheme(powerModeGuid);

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Overlay scheme set. [result={result}]");
            });

            try
            {
                UpdateRegistry(powerModeGuid);
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to update registry.", ex);
            }

            return Task.CompletedTask;
        }).ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Power mode {powerMode} activated... [powerModeState={powerModeState}, powerModeGuid={powerModeGuid}]");
    }

    // 同时写入 AC/DC 两个注册表值，保证插电与电池状态下显示一致
    private static void UpdateRegistry(Guid guid)
    {
        Registry.SetValue(POWER_SCHEMES_HIVE, POWER_SCHEMES_SUBKEY, ACTIVE_OVERLAY_AC_POWER_SCHEME_KEY, guid, true);
        Registry.SetValue(POWER_SCHEMES_HIVE, POWER_SCHEMES_SUBKEY, ACTIVE_OVERLAY_DC_POWER_SCHEME_KEY, guid, true);
    }

    // "平衡"没有独立 GUID，返回 Guid.Empty 表示清除覆盖，回落到基础方案
    private static Guid GuidForWindowsPowerMode(WindowsPowerMode windowsPowerMode) => windowsPowerMode switch
    {
        WindowsPowerMode.BestPowerEfficiency => BestPowerEfficiency,
        WindowsPowerMode.BestPerformance => BestPerformance,
        _ => Guid.Empty
    };

    // 若当前激活的不是默认（平衡）基础方案，先切回默认方案
    private static unsafe void ActivateDefaultPowerPlanIfNeeded()
    {
        if (PInvoke.PowerGetActiveScheme(null, out var guid) != WIN32_ERROR.ERROR_SUCCESS)
            PInvokeExtensions.ThrowIfWin32Error("PowerGetActiveScheme");

        if (DefaultPowerPlan == *guid)
            return;

        if (PInvoke.PowerSetActiveScheme(null, DefaultPowerPlan) != WIN32_ERROR.ERROR_SUCCESS)
            PInvokeExtensions.ThrowIfWin32Error("PowerSetActiveScheme");

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Activated default power plan.");
    }

    // powrprof.dll 导出的电源模式覆盖接口（需要 Windows 11）
    [LibraryImport("powrprof.dll", EntryPoint = "PowerSetActiveOverlayScheme")]
    private static partial uint PowerSetActiveOverlayScheme(Guid guid);
}

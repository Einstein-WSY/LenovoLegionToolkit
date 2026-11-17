#if !DEBUG
using LenovoLegionToolkit.Lib.System;
#endif
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Features.Hybrid;
using LenovoLegionToolkit.Lib.Features.Hybrid.Notify;
using LenovoLegionToolkit.Lib.Features.PanelLogo;
using LenovoLegionToolkit.Lib.Features.WhiteKeyboardBacklight;
using LenovoLegionToolkit.Lib.Integrations;
using LenovoLegionToolkit.Lib.Listeners;
// 【已注释】宏功能已禁用：宏控制器仅在下方注释掉的 InitMacroController 中使用
//using LenovoLegionToolkit.Lib.Macro;
using LenovoLegionToolkit.Lib.Services;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.CLI;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Pages;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;
using LenovoLegionToolkit.WPF.Windows;
using LenovoLegionToolkit.WPF.Windows.Utils;
using Application = System.Windows.Application;
using MessageBox = System.Windows.MessageBox;
using WinFormsApp = System.Windows.Forms.Application;
using WinFormsHighDpiMode = System.Windows.Forms.HighDpiMode;

namespace LenovoLegionToolkit.WPF;

/// <summary>
/// 应用入口，负责单实例控制、兼容性校验、依赖注入初始化、
/// 各功能控制器启动以及全局未处理异常的处理。
/// </summary>
public partial class App
{
    // 单实例互斥量名称（GUID 确保唯一）
    private const string MUTEX_NAME = "LenovoLegionToolkit_Mutex_6efcc882-924c-4cbc-8fec-f45c25696f98";
    // 用于唤醒已运行实例的命名事件
    private const string EVENT_NAME = "LenovoLegionToolkit_Event_6efcc882-924c-4cbc-8fec-f45c25696f98";

    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _singleInstanceWaitHandle;

    /// <summary>获取当前 <see cref="App"/> 实例的强类型引用。</summary>
    public new static App Current => (App)Application.Current;

    /// <summary>
    /// 处理应用启动：解析参数、检查单实例与兼容性、初始化容器与服务，并显示主窗口。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">启动参数。</param>
    private async void Application_Startup(object sender, StartupEventArgs e)
    {
#if DEBUG
        // 调试模式下关闭同名的其他实例，避免互斥量冲突
        if (Debugger.IsAttached)
        {
            Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName)
                .Where(p => p.Id != Environment.ProcessId)
                .ForEach(p =>
                {
                    p.Kill();
                    p.WaitForExit();
                });
        }
#endif

        var flags = new Flags(e.Args);

        Log.Instance.IsTraceEnabled = flags.IsTraceEnabled;

        // 订阅 AppDomain 级未处理异常，防止直接崩溃
        AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Flags: {flags}");

        EnsureSingleInstance();

        // 初始化本地化（读取语言设置并加载对应资源）
        await LocalizationHelper.SetLanguageAsync(true);

        if (!flags.SkipCompatibilityCheck)
        {
            try
            {
                if (!await CheckBasicCompatibilityAsync())
                    return;
                if (!await CheckCompatibilityAsync())
                    return;
            }
            catch (Exception ex)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Failed to check device compatibility", ex);

                MessageBox.Show(Resource.CompatibilityCheckError_Message, Resource.AppName, MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(200);
                return;
            }
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Starting... [version={Assembly.GetEntryAssembly()?.GetName().Version}, build={Assembly.GetEntryAssembly()?.GetBuildDateTimeString()}, os={Environment.OSVersion}, dotnet={Environment.Version}]");

        // 使用 PerMonitorV2 DPI 模式，并强制软件渲染以规避部分显卡驱动渲染问题
        WinFormsApp.SetHighDpiMode(WinFormsHighDpiMode.PerMonitorV2);
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;

        // 依次注册 Lib、自动化、宏与 WPF 层的依赖注入模块
        IoCContainer.Initialize(
            new Lib.IoCModule(),
            new Lib.Automation.IoCModule(),
            new Lib.Macro.IoCModule(),
            new IoCModule()
        );

        // 应用代理与各类功能开关标志
        IoCContainer.Resolve<HttpClientFactory>().SetProxy(flags.ProxyUrl, flags.ProxyUsername, flags.ProxyPassword, flags.ProxyAllowAllCerts);

        // 将各项启动标志应用到对应的功能控制器
        IoCContainer.Resolve<PowerModeFeature>().AllowAllPowerModesOnBattery = flags.AllowAllPowerModesOnBattery;
        IoCContainer.Resolve<RGBKeyboardBacklightController>().ForceDisable = flags.ForceDisableRgbKeyboardSupport;
        IoCContainer.Resolve<SpectrumKeyboardBacklightController>().ForceDisable = flags.ForceDisableSpectrumKeyboardSupport;
        IoCContainer.Resolve<WhiteKeyboardLenovoLightingBacklightFeature>().ForceDisable = flags.ForceDisableLenovoLighting;
        IoCContainer.Resolve<PanelLogoLenovoLightingBacklightFeature>().ForceDisable = flags.ForceDisableLenovoLighting;
        IoCContainer.Resolve<PortsBacklightFeature>().ForceDisable = flags.ForceDisableLenovoLighting;
        IoCContainer.Resolve<IGPUModeFeature>().ExperimentalGPUWorkingMode = flags.ExperimentalGPUWorkingMode;
        IoCContainer.Resolve<DGPUNotify>().ExperimentalGPUWorkingMode = flags.ExperimentalGPUWorkingMode;
        //IoCContainer.Resolve<UpdateChecker>().Disable = flags.DisableUpdateChecker; // 【已注释】更新模块已停用

        AutomationPage.EnableHybridModeAutomation = flags.EnableHybridModeAutomation;

        // 【性能优化】以下硬件功能彼此独立，且每项都包含多次 WMI/HID/NVAPI 往返与固定等待，
        // 串行 await 会把启动耗时线性累加；改为并行执行以重叠这些 I/O 等待。
        // 仍需保持的顺序约束：
        //  - GPU 超频必须早于混合模式（后者可能弹出/断电独显，届时无法再写超频）
        //  - RGB 与 Spectrum 键盘控制器共用灯效控制权，二者之间保持串行
        //  - 自动化处理器必须最后执行（其步骤依赖上述功能已就绪）
        await Task.WhenAll(
            LogSoftwareStatusAsync(),
            InitPowerModeFeatureAsync(),
            InitBatteryFeatureAsync(),
            InitGpuOverclockControllerAsync(),
            InitKeyboardBacklightAsync());

        // 依赖顺序：独显可能在此被断电，必须位于超频应用之后
        await InitHybridModeAsync();
        // 自动化规则需在各硬件功能初始化完成后执行
        await InitAutomationProcessorAsync();
        // 【已注释】宏功能已禁用：不再启动宏控制器（不安装全局键盘钩子，宏录制/回放完全失效）
        // InitMacroController();

        // 启动后台服务（AI、HWiNFO 集成、IPC、电池放电监控）
        await IoCContainer.Resolve<AIController>().StartIfNeededAsync();
        await IoCContainer.Resolve<HWiNFOIntegration>().StartStopIfNeededAsync();
        await IoCContainer.Resolve<IpcServer>().StartStopIfNeededAsync();
        await IoCContainer.Resolve<BatteryDischargeRateMonitorService>().StartStopIfNeededAsync();

#if !DEBUG
        Autorun.Validate();
#endif

        var mainWindow = new MainWindow
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            TrayTooltipEnabled = !flags.DisableTrayTooltip,
            DisableConflictingSoftwareWarning = flags.DisableConflictingSoftwareWarning
        };
        MainWindow = mainWindow;

        // 应用主题（需在主窗口创建后执行）
        IoCContainer.Resolve<ThemeManager>().Apply();

        if (flags.Minimized)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Sending MainWindow to tray...");

            // 以最小化状态启动后直接隐藏到托盘
            mainWindow.WindowState = WindowState.Minimized;
            mainWindow.Show();
            mainWindow.SendToTray();
        }
        else
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Showing MainWindow...");

            mainWindow.Show();
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Start up complete");
    }

    /// <summary>应用退出时释放单实例互斥量。</summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">退出事件参数。</param>
    private void Application_Exit(object sender, ExitEventArgs e)
    {
        _singleInstanceMutex?.Close();
    }

    /// <summary>
    /// 关闭当前主窗口并重新创建一个，用于切换语言/主题等需要重建界面的场景。
    /// </summary>
    public void RestartMainWindow()
    {
        if (MainWindow is MainWindow mw)
        {
            // 跳过关闭事件处理，避免触发“退出应用”逻辑
            mw.SuppressClosingEventHandler = true;
            mw.Close();
        }

        var mainWindow = new MainWindow
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    /// <summary>
    /// 关闭后台服务并退出应用。各服务停止均独立 try/catch，单个失败不影响整体关闭。
    /// </summary>
    public async Task ShutdownAsync()
    {
        try
        {
            if (IoCContainer.TryResolve<AIController>() is { } aiController)
                await aiController.StopAsync();
        }
        catch {  /* Ignored. */ }

        try
        {
            if (IoCContainer.TryResolve<RGBKeyboardBacklightController>() is { } rgbKeyboardBacklightController)
            {
                if (await rgbKeyboardBacklightController.IsSupportedAsync())
                    await rgbKeyboardBacklightController.SetLightControlOwnerAsync(false);
            }
        }
        catch {  /* Ignored. */ }

        try
        {
            if (IoCContainer.TryResolve<SpectrumKeyboardBacklightController>() is { } spectrumKeyboardBacklightController)
            {
                if (await spectrumKeyboardBacklightController.IsSupportedAsync())
                    await spectrumKeyboardBacklightController.StopAuroraIfNeededAsync();
            }
        }
        catch {  /* Ignored. */ }

        try
        {
            if (IoCContainer.TryResolve<NativeWindowsMessageListener>() is { } nativeMessageWindowListener)
            {
                await nativeMessageWindowListener.StopAsync();
            }
        }
        catch {  /* Ignored. */ }

        try
        {
            if (IoCContainer.TryResolve<SessionLockUnlockListener>() is { } sessionLockUnlockListener)
            {
                await sessionLockUnlockListener.StopAsync();
            }
        }
        catch { /* Ignored. */ }

        try
        {
            if (IoCContainer.TryResolve<HWiNFOIntegration>() is { } hwinfoIntegration)
            {
                await hwinfoIntegration.StopAsync();
            }
        }
        catch { /* Ignored. */ }

        try
        {
            if (IoCContainer.TryResolve<IpcServer>() is { } ipcServer)
            {
                await ipcServer.StopAsync();
            }
        }
        catch { /* Ignored. */ }

        try
        {
            if (IoCContainer.TryResolve<BatteryDischargeRateMonitorService>() is { } batteryDischargeMon)
            {
                await batteryDischargeMon.StopAsync();
            }
        }
        catch { /* Ignored. */ }

        Shutdown();
    }

    // AppDomain 级别未处理异常：记录日志、弹窗提示后以退出码 100 结束进程
    private void AppDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;

        Log.Instance.ErrorReport("AppDomain_UnhandledException", exception ?? new Exception($"Unknown exception caught: {e.ExceptionObject}"));
        Log.Instance.Trace($"Unhandled exception occurred.", exception);

        MessageBox.Show(string.Format(Resource.UnexpectedException, exception?.ToStringDemystified() ?? "Unknown exception."),
            "Application Domain Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Shutdown(100);
    }

    // UI 线程（Dispatcher）未处理异常：记录日志、弹窗提示后以退出码 101 结束进程
    private void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Instance.ErrorReport("Application_DispatcherUnhandledException", e.Exception);
        Log.Instance.Trace($"Unhandled exception occurred.", e.Exception);

        MessageBox.Show(string.Format(Resource.UnexpectedException, e.Exception.ToStringDemystified()),
            "Application Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Shutdown(101);
    }

    // 基础兼容性检查：不通过则提示并退出
    private async Task<bool> CheckBasicCompatibilityAsync()
    {
        var isCompatible = await Compatibility.CheckBasicCompatibilityAsync();
        if (isCompatible)
            return true;

        MessageBox.Show(Resource.IncompatibleDevice_Message, Resource.AppName, MessageBoxButton.OK, MessageBoxImage.Error);

        Shutdown(201);
        return false;
    }

    // 完整兼容性检查：不兼容时显示提示窗口，由用户选择是否继续
    private async Task<bool> CheckCompatibilityAsync()
    {
        var (isCompatible, mi) = await Compatibility.IsCompatibleAsync();
        if (isCompatible)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Compatibility check passed. [Vendor={mi.Vendor}, Model={mi.Model}, MachineType={mi.MachineType}, BIOS={mi.BiosVersion}]");
            return true;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Incompatible system detected. [Vendor={mi.Vendor}, Model={mi.Model}, MachineType={mi.MachineType}, BIOS={mi.BiosVersion}]");

        var unsupportedWindow = new UnsupportedWindow(mi);
        unsupportedWindow.Show();

        var result = await unsupportedWindow.ShouldContinue;
        if (result)
        {
            Log.Instance.IsTraceEnabled = true;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Compatibility check OVERRIDE. [Vendor={mi.Vendor}, Model={mi.Model}, MachineType={mi.MachineType}, version={Assembly.GetEntryAssembly()?.GetName().Version}, build={Assembly.GetEntryAssembly()?.GetBuildDateTimeString() ?? string.Empty}]");
            return true;
        }

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Shutting down... [Vendor={mi.Vendor}, Model={mi.Model}, MachineType={mi.MachineType}]");

        Shutdown(202);
        return false;
    }

    // 通过命名互斥量保证单实例运行；后启动的实例会唤醒已有实例并退出
    private void EnsureSingleInstance()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Checking for other instances...");

        _singleInstanceMutex = new Mutex(true, MUTEX_NAME, out var isOwned);
        _singleInstanceWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, EVENT_NAME);

        if (!isOwned)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Another instance running, closing...");

            _singleInstanceWaitHandle.Set();
            Shutdown();
            return;
        }

        // 后台线程等待“再次启动”事件，收到后切回 UI 线程把主窗口置前
        new Thread(() =>
        {
            while (_singleInstanceWaitHandle.WaitOne())
            {
                Current.Dispatcher.BeginInvoke(async () =>
                {
                    if (Current.MainWindow is { } window)
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Another instance started, bringing this one to front instead...");

                        window.BringToForeground();
                    }
                    else
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"!!! PANIC !!! This instance is missing main window. Shutting down.");

                        await ShutdownAsync();
                    }
                });
            }
        })
        {
            IsBackground = true
        }.Start();
    }

    // 以 Trace 级别记录 Vantage / LegionZone / FnKeys 等冲突软件的运行状态
    private static async Task LogSoftwareStatusAsync()
    {
        if (!Log.Instance.IsTraceEnabled)
            return;

        var vantageStatus = await IoCContainer.Resolve<VantageDisabler>().GetStatusAsync();
        Log.Instance.Trace($"Vantage status: {vantageStatus}");

        var legionZoneStatus = await IoCContainer.Resolve<LegionZoneDisabler>().GetStatusAsync();
        Log.Instance.Trace($"LegionZone status: {legionZoneStatus}");

        var fnKeysStatus = await IoCContainer.Resolve<FnKeysDisabler>().GetStatusAsync();
        Log.Instance.Trace($"FnKeys status: {fnKeysStatus}");
    }

    // 初始化混合模式：必要时弹出独立显卡
    private static async Task InitHybridModeAsync()
    {
        try
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Initializing hybrid mode...");

            var feature = IoCContainer.Resolve<HybridModeFeature>();
            await feature.EnsureDGPUEjectedIfNeededAsync();
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't initialize hybrid mode.", ex);
        }
    }

    // 初始化自动化处理器并执行开机自动化动作
    private static async Task InitAutomationProcessorAsync()
    {
        try
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Initializing automation processor...");

            var automationProcessor = IoCContainer.Resolve<AutomationProcessor>();
            await automationProcessor.InitializeAsync();
            automationProcessor.RunOnStartup();
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't initialize automation processor.", ex);
        }
    }

    // 确保电源模式的 God Mode 状态与 Windows 电源计划正确
    private static async Task InitPowerModeFeatureAsync()
    {
        try
        {
            var feature = IoCContainer.Resolve<PowerModeFeature>();
            if (await feature.IsSupportedAsync())
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Ensuring god mode state is applied...");

                await feature.EnsureGodModeStateIsAppliedAsync();
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't ensure god mode state.", ex);
        }

        try
        {
            var feature = IoCContainer.Resolve<PowerModeFeature>();
            if (await feature.IsSupportedAsync())
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Ensuring correct power plan is set...");

                await feature.EnsureCorrectWindowsPowerSettingsAreSetAsync();
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't ensure correct power plan.", ex);
        }
    }

    // 确保电池模式设置正确
    private static async Task InitBatteryFeatureAsync()
    {
        try
        {
            var feature = IoCContainer.Resolve<BatteryFeature>();
            if (await feature.IsSupportedAsync())
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Ensuring correct battery mode is set...");

                await feature.EnsureCorrectBatteryModeIsSetAsync();
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't ensure correct battery mode.", ex);
        }
    }

    // 【性能优化】把两个键盘背光控制器组合为一次串行等待，对外表现为一个可并行的初始化任务。
    // 二者都通过 WMI 争夺"灯光控制权"，并行会互相抢占，故内部保持先后顺序。
    private static async Task InitKeyboardBacklightAsync()
    {
        await InitRgbKeyboardControllerAsync();
        await InitSpectrumKeyboardControllerAsync();
    }

    // 获取 RGB 键盘灯效控制权并恢复上次预设
    private static async Task InitRgbKeyboardControllerAsync()
    {
        try
        {
            var controller = IoCContainer.Resolve<RGBKeyboardBacklightController>();
            if (await controller.IsSupportedAsync())
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Setting light control owner and restoring preset...");

                await controller.SetLightControlOwnerAsync(true, true);
            }
            else
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"RGB keyboard is not supported.");
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't set light control owner or current preset.", ex);
        }
    }

    // 必要时启动 Spectrum 键盘的 Aurora 服务
    private static async Task InitSpectrumKeyboardControllerAsync()
    {
        try
        {
            var controller = IoCContainer.Resolve<SpectrumKeyboardBacklightController>();
            if (await controller.IsSupportedAsync())
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Starting Aurora if needed...");

                var result = await controller.StartAuroraIfNeededAsync();
                if (result)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Aurora started.");
                }
                else
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Aurora not needed.");
                }
            }
            else
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Spectrum keyboard is not supported.");
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't start Aurora if needed.", ex);
        }
    }

    // 确保已保存的 GPU 超频设置被重新应用
    private static async Task InitGpuOverclockControllerAsync()
    {
        try
        {
            var controller = IoCContainer.Resolve<GPUOverclockController>();
            if (await controller.IsSupportedAsync())
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Ensuring GPU overclock is applied...");

                var result = await controller.EnsureOverclockIsAppliedAsync();
                if (result)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"GPU overclock applied.");
                }
                else
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"GPU overclock not needed.");
                }
            }
            else
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"GPU overclock is not supported.");
            }
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't overclock GPU.", ex);
        }
    }

    // 【已注释】宏功能已禁用：整个启动方法停用（调用处一并注释）
    //private static void InitMacroController()
    //{
    //    var controller = IoCContainer.Resolve<MacroController>();
    //    controller.Start();
    //}
}

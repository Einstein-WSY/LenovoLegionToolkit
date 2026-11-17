using Autofac;
using LenovoLegionToolkit.Lib.AutoListeners;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Controllers.GodMode;
using LenovoLegionToolkit.Lib.Controllers.Sensors;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Features.FlipToStart;
using LenovoLegionToolkit.Lib.Features.Hybrid;
using LenovoLegionToolkit.Lib.Features.Hybrid.Notify;
using LenovoLegionToolkit.Lib.Features.InstantBoot;
using LenovoLegionToolkit.Lib.Features.OverDrive;
using LenovoLegionToolkit.Lib.Features.PanelLogo;
using LenovoLegionToolkit.Lib.Features.WhiteKeyboardBacklight;
using LenovoLegionToolkit.Lib.Integrations;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.PackageDownloader;
using LenovoLegionToolkit.Lib.Services;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib;

/// <summary>
/// 主 Autofac 注册模块：集中注册应用所有服务、功能（Feature）、监听器（Listener）、控制器（Controller）等组件。
/// 大多数组件注册为单例；<c>builder.Register&lt;T&gt;(true)</c> 形式表示"仅按需自动激活"（<see cref="LenovoLegionToolkit.Lib.Extensions.IoCExtensions"/> 扩展），
/// 常用于依赖特定硬件能力、不适合启动即实例化的实现。
/// </summary>
public class IoCModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register<HttpClientFactory>(); // HTTP 客户端工厂（代理支持）

        // 软件屏蔽器：检测并抑制 Lenovo 官方软件（FnKeys/LegionZone/Vantage）与本项目冲突
        builder.Register<FnKeysDisabler>();
        builder.Register<LegionZoneDisabler>();
        builder.Register<VantageDisabler>();

        // 各类设置存储（单例，负责持久化加载/保存）
        builder.Register<ApplicationSettings>();
        builder.Register<BalanceModeSettings>();
        builder.Register<GodModeSettings>();
        builder.Register<GPUOverclockSettings>();
        builder.Register<IntegrationsSettings>();
        builder.Register<PackageDownloaderSettings>();
        builder.Register<RGBKeyboardSettings>();
        builder.Register<SpectrumKeyboardSettings>();
        builder.Register<SunriseSunsetSettings>();

        // 硬件功能封装：每个 Feature 对应一项可开关/切换的设备能力
        builder.Register<AlwaysOnUSBFeature>();
        builder.Register<BatteryFeature>();
        builder.Register<BatteryNightChargeFeature>();
        builder.Register<DpiScaleFeature>();
        builder.Register<FlipToStartFeature>();
        builder.Register<FlipToStartCapabilityFeature>(true);
        builder.Register<FlipToStartUEFIFeature>(true);
        builder.Register<FnLockFeature>();
        builder.Register<GSyncFeature>();
        builder.Register<HDRFeature>();
        builder.Register<HybridModeFeature>();
        builder.Register<IGPUModeFeature>();
        builder.Register<IGPUModeCapabilityFeature>(true);
        builder.Register<IGPUModeFeatureFlagsFeature>(true);
        builder.Register<IGPUModeGamezoneFeature>(true);
        builder.Register<InstantBootFeature>();
        builder.Register<InstantBootFeatureFlagsFeature>(true);
        builder.Register<InstantBootCapabilityFeature>(true);
        builder.Register<MicrophoneFeature>();
        builder.Register<OneLevelWhiteKeyboardBacklightFeature>();
        builder.Register<OverDriveFeature>();
        builder.Register<OverDriveGameZoneFeature>(true);
        builder.Register<OverDriveCapabilityFeature>(true);
        builder.Register<PanelLogoBacklightFeature>();
        builder.Register<PanelLogoSpectrumBacklightFeature>(true);
        builder.Register<PanelLogoLenovoLightingBacklightFeature>(true);
        builder.Register<PortsBacklightFeature>();
        builder.Register<PowerModeFeature>();
        builder.Register<RefreshRateFeature>();
        builder.Register<ResolutionFeature>();
        builder.Register<SpeakerFeature>();
        builder.Register<TouchpadLockFeature>();
        builder.Register<WhiteKeyboardBacklightFeature>();
        builder.Register<WhiteKeyboardDriverBacklightFeature>(true);
        builder.Register<WhiteKeyboardLenovoLightingBacklightFeature>(true);
        builder.Register<WinKeyFeature>();

        // dGPU（独立显卡）相关通知组件
        builder.Register<DGPUNotify>();
        builder.Register<DGPUCapabilityNotify>(true);
        builder.Register<DGPUFeatureFlagsNotify>(true);
        builder.Register<DGPUGamezoneNotify>(true);

        // 系统级监听器：AutoActivateListener 表示注册后立即自动启动监听
        builder.Register<DisplayBrightnessListener>().AutoActivateListener();
        builder.Register<DisplayConfigurationListener>().AutoActivateListener();
        builder.Register<DriverKeyListener>().AutoActivateListener();
        builder.Register<LightingChangeListener>().AutoActivateListener();
        builder.Register<NativeWindowsMessageListener>().AutoActivateListener();
        builder.Register<PowerModeListener>().AutoActivateListener();
        builder.Register<PowerStateListener>().AutoActivateListener();
        builder.Register<RGBKeyboardBacklightListener>().AutoActivateListener();
        builder.Register<SessionLockUnlockListener>().AutoActivateListener();
        builder.Register<SpecialKeyListener>().AutoActivateListener();
        builder.Register<SystemThemeListener>().AutoActivateListener();
        builder.Register<ThermalModeListener>().AutoActivateListener();
        builder.Register<WinKeyListener>().AutoActivateListener();

        // 自动化监听器：用于"自动化规则"中检测触发条件（游戏运行、进程、时间等）
        builder.Register<GameAutoListener>();
        builder.Register<InstanceStartedEventAutoAutoListener>();
        builder.Register<InstanceStoppedEventAutoAutoListener>();
        builder.Register<ProcessAutoListener>();
        builder.Register<TimeAutoListener>();
        builder.Register<UserInactivityAutoListener>();
        builder.Register<WiFiAutoListener>();

        // 控制器：负责组合底层 Feature 完成较复杂的设备状态控制
        builder.Register<AIController>();
        builder.Register<DisplayBrightnessController>();
        builder.Register<GodModeController>();
        builder.Register<GodModeControllerV1>(true);
        builder.Register<GodModeControllerV2>(true);
        builder.Register<GPUController>();
        builder.Register<GPUOverclockController>();
        builder.Register<RGBKeyboardBacklightController>();
        builder.Register<SensorsController>();
        builder.Register<SensorsControllerV1>(true);
        builder.Register<SensorsControllerV2>(true);
        builder.Register<SensorsControllerV3>(true);
        builder.Register<SmartFnLockController>();
        builder.Register<SpectrumKeyboardBacklightController>();
        builder.Register<WindowsPowerModeController>();
        builder.Register<WindowsPowerPlanController>();

        // 驱动/BIOS 包下载器（PCSupport 与 Vantage 两种来源）
        builder.Register<PackageDownloaderFactory>();
        builder.Register<PCSupportPackageDownloader>();
        builder.Register<VantagePackageDownloader>();

        // 第三方集成与辅助服务
        builder.Register<HWiNFOIntegration>();

        builder.Register<SunriseSunset>();

        builder.Register<BatteryDischargeRateMonitorService>();
    }
}

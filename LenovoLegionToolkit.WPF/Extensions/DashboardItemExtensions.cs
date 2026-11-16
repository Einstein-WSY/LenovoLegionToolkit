using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LenovoLegionToolkit.WPF.Controls;
using LenovoLegionToolkit.WPF.Controls.Dashboard;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Extensions;

/// <summary>
/// 提供仪表盘项目在界面展示所需元数据的扩展方法。
/// </summary>
public static class DashboardItemExtensions
{
    /// <summary>
    /// 获取仪表盘项目对应的图标。
    /// </summary>
    /// <param name="dashboardItem">仪表盘项目。</param>
    /// <returns>该项目对应的 <see cref="SymbolRegular"/> 图标。</returns>
    public static SymbolRegular GetIcon(this DashboardItem dashboardItem) => dashboardItem switch
    {
        DashboardItem.PowerMode => SymbolRegular.Gauge24,
        DashboardItem.BatteryMode => SymbolRegular.BatteryCharge24,
        DashboardItem.BatteryNightChargeMode => SymbolRegular.WeatherMoon24,
        DashboardItem.AlwaysOnUsb => SymbolRegular.UsbStick24,
        DashboardItem.InstantBoot => SymbolRegular.PlugDisconnected24,
        DashboardItem.HybridMode => SymbolRegular.LeafOne24,
        DashboardItem.DiscreteGpu => SymbolRegular.DeveloperBoard24,
        DashboardItem.OverclockDiscreteGpu => SymbolRegular.DeveloperBoardLightning20,
        DashboardItem.Resolution => SymbolRegular.ScaleFill24,
        DashboardItem.RefreshRate => SymbolRegular.DesktopPulse24,
        DashboardItem.DpiScale => SymbolRegular.TextFontSize24,
        DashboardItem.Hdr => SymbolRegular.Hdr24,
        DashboardItem.OverDrive => SymbolRegular.TopSpeed24,
        DashboardItem.PanelLogoBacklight => SymbolRegular.LightbulbCircle24,
        DashboardItem.PortsBacklight => SymbolRegular.UsbPlug24,
        DashboardItem.TurnOffMonitors => SymbolRegular.Desktop24,
        DashboardItem.Microphone => SymbolRegular.Mic24,
        DashboardItem.FlipToStart => SymbolRegular.Power24,
        DashboardItem.TouchpadLock => SymbolRegular.Tablet24,
        DashboardItem.FnLock => SymbolRegular.Keyboard24,
        DashboardItem.WinKeyLock => SymbolRegular.Keyboard24,
        DashboardItem.WhiteKeyboardBacklight => SymbolRegular.Keyboard24,
        _ => throw new InvalidOperationException($"Invalid DashboardItem {dashboardItem}"),
    };

    /// <summary>
    /// 获取仪表盘项目的本地化显示标题。
    /// </summary>
    /// <param name="dashboardItem">仪表盘项目。</param>
    /// <returns>来自资源文件的本地化标题文本。</returns>
    public static string GetTitle(this DashboardItem dashboardItem) => dashboardItem switch
    {
        DashboardItem.PowerMode => Resource.PowerModeControl_Title,
        DashboardItem.BatteryMode => Resource.BatteryModeControl_Title,
        DashboardItem.BatteryNightChargeMode => Resource.BatteryNightChargeModeControl_Title,
        DashboardItem.AlwaysOnUsb => Resource.AlwaysOnUSBControl_Title,
        DashboardItem.InstantBoot => Resource.InstantBootControl_Title,
        DashboardItem.HybridMode => $"{Resource.ComboBoxHybridModeControl_Title} / {Resource.ToggleHybridModeControl_Title}",
        DashboardItem.DiscreteGpu => Resource.DiscreteGPUControl_Title,
        DashboardItem.OverclockDiscreteGpu => Resource.OverclockDiscreteGPUControl_Title,
        DashboardItem.Resolution => Resource.ResolutionControl_Title,
        DashboardItem.RefreshRate => Resource.RefreshRateControl_Title,
        DashboardItem.DpiScale => Resource.DpiScaleControl_Title,
        DashboardItem.Hdr => Resource.HDRControl_Title,
        DashboardItem.OverDrive => Resource.OverDriveControl_Title,
        DashboardItem.PanelLogoBacklight => Resource.PanelLogoBacklightControl_Title,
        DashboardItem.PortsBacklight => Resource.PortsBacklightControl_Title,
        DashboardItem.TurnOffMonitors => Resource.TurnOffMonitorsControl_Title,
        DashboardItem.Microphone => Resource.MicrophoneControl_Title,
        DashboardItem.FlipToStart => Resource.FlipToStartControl_Title,
        DashboardItem.TouchpadLock => Resource.TouchpadLockControl_Title,
        DashboardItem.FnLock => Resource.FnLockControl_Title,
        DashboardItem.WinKeyLock => Resource.WinKeyControl_Title,
        DashboardItem.WhiteKeyboardBacklight => Resource.WhiteKeyboardBacklightControl_Title,
        _ => throw new InvalidOperationException($"Invalid DashboardItem {dashboardItem}"),
    };

    /// <summary>
    /// 异步创建仪表盘项目对应的控件实例。
    /// </summary>
    /// <param name="dashboardItem">仪表盘项目。</param>
    /// <returns>承载该项目数据的可刷新控件集合。</returns>
    /// <remarks>部分控件（如混合模式）需要在运行时异步探测硬件能力后再创建。</remarks>
    public static async Task<IEnumerable<AbstractRefreshingControl>> GetControlAsync(this DashboardItem dashboardItem) => dashboardItem switch
    {
        DashboardItem.PowerMode => [new PowerModeControl()],
        DashboardItem.BatteryMode => [new BatteryModeControl()],
        DashboardItem.BatteryNightChargeMode => [new BatteryNightChargeModeControl()],
        DashboardItem.AlwaysOnUsb => [new AlwaysOnUSBControl()],
        DashboardItem.InstantBoot => [new InstantBootControl()],
        DashboardItem.HybridMode => [await HybridModeControlFactory.GetControlAsync()],
        DashboardItem.DiscreteGpu => [new DiscreteGPUControl()],
        DashboardItem.OverclockDiscreteGpu => [new OverclockDiscreteGPUControl()],
        DashboardItem.Resolution => [new ResolutionControl()],
        DashboardItem.RefreshRate => [new RefreshRateControl()],
        DashboardItem.DpiScale => [new DpiScaleControl()],
        DashboardItem.Hdr => [new HDRControl()],
        DashboardItem.OverDrive => [new OverDriveControl()],
        DashboardItem.PanelLogoBacklight => [new PanelLogoBacklightControl()],
        DashboardItem.PortsBacklight => [new PortsBacklightControl()],
        DashboardItem.TurnOffMonitors => [new TurnOffMonitorsControl()],
        DashboardItem.Microphone => [new MicrophoneControl()],
        DashboardItem.FlipToStart => [new FlipToStartControl()],
        DashboardItem.TouchpadLock => [new TouchpadLockControl()],
        DashboardItem.FnLock => [new FnLockControl()],
        DashboardItem.WinKeyLock => [new WinKeyControl()],
        DashboardItem.WhiteKeyboardBacklight => [new WhiteKeyboardBacklightControl(), new OneLevelWhiteKeyboardBacklightControl()],
        _ => throw new InvalidOperationException($"Invalid DashboardItem {dashboardItem}"),
    };
}

using System;
using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Extensions;

/// <summary>
/// 提供自动化管道触发器的界面扩展方法。
/// </summary>
public static class AutomationPipelineTriggerExtensions
{
    /// <summary>
    /// 根据触发器类型获取用于界面展示的图标。
    /// </summary>
    /// <param name="trigger">自动化管道触发器实例。</param>
    /// <returns>与该触发器类型对应的 <see cref="SymbolRegular"/> 图标。</returns>
    public static SymbolRegular Icon(this IAutomationPipelineTrigger trigger) => trigger switch
    {
        IPowerStateAutomationPipelineTrigger => SymbolRegular.BatteryCharge24,
        IPowerModeAutomationPipelineTrigger => SymbolRegular.Gauge24,
        IGodModePresetChangedAutomationPipelineTrigger => SymbolRegular.Gauge24,
        IGameAutomationPipelineTrigger => SymbolRegular.XboxController24,
        IHDRPipelineTrigger => SymbolRegular.Hdr24,
        IProcessesAutomationPipelineTrigger => SymbolRegular.WindowConsole20,
        IUserInactivityPipelineTrigger => SymbolRegular.ClockAlarm24,
        ISessionLockPipelineTrigger => SymbolRegular.LockClosed24,
        ISessionUnlockPipelineTrigger => SymbolRegular.LockOpen24,
        ITimeAutomationPipelineTrigger => SymbolRegular.HourglassHalf24,
        IDeviceAutomationPipelineTrigger => SymbolRegular.UsbPlug24,
        INativeWindowsMessagePipelineTrigger => SymbolRegular.Desktop24,
        IOnStartupAutomationPipelineTrigger => SymbolRegular.Flash24,
        IOnResumeAutomationPipelineTrigger => SymbolRegular.Flash24,
        IWiFiConnectedPipelineTrigger => SymbolRegular.Wifi124,
        IWiFiDisconnectedPipelineTrigger => SymbolRegular.WifiOff24,
        IPeriodicAutomationPipelineTrigger => SymbolRegular.ArrowRepeatAll24,
        _ => throw new ArgumentException($"Unsupported trigger {trigger.GetType().Name}")
    };
}

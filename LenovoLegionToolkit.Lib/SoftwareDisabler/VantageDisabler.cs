using System.Collections.Generic;

namespace LenovoLegionToolkit.Lib.SoftwareDisabler;

/// <summary>
/// Lenovo Vantage 禁用器：禁用 Vantage、ImController、UDC、电池计量等计划任务，
/// 停止 ImControllerService/LenovoVantageService 服务并结束对应进程，避免与本工具争抢硬件控制。
/// </summary>
public class VantageDisabler : AbstractSoftwareDisabler
{
    // Vantage 的功能大量由计划任务驱动（自更新、定时事件、插件等），需逐一遍历禁用
    protected override IEnumerable<string> ScheduledTasksPaths =>
    [
        "Lenovo\\BatteryGauge",
        "Lenovo\\ImController",
        "Lenovo\\ImController\\Plugins",
        "Lenovo\\ImController\\TimeBasedEvents",
        "Lenovo\\UDC",
        "Lenovo\\Vantage",
        "Lenovo\\Vantage\\Schedule"
    ];

    protected override IEnumerable<string> ServiceNames =>
    [
        "ImControllerService",
        "LenovoVantageService"
    ];

    protected override IEnumerable<string> ProcessNames =>
    [
        "LenovoVantage",
        "Lenovo.Modern.ImController"
    ];
}

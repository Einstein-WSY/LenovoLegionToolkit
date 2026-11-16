using System.Collections.Generic;

namespace LenovoLegionToolkit.Lib.SoftwareDisabler;

/// <summary>
/// Legion Zone 禁用器：停止 LZService 服务并结束 LegionZone/LZTray 进程，
/// 防止官方软件同时控制性能模式与灯效。
/// </summary>
public class LegionZoneDisabler : AbstractSoftwareDisabler
{
    // Legion Zone 使用服务常驻而非计划任务，故计划任务集合为空
    protected override IEnumerable<string> ScheduledTasksPaths => [];
    protected override IEnumerable<string> ServiceNames => ["LZService"];
    protected override IEnumerable<string> ProcessNames => ["LegionZone", "LZTray"];
}

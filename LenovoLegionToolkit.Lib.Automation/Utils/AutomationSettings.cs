using System.Collections.Generic;
using LenovoLegionToolkit.Lib.Automation.Pipeline;
using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;
using LenovoLegionToolkit.Lib.Automation.Resources;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.Lib.Settings;

namespace LenovoLegionToolkit.Lib.Automation.Utils;

/// <summary>
/// 自动化功能设置，持久化到 <c>automation.json</c>。
/// </summary>
public class AutomationSettings() : AbstractSettings<AutomationSettings.AutomationSettingsStore>("automation.json")
{
    /// <summary>自动化设置的持久化数据结构。</summary>
    public class AutomationSettingsStore
    {
        /// <summary>是否启用自动化（关闭后所有触发器和流水线都不执行）。</summary>
        public bool IsEnabled { get; set; }

        /// <summary>全部自动化流水线。</summary>
        public List<AutomationPipeline> Pipelines { get; set; } = [];
    }

    /// <summary>
    /// 首次运行时的默认设置：接入电源切到均衡模式、断开电源切到安静模式，
    /// 并预置一条“结束占用独显的进程”的快捷操作。
    /// </summary>
    protected override AutomationSettingsStore Default => new()
    {
        Pipelines =
        {
            // 接入电源适配器时：切换到均衡模式
            new AutomationPipeline
            {
                Trigger = new ACAdapterConnectedAutomationPipelineTrigger(),
                Steps = { new PowerModeAutomationStep(PowerModeState.Balance) },
            },
            // 断开电源适配器时：切换到安静模式以延长续航
            new AutomationPipeline
            {
                Trigger = new ACAdapterDisconnectedAutomationPipelineTrigger(),
                Steps = { new PowerModeAutomationStep(PowerModeState.Quiet) },
            },
            // 无触发器的流水线，作为快捷操作供用户手动执行
            new AutomationPipeline
            {
                Name = Resource.DeactivateGpuQuickAction_Title,
                Steps = { new DeactivateGPUAutomationStep(DeactivateGPUAutomationStepState.KillApps) },
            },
        },
    };
}

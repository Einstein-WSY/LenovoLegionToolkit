using System.ComponentModel.DataAnnotations;
using LenovoLegionToolkit.Lib.Automation.Resources;

namespace LenovoLegionToolkit.Lib.Automation;

/// <summary>
/// 停用独立显卡步骤可选的执行方式。
/// </summary>
public enum DeactivateGPUAutomationStepState
{
    /// <summary>先结束占用显卡的应用进程，再执行停用。</summary>
    [Display(ResourceType = typeof(Resource), Name = "DeactivateGPUAutomationStepState_KillApps")]
    KillApps,
    /// <summary>重启显卡设备以释放占用后停用。</summary>
    [Display(ResourceType = typeof(Resource), Name = "DeactivateGPUAutomationStepState_RestartGPU")]
    RestartGPU,
}

/// <summary>
/// 宏功能的开关状态，用于宏自动化步骤。
/// </summary>
public enum MacroAutomationStepState
{
    /// <summary>关闭宏。</summary>
    [Display(ResourceType = typeof(Resource), Name = "MacroAutomationStepState_Off")]
    Off,
    /// <summary>开启宏。</summary>
    [Display(ResourceType = typeof(Resource), Name = "MacroAutomationStepState_On")]
    On
}

/// <summary>
/// 独立显卡超频的开关状态，用于超频自动化步骤。
/// </summary>
public enum OverclockDiscreteGPUAutomationStepState
{
    /// <summary>关闭超频。</summary>
    [Display(ResourceType = typeof(Resource), Name = "OverclockDiscreteGPUAutomationStepState_Off")]
    Off,
    /// <summary>开启超频。</summary>
    [Display(ResourceType = typeof(Resource), Name = "OverclockDiscreteGPUAutomationStepState_On")]
    On
}

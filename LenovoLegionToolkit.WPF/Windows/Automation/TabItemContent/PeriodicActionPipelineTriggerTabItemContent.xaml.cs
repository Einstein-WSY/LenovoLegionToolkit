using System;
using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

namespace LenovoLegionToolkit.WPF.Windows.Automation.TabItemContent;

/// <summary>
/// 周期触发器的配置选项卡，以分钟为单位设置触发间隔。
/// </summary>
public partial class PeriodicAutomationPipelineTriggerTabItemContent : IAutomationPipelineTriggerTabItemContent<IPeriodicAutomationPipelineTrigger>
{
    private readonly IPeriodicAutomationPipelineTrigger _trigger;
    private readonly TimeSpan _period;

    /// <summary>
    /// 初始化选项卡内容，并记录触发器的原始周期。
    /// </summary>
    /// <param name="trigger">待编辑的周期触发器。</param>
    public PeriodicAutomationPipelineTriggerTabItemContent(IPeriodicAutomationPipelineTrigger trigger)
    {
        _trigger = trigger;
        _period = trigger.Period;
        InitializeComponent();
    }

    /// <summary>
    /// 将原始周期换算为分钟并回填到数值选择器。
    /// </summary>
    private void PeriodicAutomationPipelineTriggerTabItemContent_Initialized(object? sender, EventArgs e)
    {
        _periodPickerMinutes.Value = _period.TotalMinutes;
    }

    /// <summary>
    /// 读取选择器中的分钟数并生成触发器副本。
    /// </summary>
    /// <returns>更新后的周期触发器。</returns>
    public IPeriodicAutomationPipelineTrigger GetTrigger()
    {
        var periodMinutes = (int)(_periodPickerMinutes.Value ?? 1);
        return _trigger.DeepCopy(TimeSpan.FromMinutes(periodMinutes));
    }
}

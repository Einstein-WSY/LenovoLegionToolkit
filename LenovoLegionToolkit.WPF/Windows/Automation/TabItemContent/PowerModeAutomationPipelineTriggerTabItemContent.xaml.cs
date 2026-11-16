using System;
using System.Linq;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;

namespace LenovoLegionToolkit.WPF.Windows.Automation.TabItemContent;

/// <summary>
/// 电源模式触发器的配置选项卡，列出当前设备支持的所有电源模式供单选。
/// </summary>
public partial class PowerModeAutomationPipelineTriggerTabItemContent : IAutomationPipelineTriggerTabItemContent<IPowerModeAutomationPipelineTrigger>
{
    private readonly PowerModeFeature _feature = IoCContainer.Resolve<PowerModeFeature>();

    private readonly IPowerModeAutomationPipelineTrigger _trigger;
    private readonly PowerModeState _powerModeState;

    /// <summary>
    /// 初始化选项卡内容，并记录触发器当前的电源模式。
    /// </summary>
    /// <param name="trigger">待编辑的电源模式触发器。</param>
    public PowerModeAutomationPipelineTriggerTabItemContent(IPowerModeAutomationPipelineTrigger trigger)
    {
        _trigger = trigger;
        _powerModeState = trigger.PowerModeState;

        InitializeComponent();
    }

    /// <summary>
    /// 读取当前选中的电源模式并生成触发器副本；未选中时回退到均衡模式。
    /// </summary>
    /// <returns>更新后的触发器。</returns>
    public IPowerModeAutomationPipelineTrigger GetTrigger()
    {
        var state = _content.Children
            .OfType<RadioButton>()
            .Where(r => r.IsChecked ?? false)
            .Select(r => (PowerModeState)r.Tag)
            .DefaultIfEmpty(PowerModeState.Balance)
            .FirstOrDefault();
        return _trigger.DeepCopy(state);
    }

    /// <summary>
    /// 异步获取设备支持的全部电源模式，并为每个模式生成单选按钮。
    /// </summary>
    private async void PowerModeAutomationPipelineTriggerTabItemContent_Initialized(object? sender, EventArgs eventArgs)
    {
        var states = await _feature.GetAllStatesAsync();

        foreach (var state in states)
        {
            var radio = new RadioButton
            {
                Content = state.GetDisplayName(),
                Tag = state,
                IsChecked = state == _powerModeState,
                Margin = new(0, 0, 0, 8)
            };
            _content.Children.Add(radio);
        }
    }
}

using System;
using System.Linq;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;
using LenovoLegionToolkit.Lib.Settings;

namespace LenovoLegionToolkit.WPF.Windows.Automation.TabItemContent;

/// <summary>
/// 上帝模式预设变更触发器的配置选项卡，列出所有已保存的预设供单选。
/// </summary>
public partial class GodModePresetPipelineTriggerTabItemContent : IAutomationPipelineTriggerTabItemContent<IGodModePresetChangedAutomationPipelineTrigger>
{
    private readonly GodModeSettings _settings = IoCContainer.Resolve<GodModeSettings>();

    private readonly IGodModePresetChangedAutomationPipelineTrigger _trigger;

    /// <summary>
    /// 初始化选项卡内容。
    /// </summary>
    /// <param name="trigger">待编辑的触发器。</param>
    public GodModePresetPipelineTriggerTabItemContent(IGodModePresetChangedAutomationPipelineTrigger trigger)
    {
        _trigger = trigger;

        InitializeComponent();
    }

    /// <summary>
    /// 读取当前选中的预设 ID 并生成触发器副本；未选中时使用空 GUID。
    /// </summary>
    /// <returns>更新后的触发器。</returns>
    public IGodModePresetChangedAutomationPipelineTrigger GetTrigger()
    {
        var state = _content.Children
            .OfType<RadioButton>()
            .Where(r => r.IsChecked ?? false)
            .Select(r => (Guid)r.Tag)
            .DefaultIfEmpty(Guid.Empty)
            .FirstOrDefault();
        return _trigger.DeepCopy(state);
    }

    /// <summary>
    /// 为每个已保存的预设生成一个单选按钮。
    /// </summary>
    private void GodModePresetPipelineTriggerTabItemContent_Initialized(object? sender, EventArgs e)
    {
        foreach (var (guid, preset) in _settings.Store.Presets)
        {
            var radio = new RadioButton
            {
                Content = preset.Name,
                Tag = guid,
                IsChecked = guid == _trigger.PresetId,
                Margin = new(0, 0, 0, 8)
            };
            _content.Children.Add(radio);
        }
    }
}
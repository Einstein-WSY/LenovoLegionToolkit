using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “God Mode 预设”步骤的编辑器：以下拉框选择要应用的预设（以 Guid 标识）。
/// </summary>
public class GodModePresetAutomationStepControl : AbstractAutomationStepControl<GodModePresetAutomationStep>
{
    // 预设下拉框，加载完成前隐藏以避免闪烁。
    private readonly ComboBox _comboBox = new()
    {
        MinWidth = 150,
        Visibility = Visibility.Hidden,
        Margin = new(8, 0, 0, 0)
    };

    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public GodModePresetAutomationStepControl(GodModePresetAutomationStep step) : base(step)
    {
        Icon = SymbolRegular.Gauge24;
        Title = Resource.GodModePresetAutomationStepControl_Title;
        Subtitle = Resource.GodModePresetAutomationStepControl_Message;
    }

    /// <summary>
    /// 根据下拉框当前选中项创建步骤；未选中任何预设时使用空 Guid。
    /// </summary>
    /// <returns>新的步骤实例。</returns>
    public override IAutomationStep CreateAutomationStep()
    {
        var presetId = Guid.Empty;

        if (_comboBox.TryGetSelectedItem(out KeyValuePair<Guid, GodModePreset> value))
            presetId = value.Key;

        return new GodModePresetAutomationStep(presetId);
    }

    /// <summary>
    /// 提供预设下拉框控件。
    /// </summary>
    /// <returns>下拉框控件。</returns>
    protected override UIElement GetCustomControl()
    {
        _comboBox.SelectionChanged += ComboBox_SelectionChanged;
        return _comboBox;
    }

    /// <summary>
    /// 下拉选择变化时通知上层步骤参数已变化。
    /// </summary>
    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) => RaiseChanged();

    /// <summary>
    /// 拉取所有预设填充下拉框，并在无预设时禁用下拉框。
    /// </summary>
    protected override async Task RefreshAsync()
    {
        var state = await AutomationStep.GetStateAsync();
        var presets = state.Presets;
        var selectedPreset = presets.FirstOrDefault(kv => kv.Key == AutomationStep.PresetId);

        _comboBox.SetItems(presets, selectedPreset, kv => kv.Value.Name);
        _comboBox.IsEnabled = presets.Count != 0;
    }

    /// <summary>
    /// 加载完成后显示下拉框。
    /// </summary>
    protected override void OnFinishedLoading() => _comboBox.Visibility = Visibility.Visible;
}

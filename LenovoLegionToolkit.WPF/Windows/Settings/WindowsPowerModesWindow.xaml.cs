using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.WPF.Extensions;

namespace LenovoLegionToolkit.WPF.Windows.Settings;

/// <summary>
/// Windows 电源模式映射窗口，为每种 Legion 电源模式指定对应的 Windows 电源模式。
/// </summary>
public partial class WindowsPowerModesWindow
{
    private readonly PowerModeFeature _powerModeFeature = IoCContainer.Resolve<PowerModeFeature>();
    private readonly ApplicationSettings _settings = IoCContainer.Resolve<ApplicationSettings>();

    /// <summary>是否正在刷新界面，用于屏蔽回填过程触发的选择变更。</summary>
    private bool IsRefreshing => _loader.IsLoading;

    /// <summary>
    /// 初始化窗口并订阅可见性变化。
    /// </summary>
    public WindowsPowerModesWindow()
    {
        InitializeComponent();

        IsVisibleChanged += PowerModesWindow_IsVisibleChanged;
    }

    private async void PowerModesWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            await RefreshAsync();
    }

    /// <summary>
    /// 用全部 Windows 电源模式填充各下拉框，不支持上帝模式时隐藏对应卡片。
    /// </summary>
    private async Task RefreshAsync()
    {
        _loader.IsLoading = true;

        var loadingTask = Task.Delay(500);

        var powerModes = Enum.GetValues<WindowsPowerMode>();
        Refresh(_quietModeComboBox, powerModes, PowerModeState.Quiet);
        Refresh(_balanceModeComboBox, powerModes, PowerModeState.Balance);
        Refresh(_performanceModeComboBox, powerModes, PowerModeState.Performance);

        var allStates = await _powerModeFeature.GetAllStatesAsync();
        if (allStates.Contains(PowerModeState.GodMode))
            Refresh(_godModeComboBox, powerModes, PowerModeState.GodMode);
        else
            _godModeCardControl.Visibility = Visibility.Collapsed;

        await loadingTask;

        _loader.IsLoading = false;
    }

    /// <summary>
    /// 按已保存的映射选中下拉框中的对应项。
    /// </summary>
    private void Refresh(ComboBox comboBox, WindowsPowerMode[] windowsPowerPlans, PowerModeState powerModeState)
    {
        var selectedValue = _settings.Store.PowerModes.GetValueOrDefault(powerModeState, WindowsPowerMode.Balanced);
        comboBox.SetItems(windowsPowerPlans, selectedValue, pm => pm.GetDisplayName());
    }

    /// <summary>
    /// 保存电源模式映射并立即应用到系统电源设置。
    /// </summary>
    private async Task WindowsPowerModeChangedAsync(WindowsPowerMode windowsPowerMode, PowerModeState powerModeState)
    {
        if (IsRefreshing)
            return;

        _settings.Store.PowerModes[powerModeState] = windowsPowerMode;
        _settings.SynchronizeStore();

        await _powerModeFeature.EnsureCorrectWindowsPowerSettingsAreSetAsync();
    }

    // 各下拉框的选择变更分别映射到对应的 Legion 电源模式
    private async void QuietModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_quietModeComboBox.TryGetSelectedItem(out WindowsPowerMode windowsPowerMode))
            await WindowsPowerModeChangedAsync(windowsPowerMode, PowerModeState.Quiet);
    }

    private async void BalanceModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_balanceModeComboBox.TryGetSelectedItem(out WindowsPowerMode windowsPowerMode))
            await WindowsPowerModeChangedAsync(windowsPowerMode, PowerModeState.Balance);
    }

    private async void PerformanceModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_performanceModeComboBox.TryGetSelectedItem(out WindowsPowerMode windowsPowerMode))
            await WindowsPowerModeChangedAsync(windowsPowerMode, PowerModeState.Performance);
    }

    private async void GodModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_godModeComboBox.TryGetSelectedItem(out WindowsPowerMode windowsPowerMode))
            await WindowsPowerModeChangedAsync(windowsPowerMode, PowerModeState.GodMode);
    }
}
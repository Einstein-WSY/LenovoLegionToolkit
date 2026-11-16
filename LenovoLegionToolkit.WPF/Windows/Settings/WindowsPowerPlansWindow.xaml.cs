using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;

namespace LenovoLegionToolkit.WPF.Windows.Settings;

/// <summary>
/// Windows 电源计划映射窗口，为每种 Legion 电源模式指定对应的 Windows 电源计划。
/// </summary>
public partial class WindowsPowerPlansWindow
{
    // 表示"不指定电源计划，沿用系统默认"的占位项
    private static readonly WindowsPowerPlan DefaultValue = new(Guid.Empty, Resource.WindowsPowerPlansWindow_DefaultPowerPlan, false);

    private readonly WindowsPowerPlanController _windowsPowerPlanController = IoCContainer.Resolve<WindowsPowerPlanController>();
    private readonly PowerModeFeature _powerModeFeature = IoCContainer.Resolve<PowerModeFeature>();
    private readonly ApplicationSettings _settings = IoCContainer.Resolve<ApplicationSettings>();

    /// <summary>是否正在刷新界面，用于屏蔽回填过程触发的选择变更。</summary>
    private bool IsRefreshing => _loader.IsLoading;

    /// <summary>
    /// 初始化窗口并订阅可见性变化。
    /// </summary>
    public WindowsPowerPlansWindow()
    {
        InitializeComponent();

        IsVisibleChanged += PowerPlansWindow_IsVisibleChanged;
    }

    private async void PowerPlansWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            await RefreshAsync();
    }

    /// <summary>
    /// 枚举系统电源计划填充下拉框，并按硬件能力显示 Always On AC 警告。
    /// </summary>
    private async Task RefreshAsync()
    {
        _loader.IsLoading = true;

        var loadingTask = Task.Delay(500);

        var compatibility = await Compatibility.GetMachineInformationAsync();
        _aoAcWarningCard.Visibility = compatibility.Properties.SupportsAlwaysOnAc.status
            ? Visibility.Visible
            : Visibility.Collapsed;

        var powerPlans = _windowsPowerPlanController.GetPowerPlans().OrderBy(x => x.Name).Prepend(DefaultValue).ToArray();
        Refresh(_quietModeComboBox, powerPlans, PowerModeState.Quiet);
        Refresh(_balanceModeComboBox, powerPlans, PowerModeState.Balance);
        Refresh(_performanceModeComboBox, powerPlans, PowerModeState.Performance);

        var allStates = await _powerModeFeature.GetAllStatesAsync();
        if (allStates.Contains(PowerModeState.GodMode))
            Refresh(_godModeComboBox, powerPlans, PowerModeState.GodMode);
        else
            _godModeCardControl.Visibility = Visibility.Collapsed;

        await loadingTask;

        _loader.IsLoading = false;
    }

    /// <summary>
    /// 按已保存的电源计划 GUID 选中下拉框中的对应项。
    /// </summary>
    private void Refresh(ComboBox comboBox, WindowsPowerPlan[] windowsPowerPlans, PowerModeState powerModeState)
    {
        var settingsPowerPlanGuid = _settings.Store.PowerPlans.GetValueOrDefault(powerModeState);
        var selectedValue = windowsPowerPlans.FirstOrDefault(pp => pp.Guid == settingsPowerPlanGuid);
        comboBox.SetItems(windowsPowerPlans, selectedValue, pp => pp.Name);
    }

    /// <summary>
    /// 保存电源计划映射并立即应用到系统。
    /// </summary>
    private async Task WindowsPowerPlanChangedAsync(WindowsPowerPlan windowsPowerPlan, PowerModeState powerModeState)
    {
        if (IsRefreshing)
            return;

        _settings.Store.PowerPlans[powerModeState] = windowsPowerPlan.Guid;
        _settings.SynchronizeStore();

        await _powerModeFeature.EnsureCorrectWindowsPowerSettingsAreSetAsync();
    }

    // 各下拉框的选择变更分别映射到对应的 Legion 电源模式
    private async void QuietModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_quietModeComboBox.TryGetSelectedItem(out WindowsPowerPlan windowsPowerPlan))
            await WindowsPowerPlanChangedAsync(windowsPowerPlan, PowerModeState.Quiet);
    }

    private async void BalanceModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_balanceModeComboBox.TryGetSelectedItem(out WindowsPowerPlan windowsPowerPlan))
            await WindowsPowerPlanChangedAsync(windowsPowerPlan, PowerModeState.Balance);
    }

    private async void PerformanceModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_performanceModeComboBox.TryGetSelectedItem(out WindowsPowerPlan windowsPowerPlan))
            await WindowsPowerPlanChangedAsync(windowsPowerPlan, PowerModeState.Performance);
    }

    private async void GodModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_godModeComboBox.TryGetSelectedItem(out WindowsPowerPlan windowsPowerPlan))
            await WindowsPowerPlanChangedAsync(windowsPowerPlan, PowerModeState.GodMode);
    }
}

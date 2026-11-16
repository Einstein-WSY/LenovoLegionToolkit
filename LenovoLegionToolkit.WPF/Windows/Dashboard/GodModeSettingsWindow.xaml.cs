using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers.GodMode;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;

namespace LenovoLegionToolkit.WPF.Windows.Dashboard;

/// <summary>
/// 上帝模式设置窗口，用于编辑自定义预设中的 CPU/GPU 功耗、温度限制与风扇曲线等参数。
/// </summary>
public partial class GodModeSettingsWindow
{
    private readonly PowerModeFeature _powerModeFeature = IoCContainer.Resolve<PowerModeFeature>();
    private readonly GodModeController _godModeController = IoCContainer.Resolve<GodModeController>();

    private readonly VantageDisabler _vantageDisabler = IoCContainer.Resolve<VantageDisabler>();
    private readonly LegionZoneDisabler _legionZoneDisabler = IoCContainer.Resolve<LegionZoneDisabler>();

    private GodModeState? _state;
    private Dictionary<PowerModeState, GodModeDefaults>? _defaults;
    private bool _isRefreshing;

    /// <summary>
    /// 初始化窗口并订阅可见性变化。
    /// </summary>
    public GodModeSettingsWindow()
    {
        InitializeComponent();

        IsVisibleChanged += GodModeSettingsWindow_IsVisibleChanged;
    }

    /// <summary>
    /// 窗口每次显示时重新加载上帝模式状态。
    /// </summary>
    private async void GodModeSettingsWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            await RefreshAsync();
    }

    /// <summary>
    /// 从控制器读取当前状态与其他模式的默认值，并回填到界面控件。
    /// </summary>
    private async Task RefreshAsync()
    {
        // 标记刷新中，避免回填控件时触发滑块联动逻辑
        _isRefreshing = true;

        try
        {
            _loader.IsLoading = true;
            _buttonsStackPanel.Visibility = Visibility.Hidden;

            var loadingTask = Task.Delay(TimeSpan.FromMilliseconds(500));

            // 仅当控制器确实需要禁用对应软件且该软件正在运行时才显示警告
            _vantageRunningWarningInfoBar.IsOpen = await _godModeController.NeedsVantageDisabledAsync() && await _vantageDisabler.GetStatusAsync() == SoftwareStatus.Enabled;
            _legionZoneRunningWarningInfoBar.IsOpen = await _godModeController.NeedsLegionZoneDisabledAsync() && await _legionZoneDisabler.GetStatusAsync() == SoftwareStatus.Enabled;

            _state = await _godModeController.GetStateAsync();
            _defaults = await _godModeController.GetDefaultsInOtherPowerModesAsync();

            if (_state is null)
                throw new InvalidOperationException($"{nameof(_state)} is null");

            if (_defaults is null)
                throw new InvalidOperationException($"{nameof(_defaults)} are null");

            await SetStateAsync(_state.Value);

            await loadingTask;

            _loadButton.Visibility = _defaults.Count != 0 ? Visibility.Visible : Visibility.Collapsed;
            _buttonsStackPanel.Visibility = Visibility.Visible;
            _loader.IsLoading = false;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't load settings.", ex);

            await _snackBar.ShowAsync(Resource.GodModeSettingsWindow_Error_Load_Title, ex.Message);

            Close();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    /// <summary>
    /// 将界面上的参数收集为新的预设，并切换电源模式后应用到硬件。
    /// </summary>
    /// <returns>应用成功返回 <see langword="true"/>，失败时提示错误并返回 <see langword="false"/>。</returns>
    private async Task<bool> ApplyAsync()
    {
        try
        {
            if (!_state.HasValue)
                throw new InvalidOperationException("State is null");

            var activePresetId = _state.Value.ActivePresetId;
            var presets = _state.Value.Presets;
            var preset = presets[activePresetId];

            // 仅当原始预设中该参数存在时才读取控件值，从而保留"硬件不支持"的语义
            var newPreset = new GodModePreset
            {
                Name = preset.Name,
                CPULongTermPowerLimit = preset.CPULongTermPowerLimit?.WithValue(_cpuLongTermPowerLimitControl.Value),
                CPUShortTermPowerLimit = preset.CPUShortTermPowerLimit?.WithValue(_cpuShortTermPowerLimitControl.Value),
                CPUPeakPowerLimit = preset.CPUPeakPowerLimit?.WithValue(_cpuPeakPowerLimitControl.Value),
                CPUCrossLoadingPowerLimit = preset.CPUCrossLoadingPowerLimit?.WithValue(_cpuCrossLoadingLimitControl.Value),
                CPUPL1Tau = preset.CPUPL1Tau?.WithValue(_cpuPL1TauControl.Value),
                APUsPPTPowerLimit = preset.APUsPPTPowerLimit?.WithValue(_apuSPPTPowerLimitControl.Value),
                CPUTemperatureLimit = preset.CPUTemperatureLimit?.WithValue(_cpuTemperatureLimitControl.Value),
                GPUPowerBoost = preset.GPUPowerBoost?.WithValue(_gpuPowerBoostControl.Value),
                GPUConfigurableTGP = preset.GPUConfigurableTGP?.WithValue(_gpuConfigurableTGPControl.Value),
                GPUTemperatureLimit = preset.GPUTemperatureLimit?.WithValue(_gpuTemperatureLimitControl.Value),
                GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline = preset.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline?.WithValue(_gpuTotalProcessingPowerTargetOnAcOffsetFromBaselineControl.Value),
                GPUToCPUDynamicBoost = preset.GPUToCPUDynamicBoost?.WithValue(_gpuToCpuDynamicBoostControl.Value),
                FanTableInfo = preset.FanTableInfo is not null ? _fanCurveControl.GetFanTableInfo() : null,
                FanFullSpeed = preset.FanFullSpeed is not null ? _fanFullSpeedToggle.IsChecked : null,
                MaxValueOffset = preset.MaxValueOffset is not null ? (int?)_maxValueOffsetNumberBox.Value : null,
                MinValueOffset = preset.MinValueOffset is not null ? (int?)_minValueOffsetNumberBox.Value : null
            };

            var newPresets = new Dictionary<Guid, GodModePreset>(presets)
            {
                [activePresetId] = newPreset
            };

            var newState = new GodModeState
            {
                ActivePresetId = activePresetId,
                Presets = newPresets.AsReadOnlyDictionary(),
            };

            // 若当前不处于上帝模式，先切换模式再写入参数
            if (await _powerModeFeature.GetStateAsync() != PowerModeState.GodMode)
                await _powerModeFeature.SetStateAsync(PowerModeState.GodMode);

            await _godModeController.SetStateAsync(newState);
            await _godModeController.ApplyStateAsync();

            return true;
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't apply settings", ex);

            await _snackBar.ShowAsync(Resource.GodModeSettingsWindow_Error_Apply_Title, ex.Message);

            return false;
        }
    }

    /// <summary>
    /// 将指定预设状态回填到各控件，并按硬件实际支持情况隐藏不可用区块。
    /// </summary>
    /// <param name="state">要展示的上帝模式状态。</param>
    private async Task SetStateAsync(GodModeState state)
    {
        // 回填期间临时退订事件，避免连锁触发功耗限制联动
        _cpuLongTermPowerLimitControl.ValueChanged -= CpuLongTermPowerLimitSlider_ValueChanged;
        _cpuShortTermPowerLimitControl.ValueChanged -= CpuShortTermPowerLimitSlider_ValueChanged;

        var activePresetId = state.ActivePresetId;
        var preset = state.Presets[activePresetId];

        _presetsComboBox.SetItems(state.Presets.OrderBy(kv => kv.Value.Name), new(activePresetId, preset), kv => kv.Value.Name);

        _deletePresetsButton.IsEnabled = state.Presets.Count > 1;

        _cpuLongTermPowerLimitControl.Set(preset.CPULongTermPowerLimit);
        _cpuShortTermPowerLimitControl.Set(preset.CPUShortTermPowerLimit);
        _cpuPeakPowerLimitControl.Set(preset.CPUPeakPowerLimit);
        _cpuCrossLoadingLimitControl.Set(preset.CPUCrossLoadingPowerLimit);
        _cpuPL1TauControl.Set(preset.CPUPL1Tau);
        _apuSPPTPowerLimitControl.Set(preset.APUsPPTPowerLimit);
        _cpuTemperatureLimitControl.Set(preset.CPUTemperatureLimit);
        _gpuPowerBoostControl.Set(preset.GPUPowerBoost);
        _gpuConfigurableTGPControl.Set(preset.GPUConfigurableTGP);
        _gpuTemperatureLimitControl.Set(preset.GPUTemperatureLimit);
        _gpuTotalProcessingPowerTargetOnAcOffsetFromBaselineControl.Set(preset.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline);
        _gpuToCpuDynamicBoostControl.Set(preset.GPUToCPUDynamicBoost);

        var fanTableInfo = preset.FanTableInfo;
        if (fanTableInfo.HasValue)
        {
            var minimum = await _godModeController.GetMinimumFanTableAsync();
            _fanCurveControl.SetFanTableInfo(fanTableInfo.Value, minimum);
        }
        else
        {
            _fanCurveCardControl.Visibility = Visibility.Collapsed;
        }

        var fanFullSpeed = preset.FanFullSpeed;
        if (fanFullSpeed.HasValue)
        {
            _fanCurveCardControl.IsEnabled = !fanFullSpeed.Value;
            _fanFullSpeedToggle.IsChecked = fanFullSpeed.Value;
        }
        else
        {
            _fanCurveCardControl.IsEnabled = true;
            _fanFullSpeedCardControl.Visibility = Visibility.Collapsed;
        }

        var maxValueOffset = preset.MaxValueOffset;
        if (maxValueOffset.HasValue)
            _maxValueOffsetNumberBox.Value = maxValueOffset;
        else
            _maxValueOffsetCardControl.Visibility = Visibility.Collapsed;

        var minValueOffset = preset.MinValueOffset;
        if (minValueOffset.HasValue)
            _minValueOffsetNumberBox.Value = minValueOffset;
        else
            _minValueOffsetCardControl.Visibility = Visibility.Collapsed;

        var cpuSectionVisible = new[]
        {
            _cpuLongTermPowerLimitControl,
            _cpuShortTermPowerLimitControl,
            _cpuPeakPowerLimitControl,
            _cpuCrossLoadingLimitControl,
            _cpuPL1TauControl,
            _apuSPPTPowerLimitControl,
            _cpuTemperatureLimitControl
        }.Any(v => v.Visibility == Visibility.Visible);

        var gpuSectionVisible = new[]
        {
            _gpuPowerBoostControl,
            _gpuConfigurableTGPControl,
            _gpuTemperatureLimitControl,
            _gpuTotalProcessingPowerTargetOnAcOffsetFromBaselineControl,
            _gpuToCpuDynamicBoostControl
        }.Any(v => v.Visibility == Visibility.Visible);

        var fanSectionVisible = new[]
        {
            _fanCurveCardControl,
            _fanFullSpeedCardControl
        }.Any(v => v.Visibility == Visibility.Visible);

        var advancedSectionVisible = new[]
        {
            _maxValueOffsetCardControl,
            _minValueOffsetCardControl
        }.Any(v => v.Visibility == Visibility.Visible);

        _cpuSectionTitle.Visibility = cpuSectionVisible ? Visibility.Visible : Visibility.Collapsed;
        _gpuSectionTitle.Visibility = gpuSectionVisible ? Visibility.Visible : Visibility.Collapsed;
        _fanSectionTitle.Visibility = fanSectionVisible ? Visibility.Visible : Visibility.Collapsed;
        _advancedSectionTitle.Visibility = advancedSectionVisible ? Visibility.Visible : Visibility.Collapsed;
        _advancedSectionMessage.Visibility = advancedSectionVisible ? Visibility.Visible : Visibility.Collapsed;

        _cpuLongTermPowerLimitControl.ValueChanged += CpuLongTermPowerLimitSlider_ValueChanged;
        _cpuShortTermPowerLimitControl.ValueChanged += CpuShortTermPowerLimitSlider_ValueChanged;
    }

    /// <summary>
    /// 将其他电源模式的默认参数写入当前可见的控件；不可见的控件保持原值。
    /// </summary>
    /// <param name="defaults">要应用的默认值。</param>
    private async void SetDefaults(GodModeDefaults defaults)
    {
        if (_cpuLongTermPowerLimitControl.Visibility == Visibility.Visible && defaults.CPULongTermPowerLimit is { } cpuLongTermPowerLimit)
            _cpuLongTermPowerLimitControl.Value = cpuLongTermPowerLimit;

        if (_cpuShortTermPowerLimitControl.Visibility == Visibility.Visible && defaults.CPUShortTermPowerLimit is { } cpuShortTermPowerLimit)
            _cpuShortTermPowerLimitControl.Value = cpuShortTermPowerLimit;

        if (_cpuPeakPowerLimitControl.Visibility == Visibility.Visible && defaults.CPUPeakPowerLimit is { } cpuPeakPowerLimit)
            _cpuPeakPowerLimitControl.Value = cpuPeakPowerLimit;

        if (_cpuCrossLoadingLimitControl.Visibility == Visibility.Visible && defaults.CPUCrossLoadingPowerLimit is { } cpuCrossLoadingPowerLimit)
            _cpuCrossLoadingLimitControl.Value = cpuCrossLoadingPowerLimit;

        if (_cpuPL1TauControl.Visibility == Visibility.Visible && defaults.CPUPL1Tau is { } cpuPL1Tau)
            _cpuPL1TauControl.Value = cpuPL1Tau;

        if (_apuSPPTPowerLimitControl.Visibility == Visibility.Visible && defaults.APUsPPTPowerLimit is { } apuSPPTPowerLimit)
            _apuSPPTPowerLimitControl.Value = apuSPPTPowerLimit;

        if (_cpuTemperatureLimitControl.Visibility == Visibility.Visible && defaults.CPUTemperatureLimit is { } cpuTemperatureLimit)
            _cpuTemperatureLimitControl.Value = cpuTemperatureLimit;

        if (_gpuPowerBoostControl.Visibility == Visibility.Visible && defaults.GPUPowerBoost is { } gpuPowerBoost)
            _gpuPowerBoostControl.Value = gpuPowerBoost;

        if (_gpuConfigurableTGPControl.Visibility == Visibility.Visible && defaults.GPUConfigurableTGP is { } gpuConfigurableTgp)
            _gpuConfigurableTGPControl.Value = gpuConfigurableTgp;

        if (_gpuTemperatureLimitControl.Visibility == Visibility.Visible && defaults.GPUTemperatureLimit is { } gpuTemperatureLimit)
            _gpuTemperatureLimitControl.Value = gpuTemperatureLimit;

        if (_gpuTotalProcessingPowerTargetOnAcOffsetFromBaselineControl.Visibility == Visibility.Visible && defaults.GPUTotalProcessingPowerTargetOnAcOffsetFromBaseline is { } gpuTotalProcessingPowerTargetOnAcOffsetFromBaseline)
            _gpuTotalProcessingPowerTargetOnAcOffsetFromBaselineControl.Value = gpuTotalProcessingPowerTargetOnAcOffsetFromBaseline;

        if (_gpuToCpuDynamicBoostControl.Visibility == Visibility.Visible && defaults.GPUToCPUDynamicBoost is { } gpuToCPUDynamicBoost)
            _gpuToCpuDynamicBoostControl.Value = gpuToCPUDynamicBoost;

        if (_fanCurveCardControl.Visibility == Visibility.Visible && defaults.FanTable is { } fanTable)
        {
            var state = await _godModeController.GetStateAsync();
            var preset = state.Presets[state.ActivePresetId];
            var data = preset.FanTableInfo?.Data;

            if (data is not null)
            {
                var defaultFanTableInfo = new FanTableInfo(data, fanTable);
                var minimum = await _godModeController.GetMinimumFanTableAsync();
                _fanCurveControl.SetFanTableInfo(defaultFanTableInfo, minimum);
            }
        }

        if (_fanFullSpeedCardControl.Visibility == Visibility.Visible && defaults.FanFullSpeed is { } fanFullSpeed)
            _fanFullSpeedToggle.IsChecked = fanFullSpeed;

        if (_maxValueOffsetCardControl.Visibility == Visibility.Visible)
            _maxValueOffsetNumberBox.Text = "0";

        if (_minValueOffsetCardControl.Visibility == Visibility.Visible)
            _minValueOffsetNumberBox.Text = "0";
    }

    /// <summary>
    /// 切换预设时更新当前活动预设并刷新界面。
    /// </summary>
    private async void PresetsComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_state.HasValue)
            return;

        if (!_presetsComboBox.TryGetSelectedItem<KeyValuePair<Guid, GodModePreset>>(out var item))
            return;

        if (_state.Value.ActivePresetId == item.Key)
            return;

        _state = _state.Value with { ActivePresetId = item.Key };
        await SetStateAsync(_state.Value);
    }

    /// <summary>
    /// 通过输入对话框重命名当前预设。
    /// </summary>
    private async void EditPresetsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_state.HasValue)
            return;

        var activePresetId = _state.Value.ActivePresetId;
        var presets = _state.Value.Presets;
        var preset = presets[activePresetId];

        var result = await MessageBoxHelper.ShowInputAsync(this, Resource.GodModeSettingsWindow_EditPreset_Title, Resource.GodModeSettingsWindow_EditPreset_Message, preset.Name);
        if (string.IsNullOrEmpty(result))
            return;

        var newPresets = new Dictionary<Guid, GodModePreset>(presets)
        {
            [activePresetId] = preset with { Name = result }
        };
        _state = _state.Value with { Presets = newPresets.AsReadOnlyDictionary() };
        await SetStateAsync(_state.Value);
    }

    /// <summary>
    /// 删除当前预设，并自动选中剩余预设中名称排序最靠前的一个。
    /// </summary>
    private async void DeletePresetsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_state.HasValue)
            return;

        if (_state.Value.Presets.Count <= 1)
            return;

        var activePresetId = _state.Value.ActivePresetId;
        var presets = _state.Value.Presets;

        var newPresets = new Dictionary<Guid, GodModePreset>(presets);
        newPresets.Remove(activePresetId);
        var newActivePresetId = newPresets.OrderBy(kv => kv.Value.Name)
            .Select(kv => kv.Key)
            .First();

        _state = new()
        {
            ActivePresetId = newActivePresetId,
            Presets = newPresets.AsReadOnlyDictionary()
        };
        await SetStateAsync(_state.Value);
    }

    /// <summary>
    /// 以当前预设为模板复制出一个新预设。
    /// </summary>
    private async void AddPresetsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_state.HasValue)
            return;

        var result = await MessageBoxHelper.ShowInputAsync(this, Resource.GodModeSettingsWindow_EditPreset_Title, Resource.GodModeSettingsWindow_EditPreset_Message);
        if (string.IsNullOrEmpty(result))
            return;

        var activePresetId = _state.Value.ActivePresetId;
        var presets = _state.Value.Presets;
        var preset = presets[activePresetId];

        var newActivePresetId = Guid.NewGuid();
        var newPreset = preset with { Name = result };
        var newPresets = new Dictionary<Guid, GodModePreset>(presets)
        {
            [newActivePresetId] = newPreset
        };

        _state = new()
        {
            ActivePresetId = newActivePresetId,
            Presets = newPresets.AsReadOnlyDictionary()
        };

        await SetStateAsync(_state.Value);
    }

    /// <summary>
    /// 将风扇曲线恢复为该预设的出厂默认曲线。
    /// </summary>
    private async void DefaultFanCurve_Click(object sender, RoutedEventArgs e)
    {
        var state = await _godModeController.GetStateAsync();
        var preset = state.Presets[state.ActivePresetId];
        var data = preset.FanTableInfo?.Data;

        if (data is null)
            return;

        var defaultFanTable = await _godModeController.GetDefaultFanTableAsync();
        var defaultFanTableInfo = new FanTableInfo(data, defaultFanTable);
        var minimum = await _godModeController.GetMinimumFanTableAsync();
        _fanCurveControl.SetFanTableInfo(defaultFanTableInfo, minimum);
    }

    /// <summary>
    /// 打开下拉菜单，列出其他电源模式的默认值供加载。
    /// </summary>
    private void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_defaults is null || _defaults.IsEmpty())
        {
            _loadButton.Visibility = Visibility.Collapsed;
            return;
        }

        var menuItems = _defaults
            .OrderBy(d => d.Key)
            .Select(d =>
            {
                var menuItem = new MenuItem { Header = d.Key.GetDisplayName() };
                menuItem.Click += (_, _) => SetDefaults(d.Value);
                return menuItem;
            });

        var contextMenu = new ContextMenu
        {
            PlacementTarget = _loadButton,
            Placement = PlacementMode.Bottom,
        };

        foreach (var menuItem in menuItems)
            contextMenu.Items.Add(menuItem);

        _loadButton.ContextMenu = contextMenu;
        _loadButton.ContextMenu.IsOpen = true;
    }

    /// <summary>
    /// 应用设置并在成功后关闭窗口。
    /// </summary>
    private async void SaveAndCloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (await ApplyAsync())
            Close();
    }

    /// <summary>
    /// 应用设置并刷新界面，保持窗口打开。
    /// </summary>
    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await ApplyAsync();
        await RefreshAsync();
    }

    /// <summary>
    /// 长时功耗上限超过短时上限时，将短时上限提升到相同值，保证约束合法。
    /// </summary>
    private void CpuLongTermPowerLimitSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isRefreshing)
            return;

        if (_cpuLongTermPowerLimitControl.Value > _cpuShortTermPowerLimitControl.Value)
            _cpuShortTermPowerLimitControl.Value = _cpuLongTermPowerLimitControl.Value;
    }

    /// <summary>
    /// 短时功耗上限低于长时上限时，将长时上限下调到相同值，保证约束合法。
    /// </summary>
    private void CpuShortTermPowerLimitSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isRefreshing)
            return;

        if (_cpuLongTermPowerLimitControl.Value > _cpuShortTermPowerLimitControl.Value)
            _cpuLongTermPowerLimitControl.Value = _cpuShortTermPowerLimitControl.Value;
    }

    /// <summary>
    /// 启用风扇全速时禁用曲线编辑，避免两种控制方式冲突。
    /// </summary>
    private void FanFullSpeedToggle_Click(object sender, RoutedEventArgs e)
    {
        _fanCurveCardControl.IsEnabled = !(_fanFullSpeedToggle.IsChecked ?? false);
    }
}

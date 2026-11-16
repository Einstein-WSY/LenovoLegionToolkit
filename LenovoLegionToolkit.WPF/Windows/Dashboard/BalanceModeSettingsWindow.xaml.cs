using System.Windows;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Features;

namespace LenovoLegionToolkit.WPF.Windows.Dashboard;

/// <summary>
/// 均衡模式设置窗口，用于切换均衡模式下的 AI 智能调度模式。
/// </summary>
public partial class BalanceModeSettingsWindow
{
    private readonly PowerModeFeature _powerModeFeature = IoCContainer.Resolve<PowerModeFeature>();
    private readonly AIController _aiController = IoCContainer.Resolve<AIController>();

    /// <summary>
    /// 初始化窗口并订阅可见性变化。
    /// </summary>
    public BalanceModeSettingsWindow()
    {
        InitializeComponent();

        IsVisibleChanged += BalanceModeSettingsWindow_IsVisibleChanged;
    }

    /// <summary>
    /// 每次显示时从控制器同步 AI 模式的当前状态。
    /// </summary>
    private void BalanceModeSettingsWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!IsVisible)
            return;

        _aiModeCheckBox.IsChecked = _aiController.IsAIModeEnabled;
    }

    /// <summary>
    /// 保存 AI 模式设置并重启 AI 控制器，使新的均衡模式配置立即生效。
    /// </summary>
    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var isAiModeChecked = _aiModeCheckBox.IsChecked ?? false;

        _aiController.IsAIModeEnabled = isAiModeChecked;

        // 先停止控制器再重新应用电源模式，最后按需重启，避免两次切换互相干扰
        await _aiController.StopAsync();
        await _powerModeFeature.SetStateAsync(PowerModeState.Balance);
        await _aiController.StartIfNeededAsync();

        Close();
    }

    /// <summary>关闭窗口且不保存任何更改。</summary>
    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
}

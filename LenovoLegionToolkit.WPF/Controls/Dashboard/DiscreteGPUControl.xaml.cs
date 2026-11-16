using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.WPF.Resources;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “独立显卡”仪表盘状态卡片控件，展示独显当前工作状态（活动/非活动/断电）及占用进程；
/// 数据来自 GPU 控制器的状态刷新事件，并提供停用、结束占用进程、重启显卡等操作。
/// </summary>
public partial class DiscreteGPUControl
{
    /// <summary>独立显卡控制器，负责查询状态、结束占用进程与重启显卡。</summary>
    private readonly GPUController _gpuController = IoCContainer.Resolve<GPUController>();
    /// <summary>原生 Windows 消息监听器，用于感知显示设备到达等系统事件。</summary>
    private readonly NativeWindowsMessageListener _nativeWindowsMessageListener = IoCContainer.Resolve<NativeWindowsMessageListener>();

    /// <summary>
    /// 初始化控件并加载 XAML 视图，同时订阅显卡状态、系统消息与可见性变化事件。
    /// </summary>
    public DiscreteGPUControl()
    {
        InitializeComponent();

        _gpuController.Refreshed += GpuController_Refreshed;
        _nativeWindowsMessageListener.Changed += NativeWindowsMessageListener_Changed;

        IsVisibleChanged += DiscreteGPUControl_IsVisibleChanged;
    }

    /// <summary>
    /// 加载完成回调，此控件无需额外处理。
    /// </summary>
    protected override void OnFinishedLoading() { }

    /// <summary>
    /// 刷新控件：先校验平台是否支持独显控制，卡片可见时才启动状态轮询。
    /// </summary>
    /// <returns>异步刷新任务。</returns>
    protected override async Task OnRefreshAsync()
    {
        if (!_gpuController.IsSupported())
            throw new InvalidOperationException("Unsupported operation");

        // 卡片不可见时无需轮询，避免后台无谓开销。
        if (!IsVisible)
            return;

        await _gpuController.StartAsync();
    }

    /// <summary>
    /// 系统消息回调：仅在显示设备接入（如外接显示器）时重新显示卡片并刷新。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">包含窗口消息类型的参数。</param>
    private async void NativeWindowsMessageListener_Changed(object? sender, NativeWindowsMessageListener.ChangedEventArgs e)
    {
        // 外接显示器接入通常会改变独显工作状态，需要立即复检。
        if (e.Message != NativeWindowsMessage.OnDisplayDeviceArrival)
            return;

        Visibility = Visibility.Visible;
        await RefreshAsync();
    }

    /// <summary>
    /// 可见性变化回调：卡片隐藏时停止显卡状态轮询，避免持续占用资源。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">依赖属性变化参数。</param>
    private async void DiscreteGPUControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            return;

        _content.Visibility = Visibility.Hidden;

        await _gpuController.StopAsync();
    }

    /// <summary>
    /// 显卡状态刷新回调：依据 GPU 状态更新指示灯、描述文本、提示气泡与按钮可用性。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">最新的显卡状态快照。</param>
    private void GpuController_Refreshed(object? sender, GPUStatus e) => Dispatcher.Invoke(() =>
    {
        // 提示气泡首行固定为性能状态，未知时使用占位文本。
        var tooltipStringBuilder = new StringBuilder(Resource.DiscreteGPUControl_PerformanceState);
        tooltipStringBuilder.AppendLine().Append(" · ").Append(e.PerformanceState ?? Resource.DiscreteGPUControl_PerformanceState_Unknown);

        // 状态未知或未检测到 Nvidia 显卡时，隐藏整个卡片。
        if (e.State is GPUState.Unknown or GPUState.NvidiaGpuNotFound)
        {
            _discreteGPUStatusActiveIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusInactiveIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusDescription.Text = "-";
            _gpuInfoButton.ToolTip = null;
            _gpuInfoButton.IsEnabled = false;
            Visibility = Visibility.Collapsed;
            return;
        }

        // 有显示器直连独显时，在提示中追加一行说明。
        if (e.State is GPUState.MonitorConnected)
            tooltipStringBuilder.AppendLine().AppendLine().Append(Resource.DiscreteGPUControl_MonitorConnected);

        // 活动或显示器直连状态下，列出正在使用独显的进程。
        if (e.State is GPUState.Active or GPUState.MonitorConnected)
        {
            var processesStringBuilder = new StringBuilder();

            if (e.ProcessCount > 0)
            {
                processesStringBuilder.Append(Resource.DiscreteGPUControl_Processes);
                foreach (var p in e.Processes.OrderBy(p => p.ProcessName))
                {
                    try { processesStringBuilder.AppendLine().Append(" · ").Append(p.ProcessName); }
                    catch {  /* Ignored. */ }
                }
            }
            else
            {
                processesStringBuilder.Append(Resource.DiscreteGPUControl_NoProcesses);
            }

            _discreteGPUStatusActiveIndicator.Visibility = Visibility.Visible;
            _discreteGPUStatusInactiveIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusPoweredOffIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusDescription.Text = Resource.Active;
            _gpuInfoButton.ToolTip = tooltipStringBuilder.AppendLine().AppendLine().Append(processesStringBuilder).ToString();
            _gpuInfoButton.IsEnabled = true;
        }
        else if (e.State is GPUState.PoweredOff)
        {
            _discreteGPUStatusActiveIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusInactiveIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusPoweredOffIndicator.Visibility = Visibility.Visible;
            _discreteGPUStatusDescription.Text = Resource.PoweredOff;
            _gpuInfoButton.ToolTip = tooltipStringBuilder.ToString();
            _gpuInfoButton.IsEnabled = true;
        }
        else
        {
            _discreteGPUStatusActiveIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusInactiveIndicator.Visibility = Visibility.Visible;
            _discreteGPUStatusPoweredOffIndicator.Visibility = Visibility.Collapsed;
            _discreteGPUStatusDescription.Text = Resource.Inactive;
            _gpuInfoButton.ToolTip = tooltipStringBuilder.ToString();
            _gpuInfoButton.IsEnabled = true;
        }

        // 按状态启用操作按钮：断电状态下不可停用/重启，结束进程仅在活动时可执行。
        _deactivateGPUButton.IsEnabled = e.State is GPUState.Active or GPUState.Inactive;
        _killAppsMenuItem.IsEnabled = e.State is GPUState.Active;
        _restartGPUMenuItem.IsEnabled = e.State is GPUState.Active or GPUState.Inactive;

        if (e.State is GPUState.Active or GPUState.Inactive)
        {
            _deactivateGPUButtonText.SetResourceReference(ForegroundProperty, "TextOnAccentFillColorPrimaryBrush");
            _deactivateGPUButtonIcon.SetResourceReference(ForegroundProperty, "TextOnAccentFillColorPrimaryBrush");
        }
        else
        {
            _deactivateGPUButtonText.SetResourceReference(ForegroundProperty, "TextFillColorDisabledBrush");
            _deactivateGPUButtonIcon.SetResourceReference(ForegroundProperty, "TextFillColorDisabledBrush");
        }

        // 所有状态字段更新完毕后再显示内容，避免闪烁。
        _content.Visibility = Visibility.Visible;
    });

    /// <summary>
    /// 点击“停用显卡”按钮：在其下方弹出上下文菜单，提供结束进程或重启显卡选项。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void DeactivateGPUButton_Click(object sender, RoutedEventArgs e)
    {
        if (_deactivateGPUButton.ContextMenu is null)
            return;

        _deactivateGPUButton.ContextMenu.PlacementTarget = _deactivateGPUButton;
        _deactivateGPUButton.ContextMenu.Placement = PlacementMode.Bottom;
        _deactivateGPUButton.ContextMenu.IsOpen = true;
    }

    /// <summary>
    /// 结束占用独显的进程：执行期间禁用按钮防止重复点击，完成后由状态刷新恢复可用性。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private async void KillAppsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _deactivateGPUButton.IsEnabled = false;
        await _gpuController.KillGPUProcessesAsync();
    }

    /// <summary>
    /// 重启独立显卡：执行期间禁用按钮防止重复点击，完成后由状态刷新恢复可用性。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private async void RestartGPUMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _deactivateGPUButton.IsEnabled = false;
        await _gpuController.RestartGPUAsync();
    }
}

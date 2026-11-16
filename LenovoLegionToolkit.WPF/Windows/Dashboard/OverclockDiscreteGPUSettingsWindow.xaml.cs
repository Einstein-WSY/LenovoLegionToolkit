using System.Threading.Tasks;
using System.Windows;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;

namespace LenovoLegionToolkit.WPF.Windows.Dashboard;

/// <summary>
/// 独立显卡超频设置窗口，通过滑块调整核心与显存频率偏移。
/// </summary>
public partial class OverclockDiscreteGPUSettingsWindow
{
    private const string MHZ = "MHz";

    private readonly GPUOverclockController _gpuOverclockController = IoCContainer.Resolve<GPUOverclockController>();

    /// <summary>
    /// 初始化窗口，读取当前超频状态并回填滑块与数值标签。
    /// </summary>
    public OverclockDiscreteGPUSettingsWindow()
    {
        InitializeComponent();

        var (enabled, info) = _gpuOverclockController.GetState();

        // 已启用超频时提供"应用并关闭"，未启用时提供"保存"入口
        _applyCloseGrid.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        _saveGrid.Visibility = enabled ? Visibility.Collapsed : Visibility.Visible;

        _coreSlider.Maximum = GPUOverclockController.GetMaxCoreDeltaMhz();
        _coreSlider.Value = info.CoreDeltaMhz;
        _memorySlider.Maximum = GPUOverclockController.GetMaxMemoryDeltaMhz();
        _memorySlider.Value = info.MemoryDeltaMhz;

        // 使用 "+0;-0;0" 格式让偏移量始终带符号显示
        _coreLabel.Content = $"{(int)_coreSlider.Value:+0;-0;0} {MHZ}";
        _memoryLabel.Content = $"{(int)_memorySlider.Value:+0;-0;0} {MHZ}";
    }

    /// <summary>核心频率滑块变化时更新数值标签。</summary>
    private void CoreSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => _coreLabel.Content = $"{(int)_coreSlider.Value:+0;-0;0} {MHZ}";

    /// <summary>显存频率滑块变化时更新数值标签。</summary>
    private void MemorySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => _memoryLabel.Content = $"{(int)_memorySlider.Value:+0;-0;0} {MHZ}";

    /// <summary>
    /// 保存当前滑块值并立即应用到显卡，窗口保持打开。
    /// </summary>
    private async void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        Save();
        await ApplyAsync();
    }

    /// <summary>
    /// 保存并应用当前超频设置后关闭窗口。
    /// </summary>
    private async void ApplyAndCloseButton_Click(object sender, RoutedEventArgs e)
    {
        Save();
        await ApplyAsync();
        Close();
    }

    /// <summary>
    /// 仅保存设置（不立即应用）并关闭窗口。
    /// </summary>
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        Save();
        Close();
    }

    /// <summary>
    /// 将滑块数值写入控制器状态。
    /// </summary>
    private void Save()
    {
        var (enabled, _) = _gpuOverclockController.GetState();
        var info = new GPUOverclockInfo((int)_coreSlider.Value, (int)_memorySlider.Value);

        _gpuOverclockController.SaveState(enabled, info);
    }

    /// <summary>
    /// 将已保存的超频设置应用到显卡。
    /// </summary>
    private async Task ApplyAsync() => await _gpuOverclockController.ApplyStateAsync();
}

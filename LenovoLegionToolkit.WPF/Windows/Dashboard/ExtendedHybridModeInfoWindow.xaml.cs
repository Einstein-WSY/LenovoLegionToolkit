using System.Linq;
using System.Windows;
using LenovoLegionToolkit.Lib;

namespace LenovoLegionToolkit.WPF.Windows.Dashboard;

/// <summary>
/// 混合模式说明窗口，根据设备支持的混合模式状态选择性展示各模式的说明面板。
/// </summary>
public partial class ExtendedHybridModeInfoWindow
{
    /// <summary>
    /// 初始化窗口，并按支持的混合模式显示对应的说明区块。
    /// </summary>
    /// <param name="hybridModeStates">设备当前支持的混合模式集合。</param>
    public ExtendedHybridModeInfoWindow(HybridModeState[] hybridModeStates)
    {
        InitializeComponent();

        _hybridPanel.Visibility = hybridModeStates.Contains(HybridModeState.On)
            ? Visibility.Visible
            : Visibility.Collapsed;
        _hybridIgpuPanel.Visibility = hybridModeStates.Contains(HybridModeState.OnIGPUOnly)
            ? Visibility.Visible
            : Visibility.Collapsed;
        _hybridAutoPanel.Visibility = hybridModeStates.Contains(HybridModeState.OnAuto)
            ? Visibility.Visible
            : Visibility.Collapsed;
        _dgpuPanel.Visibility = hybridModeStates.Contains(HybridModeState.Off)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>关闭说明窗口。</summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}

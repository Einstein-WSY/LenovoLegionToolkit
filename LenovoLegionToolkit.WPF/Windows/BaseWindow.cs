using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Windows;

/// <summary>
/// 所有应用窗口的基类，统一设置像素对齐、自定义标题栏与背景材质，并处理 DPI 变化。
/// </summary>
public class BaseWindow : UiWindow
{
    protected BaseWindow()
    {
        SnapsToDevicePixels = true;
        ExtendsContentIntoTitleBar = true;

        WindowBackdropType = BackgroundType.Mica;

        DpiChanged += BaseWindow_DpiChanged;
    }

    /// <summary>
    /// 在窗口跨越不同 DPI 的显示器时同步可视化树根节点的 DPI，避免界面缩放模糊。
    /// </summary>
    private void BaseWindow_DpiChanged(object sender, DpiChangedEventArgs e) => VisualTreeHelper.SetRootDpi(this, e.NewDpi);
}

using System.Threading.Tasks;
using System.Windows;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Listeners;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// “关闭显示器”仪表盘操作卡片控件，点击后通过原生窗口消息关闭全部显示器输出。
/// </summary>
public partial class TurnOffMonitorsControl
{
    /// <summary>原生 Windows 消息监听器，用于向系统发送关闭显示器的消息。</summary>
    private readonly NativeWindowsMessageListener _nativeWindowsMessageListener = IoCContainer.Resolve<NativeWindowsMessageListener>();

    /// <summary>
    /// 初始化控件并加载对应的 XAML 视图。
    /// </summary>
    public TurnOffMonitorsControl() => InitializeComponent();

    /// <summary>
    /// 处理“关闭显示器”按钮点击：执行期间禁用按钮，避免重复触发。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private async void TurnOffButton_Click(object sender, RoutedEventArgs e)
    {
        _turnOffButton.IsEnabled = false;
        await _nativeWindowsMessageListener.TurnOffMonitorAsync();
        _turnOffButton.IsEnabled = true;
    }

    /// <summary>
    /// 该卡片不展示系统状态指标，因此刷新为空操作。
    /// </summary>
    /// <returns>已完成的 <see cref="Task"/>。</returns>
    protected override Task OnRefreshAsync() => Task.CompletedTask;

    /// <summary>
    /// 加载完成回调，此控件无需额外处理。
    /// </summary>
    protected override void OnFinishedLoading() { }
}

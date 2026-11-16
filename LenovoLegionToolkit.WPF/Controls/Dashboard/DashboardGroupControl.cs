using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LenovoLegionToolkit.WPF.Extensions;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// 仪表盘分组容器控件：按分组配置动态创建标题与各指标卡片控件，纵向排列并承载为内容。
/// </summary>
public class DashboardGroupControl : UserControl
{
    /// <summary>用于通知外部“分组内容已全部创建完成”的任务完成源。</summary>
    private readonly TaskCompletionSource _initializedTaskCompletionSource = new();

    /// <summary>该控件所呈现的仪表盘分组定义。</summary>
    private readonly DashboardGroup _dashboardGroup;

    /// <summary>
    /// 获取一个在分组内全部卡片控件创建并挂载完成后才完成的 <see cref="Task"/>。
    /// </summary>
    public Task InitializedTask => _initializedTaskCompletionSource.Task;

    /// <summary>
    /// 使用指定的分组定义初始化控件，并订阅自身的 Loaded/Initialized 事件。
    /// </summary>
    /// <param name="dashboardGroup">要呈现的仪表盘分组。</param>
    public DashboardGroupControl(DashboardGroup dashboardGroup)
    {
        _dashboardGroup = dashboardGroup;

        Initialized += DashboardGroupControl_Initialized;
    }

    /// <summary>
    /// 初始化回调：构建分组标题与各卡片控件，全部就绪后设置完成源并作为内容展示。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数。</param>
    private async void DashboardGroupControl_Initialized(object? sender, System.EventArgs e)
    {
        // 分组使用纵向堆叠布局，右侧留出 16px 间距。
        var stackPanel = new StackPanel { Margin = new(0, 0, 16, 0) };

        var textBlock = new TextBlock
        {
            Text = _dashboardGroup.GetName(),
            Focusable = true,
            FontSize = 24,
            FontWeight = FontWeights.Medium,
            Margin = new(0, 16, 0, 24)
        };
        // 将标题文本同步为无障碍名称，便于辅助技术朗读。
        AutomationProperties.SetName(textBlock, textBlock.Text);
        stackPanel.Children.Add(textBlock);

        // 并行创建分组内所有卡片控件，缩短整体加载耗时。
        var controlsTasks = _dashboardGroup.Items.Select(i => i.GetControlAsync());
        var controls = await Task.WhenAll(controlsTasks);

        foreach (var control in controls.SelectMany(c => c))
        {
            stackPanel.Children.Add(control);
        }

        Content = stackPanel;

        _initializedTaskCompletionSource.TrySetResult();
    }
}

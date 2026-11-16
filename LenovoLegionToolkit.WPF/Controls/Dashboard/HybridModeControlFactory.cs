using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Features.Hybrid;
using LenovoLegionToolkit.Lib.Features.Hybrid.Notify;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;
using LenovoLegionToolkit.WPF.Windows.Dashboard;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;

namespace LenovoLegionToolkit.WPF.Controls.Dashboard;

/// <summary>
/// 混合模式（核显/独显切换）卡片控件工厂：
/// 按机型能力返回下拉框版（支持纯核显模式）或开关版控件。
/// </summary>
public static class HybridModeControlFactory
{
    /// <summary>
    /// 根据机型能力创建对应的混合模式卡片控件。
    /// </summary>
    /// <returns>可直接加入仪表盘的分组控件。</returns>
    public static async Task<AbstractRefreshingControl> GetControlAsync()
    {
        var mi = await Compatibility.GetMachineInformationAsync();
        // 支持纯核显模式的机型提供三态选择，否则退化为简单开关。
        return mi.Properties.SupportsIGPUMode
            ? new ComboBoxHybridModeControl()
            : new ToggleHybridModeControl();
    }

    /// <summary>
    /// 下拉框版混合模式卡片控件：支持在关闭/自动/纯核显等模式间切换。
    /// </summary>
    private class ComboBoxHybridModeControl : AbstractComboBoxFeatureCardControl<HybridModeState>
    {
        /// <summary>独显连接状态通知器，用于在独显接入/断开时提示用户。</summary>
        private readonly DGPUNotify _dgpuNotify = IoCContainer.Resolve<DGPUNotify>();

        /// <summary>打开混合模式说明窗口的信息按钮。</summary>
        private readonly Button _infoButton = new()
        {
            Icon = SymbolRegular.Info24,
            FontSize = 20,
            Margin = new(8, 0, 0, 0),
        };

        /// <summary>
        /// 初始化控件，设置卡片图标与本地化标题、副标题，并订阅独显通知事件。
        /// </summary>
        public ComboBoxHybridModeControl()
        {
            Icon = SymbolRegular.LeafOne24;
            Title = Resource.ComboBoxHybridModeControl_Title;
            Subtitle = Resource.ComboBoxHybridModeControl_Message;

            AutomationProperties.SetName(_infoButton, Resource.ComboBoxHybridModeControl_Title);
            AutomationProperties.SetHelpText(_infoButton, Resource.Information);

            _dgpuNotify.Notified += DGPUNotify_Notified;
        }

        /// <summary>
        /// 构建卡片附加区：下拉框右侧放置信息按钮，并为下拉框设置最小宽度避免文字被截断。
        /// </summary>
        /// <param name="comboBox">基类创建的模式下拉框。</param>
        /// <returns>包含下拉框与信息按钮的横向布局容器。</returns>
        protected override FrameworkElement GetAccessory(ComboBox comboBox)
        {
            comboBox.MinWidth = 150;

            _infoButton.Click += InfoButton_Click;

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
            };
            stackPanel.Children.Add(comboBox);
            stackPanel.Children.Add(_infoButton);

            return stackPanel;
        }

        /// <summary>
        /// 模式切换处理：涉及关闭混合模式时先询问是否立即重启，随后执行切换并按需重启系统。
        /// </summary>
        /// <param name="comboBox">触发切换的下拉框。</param>
        /// <param name="feature">混合模式功能实现。</param>
        /// <param name="newValue">切换后的模式。</param>
        /// <param name="oldValue">切换前的模式。</param>
        /// <returns>异步操作任务。</returns>
        protected override async Task OnStateChangeAsync(ComboBox comboBox, IFeature<HybridModeState> feature, HybridModeState? newValue, HybridModeState? oldValue)
        {
            if (newValue is null || oldValue is null)
                return;

            // 从/到 Off 的切换需要重启才能生效，先征求用户意见。
            var reboot = (newValue == HybridModeState.Off || oldValue == HybridModeState.Off) && await MessageBoxHelper.ShowAsync(this,
                    Resource.ComboBoxHybridModeControl_RestartRequired_Title,
                    string.Format(Resource.ComboBoxHybridModeControl_RestartRequired_Message, newValue.GetDisplayName()),
                    Resource.RestartNow,
                    Resource.RestartLater);

            await base.OnStateChangeAsync(comboBox, feature, newValue, oldValue);

            if (reboot)
            {
                await Power.RestartAsync();
                return;
            }

            await RefreshAsync();
        }

        /// <summary>
        /// 模式切换异常回调：纯核显模式切换失败时按目标模式给出对应提示。
        /// </summary>
        /// <param name="exception">切换过程中抛出的异常。</param>
        protected override void OnStateChangeException(Exception exception)
        {
            if (exception is IGPUModeChangeException ex1)
            {
                var (title, message) = ex1.IGPUMode switch
                {
                    IGPUModeState.IGPUOnly => (Resource.IGPUModeChangeException_Title_IGPUOnly, Resource.IGPUModeChangeException_Message_IGPUOnly),
                    IGPUModeState.Auto => (Resource.IGPUModeChangeException_Title_Auto, Resource.IGPUModeChangeException_Message_Auto),
                    _ => (Resource.IGPUModeChangeException_Title, Resource.IGPUModeChangeException_Message)
                };

                SnackbarHelper.Show(title, message, SnackbarType.Info);
            }
        }

        /// <summary>
        /// 额外的状态切换等待时长：非关闭混合模式的切换需给固件 5 秒完成显卡切换。
        /// </summary>
        /// <param name="oldValue">切换前的模式。</param>
        /// <param name="newValue">切换后的模式。</param>
        /// <returns>需要额外等待的时长。</returns>
        protected override TimeSpan AdditionalStateChangeDelay(HybridModeState? oldValue, HybridModeState? newValue)
        {
            // 涉及 Off 的切换会走重启流程，无需额外等待。
            if (oldValue == HybridModeState.Off || newValue == HybridModeState.Off)
                return TimeSpan.Zero;

            // 核显/混合模式间切换需要硬件重新枚举，预留 5 秒。
            return TimeSpan.FromSeconds(5);
        }

        /// <summary>
        /// 独显接入/断开通知回调：切回 UI 线程弹出提示条告知用户。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">true 表示独显已连接，false 表示已断开。</param>
        private void DGPUNotify_Notified(object? sender, bool e) => Dispatcher.Invoke(() =>
        {
            SnackbarHelper.Show(e ? Resource.DGPU_Connected_Title : Resource.DGPU_Disconnected_Title, type: SnackbarType.Info);
        });

        /// <summary>
        /// 信息按钮点击：查询全部可用模式并以模态窗口展示详细说明。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">路由事件参数。</param>
        private async void InfoButton_Click(object sender, RoutedEventArgs e)
        {
            var states = await Feature.GetAllStatesAsync();
            var window = new ExtendedHybridModeInfoWindow(states) { Owner = Window.GetWindow(this) };
            window.ShowDialog();
        }
    }

    /// <summary>
    /// 开关版混合模式卡片控件：仅支持在开启/关闭混合模式之间切换。
    /// </summary>
    private class ToggleHybridModeControl : AbstractToggleFeatureCardControl<HybridModeState>
    {
        /// <summary>开关处于“开”状态时对应的固件枚举值。</summary>
        protected override HybridModeState OnState => HybridModeState.On;

        /// <summary>开关处于“关”状态时对应的固件枚举值。</summary>
        protected override HybridModeState OffState => HybridModeState.Off;

        /// <summary>
        /// 初始化控件，设置卡片图标与本地化标题、副标题。
        /// </summary>
        public ToggleHybridModeControl()
        {
            Icon = SymbolRegular.LeafOne24;
            Title = Resource.ToggleHybridModeControl_Title;
            Subtitle = Resource.ToggleHybridModeControl_Message;
        }

        /// <summary>
        /// 处理开关切换：切换后询问是否立即重启以使混合模式设置生效。
        /// </summary>
        /// <param name="toggle">触发切换的开关控件。</param>
        /// <param name="feature">混合模式功能实现。</param>
        /// <returns>异步操作任务。</returns>
        protected override async Task OnStateChange(ToggleSwitch toggle, IFeature<HybridModeState> feature)
        {
            await base.OnStateChange(toggle, feature);

            var result = await MessageBoxHelper.ShowAsync(
                this,
                Resource.ToggleHybridModeControl_RestartRequired_Title,
                Resource.ToggleHybridModeControl_RestartRequired_Message,
                Resource.RestartNow,
                Resource.RestartLater);

            if (result)
                await Power.RestartAsync();
        }
    }
}

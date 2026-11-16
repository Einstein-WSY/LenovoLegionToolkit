using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation;
using LenovoLegionToolkit.Lib.Automation.Pipeline;
using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.WPF.Controls.Automation;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;
using LenovoLegionToolkit.WPF.Windows.Automation;
using LenovoLegionToolkit.WPF.Windows.Utils;
using Wpf.Ui.Common;
using MenuItem = Wpf.Ui.Controls.MenuItem;

namespace LenovoLegionToolkit.WPF.Pages;

/// <summary>
/// “自动化”页面：管理自动与手动两类自动化流水线，支持新建、重命名、换图标、排序、删除与保存。
/// </summary>
public partial class AutomationPage
{
    /// <summary>是否启用混合模式自动化步骤；混合模式切换通常需要重启，默认关闭。</summary>
    public static bool EnableHybridModeAutomation;

    /// <summary>自动化处理器，负责读写与执行自动化流水线。</summary>
    private readonly AutomationProcessor _automationProcessor = IoCContainer.Resolve<AutomationProcessor>();

    /// <summary>本机支持的自动化步骤缓存，首次刷新时计算，避免重复探测。</summary>
    private IAutomationStep[] _supportedAutomationSteps = [];

    /// <summary>
    /// 初始化页面：先订阅初始化事件，再加载 XAML 视图。
    /// </summary>
    public AutomationPage()
    {
        Initialized += AutomationPage_Initialized;

        InitializeComponent();
    }

    /// <summary>
    /// 页面初始化完成回调：加载全部流水线。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数。</param>
    private async void AutomationPage_Initialized(object? sender, EventArgs e)
    {
        await RefreshAsync();
    }

    /// <summary>
    /// 自动流水线总开关点击：把开关状态提交给自动化处理器。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private async void EnableAutomaticPipelinesToggle_Click(object sender, RoutedEventArgs e)
    {
        var isChecked = _enableAutomaticPipelinesToggle.IsChecked;
        if (isChecked.HasValue)
            await _automationProcessor.SetEnabledAsync(isChecked.Value);
    }

    /// <summary>
    /// “新建自动流水线”按钮点击：收集已被占用的触发器类型，打开创建窗口避免重复添加。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void NewAutomaticPipelineButton_Click(object sender, RoutedEventArgs e)
    {
        // 同一触发器只能存在一条自动流水线，因此需要把已用类型传给创建窗口排除。
        var existingTriggersTypes = _automaticPipelinesStackPanel.Children.ToArray()
            .OfType<AutomationPipelineControl>()
            .Select(c => c.AutomationPipeline.Trigger)
            .Where(t => t is not null)
            .Select(t => t!.GetType())
            .ToHashSet();

        var window = new CreateAutomationPipelineWindow(existingTriggersTypes, AddAutomaticPipeline) { Owner = Window.GetWindow(this) };
        window.ShowDialog();
    }

    /// <summary>
    /// “新建手动流水线”按钮点击：输入名称后创建一条手动流水线。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private async void NewManualPipelineButton_Click(object sender, RoutedEventArgs e)
    {
        await AddManualPipelineAsync();
    }

    /// <summary>
    /// “保存”按钮点击：从界面收集全部流水线并交给处理器重新加载，保存期间禁用按钮。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _saveButton.IsEnabled = false;
            _saveButton.Content = Resource.Saving;

            var automaticPipelines = _automaticPipelinesStackPanel.Children.ToArray()
                .OfType<AutomationPipelineControl>()
                .Select(c => c.CreateAutomationPipeline())
                .ToList();

            var manualPipelines = _manualPipelinesStackPanel.Children.ToArray()
                .OfType<AutomationPipelineControl>()
                .Select(c => c.CreateAutomationPipeline())
                .ToList();

            var pipelines = new List<AutomationPipeline>();
            pipelines.AddRange(automaticPipelines);
            pipelines.AddRange(manualPipelines);

            await _automationProcessor.ReloadPipelinesAsync(pipelines);
            await RefreshAsync();

            await SnackbarHelper.ShowAsync(Resource.AutomationPage_Saved_Title, Resource.AutomationPage_Saved_Message);
        }
        finally
        {
            // 无论成功失败都恢复按钮状态，避免界面卡在“保存中”。
            _saveButton.Content = Resource.Save;
            _saveButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// “还原”按钮点击：丢弃未保存的界面改动，重新从存储加载流水线。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private async void RevertButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();

        await SnackbarHelper.ShowAsync(Resource.AutomationPage_Reverted_Title, Resource.AutomationPage_Reverted_Message);
    }

    /// <summary>
    /// 重新加载页面：分别重建自动（有触发器）与手动（无触发器）流水线控件，并等待全部初始化完成。
    /// </summary>
    /// <returns>异步刷新任务。</returns>
    private async Task RefreshAsync()
    {
        _scrollViewer.ScrollToTop();

        _loaderAutomatic.IsLoading = true;
        _loaderManual.IsLoading = true;

        // 至少展示 1 秒加载动画，避免内容闪现。
        var initializedTasks = new List<Task> { Task.Delay(TimeSpan.FromSeconds(1)) };

        _enableAutomaticPipelinesToggle.IsChecked = _automationProcessor.IsEnabled;

        _automaticPipelinesStackPanel.Children.Clear();
        _manualPipelinesStackPanel.Children.Clear();

        var pipelines = await _automationProcessor.GetPipelinesAsync();

        // 支持性探测有硬件访问开销，仅在本页首次刷新时执行一次。
        if (_supportedAutomationSteps.IsEmpty())
            _supportedAutomationSteps = await GetSupportedAutomationStepsAsync();

        // 有触发器的归入自动流水线。
        foreach (var pipeline in pipelines.Where(p => p.Trigger is not null))
        {
            var control = GenerateControl(pipeline, _automaticPipelinesStackPanel);
            _automaticPipelinesStackPanel.Children.Add(control);
            initializedTasks.Add(control.InitializedTask);
        }

        // 无触发器的归入手动流水线；手动流水线不允许包含“快捷操作”步骤，避免递归执行。
        foreach (var pipeline in pipelines.Where(p => p.Trigger is null))
        {
            var control = GenerateControl(pipeline, _manualPipelinesStackPanel, false);
            _manualPipelinesStackPanel.Children.Add(control);
            initializedTasks.Add(control.InitializedTask);
        }

        _saveRevertStackPanel.Visibility = Visibility.Collapsed;

        _noAutomaticActionsText.Visibility = _automaticPipelinesStackPanel.Children.Count < 1
            ? Visibility.Visible
            : Visibility.Collapsed;
        _noManualActionsText.Visibility = _manualPipelinesStackPanel.Children.Count < 1
            ? Visibility.Visible
            : Visibility.Collapsed;

        await Task.WhenAll(initializedTasks);

        _loaderAutomatic.IsLoading = false;
        _loaderManual.IsLoading = false;
    }

    /// <summary>
    /// 构建全部可用的自动化步骤清单，并剔除当前机型不支持的项目。
    /// </summary>
    /// <returns>本机支持的自动化步骤数组。</returns>
    private static async Task<IAutomationStep[]> GetSupportedAutomationStepsAsync()
    {
        var steps = new List<IAutomationStep>
        {
            new AlwaysOnUsbAutomationStep(default),
            new BatteryAutomationStep(default),
            new BatteryNightChargeAutomationStep(default),
            new DeactivateGPUAutomationStep(default),
            new DelayAutomationStep(default),
            new DisplayBrightnessAutomationStep(50),
            new DpiScaleAutomationStep(default),
            new FlipToStartAutomationStep(default),
            new FnLockAutomationStep(default),
            new GodModePresetAutomationStep(default),
            new HDRAutomationStep(default),
            new InstantBootAutomationStep(default),
            new MacroAutomationStep(default),
            new MicrophoneAutomationStep(default),
            new SpeakerAutomationStep(default),
            new NotificationAutomationStep(default),
            new OneLevelWhiteKeyboardBacklightAutomationStep(default),
            new OverclockDiscreteGPUAutomationStep(default),
            new OverDriveAutomationStep(default),
            new PanelLogoBacklightAutomationStep(default),
            new PlaySoundAutomationStep(default),
            new PortsBacklightAutomationStep(default),
            new PowerModeAutomationStep(default),
            new QuickActionAutomationStep(default),
            new RefreshRateAutomationStep(default),
            new ResolutionAutomationStep(default),
            new RGBKeyboardBacklightAutomationStep(default),
            new RunAutomationStep(default, default, default, default),
            new SpectrumKeyboardBacklightBrightnessAutomationStep(0),
            new SpectrumKeyboardBacklightProfileAutomationStep(1),
            new SpectrumKeyboardBacklightImportProfileAutomationStep(default),
            new TouchpadLockAutomationStep(default),
            new TurnOffMonitorsAutomationStep(),
            new TurnOffWiFiAutomationStep(),
            new TurnOnWiFiAutomationStep(),
            new WhiteKeyboardBacklightAutomationStep(default),
            new WinKeyAutomationStep(default)
        };

        // 混合模式步骤默认不提供，避免用户在无预期的情况下触发需要重启的切换。
        if (EnableHybridModeAutomation)
            steps.Add(new HybridModeAutomationStep(default));

        // 倒序遍历删除不支持项，避免删除时索引前移导致漏检。
        for (var index = steps.Count - 1; index >= 0; index--)
        {
            if (!await steps[index].IsSupportedAsync())
                steps.RemoveAt(index);
        }

        return [.. steps];
    }

    /// <summary>
    /// 为指定流水线创建界面控件，并连接右键菜单、内容变更与删除事件。
    /// </summary>
    /// <param name="pipeline">要呈现的流水线。</param>
    /// <param name="stackPanel">该控件所属的容器面板。</param>
    /// <param name="allowQuickActionAutomationStep">是否允许使用“快捷操作”步骤（手动流水线中禁用）。</param>
    /// <returns>配置完成的流水线控件。</returns>
    private AutomationPipelineControl GenerateControl(AutomationPipeline pipeline, Panel stackPanel, bool allowQuickActionAutomationStep = true)
    {
        var supportedSteps = _supportedAutomationSteps;
        if (!allowQuickActionAutomationStep)
        {
            supportedSteps = Array.FindAll(supportedSteps, s => s is not QuickActionAutomationStep);
        }

        var control = new AutomationPipelineControl(pipeline, supportedSteps);
        control.MouseRightButtonUp += (_, e) =>
        {
            ShowPipelineContextMenu(control, stackPanel);
            e.Handled = true;
        };
        control.OnChanged += (_, _) => PipelinesChanged();
        control.OnDelete += (s, _) =>
        {
            if (s is AutomationPipelineControl c)
                DeletePipeline(c, stackPanel);
        };
        return control;
    }

    /// <summary>
    /// 标记流水线内容已变更，显示“保存/还原”操作栏。
    /// </summary>
    private void PipelinesChanged()
    {
        _saveRevertStackPanel.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// 在指定流水线控件上弹出右键菜单：上移、下移、重命名、（手动流水线的）更换图标与删除。
    /// </summary>
    /// <param name="control">目标流水线控件。</param>
    /// <param name="stackPanel">该控件所在的容器面板。</param>
    private void ShowPipelineContextMenu(AutomationPipelineControl control, Panel stackPanel)
    {
        var menuItems = new List<MenuItem>();

        var index = stackPanel.Children.IndexOf(control);
        var maxIndex = stackPanel.Children.Count - 1;

        var moveUpMenuItem = new MenuItem { SymbolIcon = SymbolRegular.ArrowUp24, Header = Resource.MoveUp };
        // 已在首位时禁用“上移”。
        if (index > 0)
            moveUpMenuItem.Click += (_, _) => MovePipeline(control, stackPanel, index - 1);
        else
            moveUpMenuItem.IsEnabled = false;
        menuItems.Add(moveUpMenuItem);

        var moveDownMenuItem = new MenuItem { SymbolIcon = SymbolRegular.ArrowDown24, Header = Resource.MoveDown };
        // 已在末位时禁用“下移”。
        if (index < maxIndex)
            moveDownMenuItem.Click += (_, _) => MovePipeline(control, stackPanel, index + 1);
        else
            moveDownMenuItem.IsEnabled = false;
        menuItems.Add(moveDownMenuItem);

        var renameMenuItem = new MenuItem { SymbolIcon = SymbolRegular.Edit24, Header = Resource.Rename };
        renameMenuItem.Click += async (_, _) => await RenamePipelineAsync(control);
        menuItems.Add(renameMenuItem);

        // 只有手动流水线（无触发器）支持自定义图标。
        if (control.AutomationPipeline.Trigger is null)
        {
            var changeIconMenuItem = new MenuItem { SymbolIcon = SymbolRegular.Edit24, Header = Resource.AutomationPage_ChangeIcon };
            changeIconMenuItem.Click += async (_, _) => await ChangePipelineIconAsync(control);
            menuItems.Add(changeIconMenuItem);
        }

        var deleteMenuItem = new MenuItem { SymbolIcon = SymbolRegular.Delete24, Header = Resource.Delete };
        deleteMenuItem.Click += (_, _) => DeletePipeline(control, stackPanel);
        menuItems.Add(deleteMenuItem);

        control.ContextMenu = new();
        control.ContextMenu.Items.AddRange(menuItems);
        control.ContextMenu.IsOpen = true;
    }

    /// <summary>
    /// 将流水线控件移动到容器中的新索引位置。
    /// </summary>
    /// <param name="control">要移动的控件。</param>
    /// <param name="stackPanel">所在容器面板。</param>
    /// <param name="index">目标索引。</param>
    private void MovePipeline(UIElement control, Panel stackPanel, int index)
    {
        stackPanel.Children.Remove(control);
        stackPanel.Children.Insert(index, control);

        PipelinesChanged();
    }

    /// <summary>
    /// 添加一条自动流水线（带触发器），新项插入到列表顶部。
    /// </summary>
    /// <param name="trigger">流水线触发器。</param>
    private void AddAutomaticPipeline(IAutomationPipelineTrigger trigger)
    {
        var pipeline = new AutomationPipeline(trigger);
        var control = GenerateControl(pipeline, _automaticPipelinesStackPanel);
        _automaticPipelinesStackPanel.Children.Insert(0, control);

        _noAutomaticActionsText.Visibility = _automaticPipelinesStackPanel.Children.Count < 1
            ? Visibility.Visible
            : Visibility.Collapsed;

        PipelinesChanged();
    }

    /// <summary>
    /// 弹窗输入名称后添加一条手动流水线，新项插入到列表顶部。
    /// </summary>
    /// <returns>异步操作任务。</returns>
    private async Task AddManualPipelineAsync()
    {
        var newName = await MessageBoxHelper.ShowInputAsync(this,
            Resource.AutomationPage_AddManualPipeline_Title,
            Resource.AutomationPage_AddManualPipeline_Placeholder);
        if (string.IsNullOrWhiteSpace(newName))
            return;

        var pipeline = new AutomationPipeline(newName);
        var control = GenerateControl(pipeline, _manualPipelinesStackPanel, false);
        _manualPipelinesStackPanel.Children.Insert(0, control);

        _noManualActionsText.Visibility = _manualPipelinesStackPanel.Children.Count < 1
            ? Visibility.Visible
            : Visibility.Collapsed;

        PipelinesChanged();
    }

    /// <summary>
    /// 弹窗修改流水线名称，允许留空以恢复默认名称。
    /// </summary>
    /// <param name="control">目标流水线控件。</param>
    /// <returns>异步操作任务。</returns>
    private async Task RenamePipelineAsync(AutomationPipelineControl control)
    {
        var name = control.GetName();
        var newName = await MessageBoxHelper.ShowInputAsync(this,
            Resource.AutomationPage_RenamePipeline_Title,
            Resource.AutomationPage_RenamePipeline_Placeholder,
            name,
            allowEmpty: true);

        control.SetName(newName);
    }

    /// <summary>
    /// 打开图标选择窗口并应用所选图标；用户取消时静默忽略。
    /// </summary>
    /// <param name="control">目标流水线控件。</param>
    /// <returns>异步操作任务。</returns>
    private async Task ChangePipelineIconAsync(AutomationPipelineControl control)
    {
        try
        {
            var window = new SymbolRegularPicker { Owner = Window.GetWindow(this) };
            window.ShowDialog();

            var icon = await window.SymbolRegularTask;
            control.SetIcon(icon);
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// 删除流水线控件并同步两处“暂无流水线”提示的可见性。
    /// </summary>
    /// <param name="control">要删除的控件。</param>
    /// <param name="stackPanel">所在容器面板。</param>
    private void DeletePipeline(UIElement control, Panel stackPanel)
    {
        stackPanel.Children.Remove(control);

        _noAutomaticActionsText.Visibility = _automaticPipelinesStackPanel.Children.Count < 1
            ? Visibility.Visible
            : Visibility.Collapsed;
        _noManualActionsText.Visibility = _manualPipelinesStackPanel.Children.Count < 1
            ? Visibility.Visible
            : Visibility.Collapsed;

        PipelinesChanged();
    }
}

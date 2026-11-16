using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Humanizer;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation;
using LenovoLegionToolkit.Lib.Automation.Pipeline;
using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Controls.Automation.Steps;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;
using LenovoLegionToolkit.WPF.Windows.Automation;
using Wpf.Ui.Common;
using Button = Wpf.Ui.Controls.Button;
using CardExpander = LenovoLegionToolkit.WPF.Controls.Custom.CardExpander;
using MenuItem = Wpf.Ui.Controls.MenuItem;

namespace LenovoLegionToolkit.WPF.Controls.Automation;

/// <summary>
/// 单条自动化管线的编辑控件：以可展开卡片展示触发器信息与步骤列表，支持添加、删除、排序步骤以及立即运行。
/// </summary>
public class AutomationPipelineControl : UserControl
{
    // 用于向外暴露"界面初始化完成"的可等待信号，避免调用方在控件构建完成前访问内部元素。
    private readonly TaskCompletionSource _initializedTaskCompletionSource = new();

    // 自动化处理器，负责实际执行管线。
    private readonly AutomationProcessor _automationProcessor = IoCContainer.Resolve<AutomationProcessor>();

    // God Mode 预设设置，用于把预设 ID 解析为可读名称显示在副标题中。
    private readonly GodModeSettings _godModeSettings = IoCContainer.Resolve<GodModeSettings>();

    // 管线卡片外框（可展开/折叠）。
    private readonly CardExpander _cardExpander = new()
    {
        Margin = new(0, 0, 0, 8),
    };

    // 卡片头部（标题、副标题与右侧附件按钮）。
    private readonly CardHeaderControl _cardHeaderControl = new();

    // 卡片内容根容器：包含步骤列表与底部按钮栏。
    private readonly StackPanel _stackPanel = new();

    // 步骤控件列表容器，顺序即为执行顺序。
    private readonly StackPanel _stepsStackPanel = new();

    // 底部按钮栏：独占开关、立即运行、添加步骤、删除管线。
    private readonly Grid _buttonsStackPanel = new()
    {
        Margin = new(0, 16, 0, 0),
        ColumnDefinitions =
        {
            new() { Width = new(1, GridUnitType.Star) },
            new() { Width = GridLength.Auto },
            new() { Width = GridLength.Auto },
            new() { Width = GridLength.Auto },
        }
    };

    // 独占模式开关：开启后该管线运行时其他管线不会同时运行。
    private readonly CheckBox _isExclusiveCheckBox = new()
    {
        HorizontalAlignment = HorizontalAlignment.Left,
        Content = Resource.AutomationPipelineControl_Exclusive,
        ToolTip = Resource.AutomationPipelineControl_Exclusive_ToolTip,
        MinWidth = 100,
        Margin = new(0, 0, 8, 0),
    };

    // 立即运行按钮：手动执行一次当前管线。
    private readonly Button _runNowButton = new()
    {
        Content = Resource.AutomationPipelineControl_RunNow,
        MinWidth = 100,
        Margin = new(0, 0, 8, 0),
    };

    // 添加步骤按钮：打开步骤选择窗口。
    private readonly Button _addStepButton = new()
    {
        Content = Resource.AutomationPipelineControl_AddStep,
        MinWidth = 100,
        Margin = new(0, 0, 8, 0),
    };

    // 删除整条管线的按钮。
    private readonly Button _deletePipelineButton = new()
    {
        Content = Resource.Delete,
        MinWidth = 100,
    };

    // 当前机型允许添加的步骤类型集合，供添加步骤窗口使用。
    private readonly IAutomationStep[] _supportedAutomationSteps;

    /// <summary>
    /// 当前控件所编辑的自动化管线数据对象。
    /// </summary>
    public AutomationPipeline AutomationPipeline { get; }

    /// <summary>
    /// 管线内容发生变化（名称、图标、步骤等）时触发。
    /// </summary>
    public event EventHandler? OnChanged;

    /// <summary>
    /// 用户请求删除整条管线时触发。
    /// </summary>
    public event EventHandler? OnDelete;

    /// <summary>
    /// 表示界面已完成初始化的任务，等待该任务即可安全访问内部元素。
    /// </summary>
    public Task InitializedTask => _initializedTaskCompletionSource.Task;

    /// <summary>
    /// 初始化管线控件。
    /// </summary>
    /// <param name="automationPipeline">要编辑的管线数据。</param>
    /// <param name="supportedAutomationSteps">允许添加的步骤类型集合。</param>
    public AutomationPipelineControl(AutomationPipeline automationPipeline, IAutomationStep[] supportedAutomationSteps)
    {
        AutomationPipeline = automationPipeline;
        _supportedAutomationSteps = supportedAutomationSteps;

        Initialized += AutomationPipelineControl_Initialized;
    }

    /// <summary>
    /// 根据界面当前状态（含步骤顺序与独占开关）重新构造管线数据对象，用于序列化回写。
    /// </summary>
    /// <returns>新的管线数据对象。</returns>
    public AutomationPipeline CreateAutomationPipeline() => new()
    {
        Id = AutomationPipeline.Id,
        IconName = AutomationPipeline.IconName,
        Name = AutomationPipeline.Name,
        Trigger = AutomationPipeline.Trigger,
        Steps = _stepsStackPanel.Children.ToArray()
            .OfType<AbstractAutomationStepControl>()
            .Select(s => s.CreateAutomationStep())
            .ToList(),
        IsExclusive = _isExclusiveCheckBox.IsChecked ?? false,
    };

    /// <summary>
    /// 获取管线名称。
    /// </summary>
    /// <returns>管线名称，可能为 null。</returns>
    public string? GetName() => AutomationPipeline.Name;

    /// <summary>
    /// 设置管线名称并刷新卡片标题与副标题。
    /// </summary>
    /// <param name="name">新名称。</param>
    public void SetName(string? name)
    {
        AutomationPipeline.Name = name;
        _cardHeaderControl.Title = GenerateHeader();
        _cardHeaderControl.Subtitle = GenerateSubtitle();
        _cardHeaderControl.SubtitleToolTip = _cardHeaderControl.Subtitle;

        OnChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 设置管线图标，并以枚举名形式持久化。
    /// </summary>
    /// <param name="icon">图标枚举，null 表示清除自定义图标。</param>
    public void SetIcon(SymbolRegular? icon)
    {
        AutomationPipeline.IconName = icon.HasValue ? Enum.GetName(icon.Value) : null;
        _cardExpander.Icon = GenerateIcon();

        OnChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 界面初始化：生成现有步骤控件、装配按钮栏与卡片结构，最后放出初始化完成信号。
    /// </summary>
    private async void AutomationPipelineControl_Initialized(object? sender, EventArgs e)
    {
        _cardExpander.Header = _cardHeaderControl;

        foreach (var step in AutomationPipeline.Steps)
        {
            var control = await GenerateStepControlAsync(step);
            _stepsStackPanel.Children.Add(control);
        }

        if (AutomationPipeline.Trigger is not null)
        {
            _isExclusiveCheckBox.IsChecked = AutomationPipeline.IsExclusive;
            _isExclusiveCheckBox.Checked += (_, _) => OnChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            // 无触发器的管线属于手动/快捷操作，独占模式无意义，直接隐藏该选项。
            _isExclusiveCheckBox.Visibility = Visibility.Hidden;
        }

        _runNowButton.Click += async (_, _) => await RunAsync();

        _addStepButton.Click += async (_, _) =>
        {
            var stepControls = new List<AbstractAutomationStepControl>();
            foreach (var step in _supportedAutomationSteps)
                stepControls.Add(await GenerateStepControlAsync(step));

            var window = new AddAutomationStepWindow(stepControls, AddStep) { Owner = Window.GetWindow(this) };
            window.ShowDialog();
        };

        _deletePipelineButton.Click += (_, _) => OnDelete?.Invoke(this, EventArgs.Empty);

        Grid.SetColumn(_isExclusiveCheckBox, 0);
        Grid.SetColumn(_runNowButton, 1);
        Grid.SetColumn(_addStepButton, 2);
        Grid.SetColumn(_deletePipelineButton, 3);

        _buttonsStackPanel.Children.Add(_isExclusiveCheckBox);
        _buttonsStackPanel.Children.Add(_runNowButton);
        _buttonsStackPanel.Children.Add(_addStepButton);
        _buttonsStackPanel.Children.Add(_deletePipelineButton);

        _stackPanel.Children.Add(_stepsStackPanel);
        _stackPanel.Children.Add(_buttonsStackPanel);

        _cardExpander.Icon = GenerateIcon();
        _cardHeaderControl.Title = GenerateHeader();
        _cardHeaderControl.Subtitle = GenerateSubtitle();
        _cardHeaderControl.Accessory = GenerateAccessory();
        _cardHeaderControl.SubtitleToolTip = _cardHeaderControl.Subtitle;
        _cardExpander.Content = _stackPanel;

        Content = _cardExpander;

        _initializedTaskCompletionSource.TrySetResult();
    }

    /// <summary>
    /// 立即执行一次当前管线，执行期间禁用按钮并给出成功/失败提示。
    /// </summary>
    private async Task RunAsync()
    {
        try
        {
            _runNowButton.IsEnabled = false;
            _runNowButton.Content = Resource.AutomationPipelineControl_Running;
            var pipeline = CreateAutomationPipeline();
            await _automationProcessor.RunNowAsync(pipeline);

            _runNowButton.Content = Resource.AutomationPipelineControl_RunNow;
            _runNowButton.IsEnabled = true;

            await SnackbarHelper.ShowAsync(Resource.AutomationPipelineControl_RunNow_Success_Title, Resource.AutomationPipelineControl_RunNow_Success_Message);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Run now completed with errors", ex);

            _runNowButton.Content = Resource.AutomationPipelineControl_RunNow;
            _runNowButton.IsEnabled = true;

            await SnackbarHelper.ShowAsync(Resource.AutomationPipelineControl_RunNow_Error_Title, Resource.AutomationPipelineControl_RunNow_Error_Message);
        }
    }

    /// <summary>
    /// 生成卡片图标：带触发器的管线使用流程图标，否则使用自定义图标或默认播放图标。
    /// </summary>
    private SymbolRegular GenerateIcon()
    {
        if (AutomationPipeline.Trigger is not null)
            return SymbolRegular.Flow20;

        return Enum.TryParse<SymbolRegular>(AutomationPipeline.IconName, out var icon) ? icon : SymbolRegular.Play24;
    }

    /// <summary>
    /// 生成卡片标题：优先使用自定义名称，其次是触发器显示名，最后回退为"未命名"。
    /// </summary>
    private string GenerateHeader()
    {
        if (!string.IsNullOrWhiteSpace(AutomationPipeline.Name))
            return AutomationPipeline.Name;

        if (AutomationPipeline.Trigger is not null)
            return AutomationPipeline.Trigger.DisplayName;

        return Resource.AutomationPipelineControl_Unnamed;
    }

    /// <summary>
    /// 生成卡片副标题：拼接步骤数量以及触发器的电源模式、预设、进程、时间、空闲、WiFi、周期、设备等附加信息。
    /// </summary>
    private string GenerateSubtitle()
    {
        var stepsCount = _stepsStackPanel.Children.ToArray()
            .OfType<AbstractAutomationStepControl>()
            .Count();

        var result = string.Format(stepsCount == 1 ? Resource.AutomationPipelineControl_Step : Resource.AutomationPipelineControl_Step_Many, stepsCount);

        if (!string.IsNullOrWhiteSpace(AutomationPipeline.Name) && AutomationPipeline.Trigger is not null)
            result += $" | {AutomationPipeline.Trigger.DisplayName}";

        if (AutomationPipeline.Trigger is IPowerModeAutomationPipelineTrigger pm)
            result += $" | {Resource.AutomationPipelineControl_SubtitlePart_PowerMode}: {pm.PowerModeState.GetDisplayName()}";

        if (AutomationPipeline.Trigger is IGodModePresetChangedAutomationPipelineTrigger gpt)
        {
            var name = _godModeSettings.Store.Presets.Where(kv => kv.Key == gpt.PresetId)
                .Select(kv => kv.Value.Name)
                .DefaultIfEmpty("-")
                .First();
            result += $" | {Resource.AutomationPipelineControl_SubtitlePart_Preset}: {name}";
        }

        if (AutomationPipeline.Trigger is IProcessesAutomationPipelineTrigger pt && pt.Processes.Length != 0)
            result += $" | {Resource.AutomationPipelineControl_SubtitlePart_Apps}: {string.Join(", ", pt.Processes.Select(p => p.Name))}";

        if (AutomationPipeline.Trigger is ITimeAutomationPipelineTrigger tt)
        {
            if (tt.IsSunrise)
                result += $" | {Resource.AutomationPipelineControl_SubtitlePart_AtSunrise}";
            if (tt.IsSunset)
                result += $" | {Resource.AutomationPipelineControl_SubtitlePart_AtSunset}";
            if (tt.Time is not null)
            {
                var local = DateTimeExtensions.UtcFrom(tt.Time.Value.Hour, tt.Time.Value.Minute).ToLocalTime();
                if (tt.Days.IsEmpty() || tt.Days.OrderBy(x => x).SequenceEqual(Enum.GetValues<DayOfWeek>()))
                {
                    result += $" | {string.Format(Resource.AutomationPipelineControl_SubtitlePart_AtTime, local.Hour, local.Minute)}";
                }
                else
                {
                    var localizedDayStrings = tt.Days.Select(day => Resource.Culture.DateTimeFormat.GetDayName(day));
                    result += $" | {string.Join(", ", localizedDayStrings)} {string.Format(Resource.AutomationPipelineControl_SubtitlePart_AtTime.ToLower(Resource.Culture), local.Hour, local.Minute)}";
                }
            }
        }

        if (AutomationPipeline.Trigger is IUserInactivityPipelineTrigger ut && ut.InactivityTimeSpan > TimeSpan.Zero)
            result += $" | {string.Format(Resource.AutomationPipelineControl_SubtitlePart_After, ut.InactivityTimeSpan.Humanize(culture: Resource.Culture))}";

        if (AutomationPipeline.Trigger is IWiFiConnectedPipelineTrigger wt && wt.Ssids.Length != 0)
            result += $" | {string.Join(",", wt.Ssids)}";

        if (AutomationPipeline.Trigger is IPeriodicAutomationPipelineTrigger pet)
            result += $" | {Resource.PeriodicActionPipelineTriggerTabItemContent_PeriodMinutes}: {pet.Period.TotalMinutes}";

        if (AutomationPipeline.Trigger is IDeviceAutomationPipelineTrigger dt && dt.InstanceIds.Length != 0)
            result += $" | {Resource.DevicePipelineTriggerTabItemContent_Devices}: {dt.InstanceIds.Length}";

        return result;
    }

    /// <summary>
    /// 生成卡片右侧的"配置"按钮；若当前触发器组合不合法则返回 null 隐藏按钮。
    /// </summary>
    /// <returns>配置按钮，或 null。</returns>
    private Button? GenerateAccessory()
    {
        var triggers = AutomationPipeline.AllTriggers
            .ToArray();

        if (!AutomationPipelineTriggerConfigurationWindow.IsValid(triggers))
            return null;

        var button = new Button
        {
            Content = Resource.AutomationPipelineControl_Configure,
            Margin = new(16, 0, 16, 0),
            MinWidth = 120,
        };
        button.Click += (_, _) =>
        {
            var window = new AutomationPipelineTriggerConfigurationWindow(triggers) { Owner = Window.GetWindow(this) };
            window.OnSave += (_, e) =>
            {
                AutomationPipeline.Trigger = e;
                _cardHeaderControl.Subtitle = GenerateSubtitle();
                _cardHeaderControl.Accessory = GenerateAccessory();
                _cardHeaderControl.SubtitleToolTip = _cardHeaderControl.Subtitle;
                OnChanged?.Invoke(this, EventArgs.Empty);
            };
            window.ShowDialog();
        };
        return button;
    }

    /// <summary>
    /// 根据步骤类型创建对应的编辑器控件，并挂接右键菜单、内容变更与删除事件。
    /// </summary>
    /// <param name="automationStep">要编辑的步骤。</param>
    /// <returns>对应的步骤编辑控件。</returns>
    private async Task<AbstractAutomationStepControl> GenerateStepControlAsync(IAutomationStep automationStep)
    {
        AbstractAutomationStepControl control = automationStep switch
        {
            AlwaysOnUsbAutomationStep s => new AlwaysOnUsbAutomationStepControl(s),
            BatteryAutomationStep s => new BatteryAutomationStepControl(s),
            BatteryNightChargeAutomationStep s => new BatteryNightChargeAutomationStepControl(s),
            DeactivateGPUAutomationStep s => new DeactivateGPUAutomationStepControl(s),
            DelayAutomationStep s => new DelayAutomationStepControl(s),
            DisplayBrightnessAutomationStep s => new DisplayBrightnessAutomationStepControl(s),
            DpiScaleAutomationStep s => new DpiScaleAutomationStepControl(s),
            FlipToStartAutomationStep s => new FlipToStartAutomationStepControl(s),
            FnLockAutomationStep s => new FnLockAutomationStepControl(s),
            GodModePresetAutomationStep s => new GodModePresetAutomationStepControl(s),
            HDRAutomationStep s => new HDRAutomationStepControl(s),
            HybridModeAutomationStep s => await HybridModeAutomationStepControlFactory.GetControlAsync(s),
            InstantBootAutomationStep s => new InstantBootAutomationStepControl(s),
            MacroAutomationStep s => new MacroAutomationStepControl(s),
            MicrophoneAutomationStep s => new MicrophoneAutomationStepControl(s),
            NotificationAutomationStep s => new NotificationAutomationStepControl(s),
            OneLevelWhiteKeyboardBacklightAutomationStep s => new OneLevelWhiteKeyboardBacklightAutomationStepControl(s),
            OverDriveAutomationStep s => new OverDriveAutomationStepControl(s),
            OverclockDiscreteGPUAutomationStep s => new OverclockDiscreteGPUAutomationStepControl(s),
            PanelLogoBacklightAutomationStep s => new PanelLogoBacklightAutomationStepControl(s),
            PlaySoundAutomationStep s => new PlaySoundAutomationStepControl(s),
            PortsBacklightAutomationStep s => new PortsBacklightAutomationStepControl(s),
            PowerModeAutomationStep s => new PowerModeAutomationStepControl(s),
            QuickActionAutomationStep s => new QuickActionAutomationStepControl(s),
            RefreshRateAutomationStep s => new RefreshRateAutomationStepControl(s),
            ResolutionAutomationStep s => new ResolutionAutomationStepControl(s),
            RGBKeyboardBacklightAutomationStep s => new RGBKeyboardBacklightAutomationStepControl(s),
            RunAutomationStep s => new RunAutomationStepControl(s),
            SpeakerAutomationStep s => new SpeakerAutomationStepControl(s),
            SpectrumKeyboardBacklightBrightnessAutomationStep s => new SpectrumKeyboardBacklightBrightnessAutomationStepControl(s),
            SpectrumKeyboardBacklightImportProfileAutomationStep s => new SpectrumKeyboardBacklightImportProfileAutomationStepControl(s),
            SpectrumKeyboardBacklightProfileAutomationStep s => new SpectrumKeyboardBacklightProfileAutomationStepControl(s),
            TurnOffMonitorsAutomationStep s => new TurnOffMonitorsAutomationStepControl(s),
            TurnOffWiFiAutomationStep s => new TurnOffWiFiAutomationStepControl(s),
            TurnOnWiFiAutomationStep s => new TurnOnWiFiAutomationStepControl(s),
            TouchpadLockAutomationStep s => new TouchpadLockAutomationStepControl(s),
            WhiteKeyboardBacklightAutomationStep s => new WhiteKeyboardBacklightAutomationStepControl(s),
            WinKeyAutomationStep s => new WinKeyAutomationStepControl(s),
            _ => throw new InvalidOperationException("Unknown step type"),
        };
        control.MouseRightButtonUp += (_, e) =>
        {
            ShowContextMenu(control);
            e.Handled = true;
        };
        control.Changed += (_, _) =>
        {
            OnChanged?.Invoke(this, EventArgs.Empty);
        };
        control.Delete += (s, _) =>
        {
            if (s is AbstractAutomationStepControl step)
                DeleteStep(step);
        };
        return control;
    }

    /// <summary>
    /// 在步骤控件上弹出"上移/下移"右键菜单，并根据当前位置决定菜单项是否可用。
    /// </summary>
    private void ShowContextMenu(FrameworkElement control)
    {
        var menuItems = new List<MenuItem>();

        var index = _stepsStackPanel.Children.IndexOf(control);
        var maxIndex = _stepsStackPanel.Children.Count - 1;

        var moveUpMenuItem = new MenuItem
        {
            SymbolIcon = SymbolRegular.ArrowUp24,
            Header = Resource.MoveUp
        };
        if (index > 0)
            moveUpMenuItem.Click += (_, _) => MoveStep(control, index - 1);
        else
            moveUpMenuItem.IsEnabled = false;
        menuItems.Add(moveUpMenuItem);

        var moveDownMenuItem = new MenuItem
        {
            SymbolIcon = SymbolRegular.ArrowDown24,
            Header = Resource.MoveDown
        };
        if (index < maxIndex)
            moveDownMenuItem.Click += (_, _) => MoveStep(control, index + 1);
        else
            moveDownMenuItem.IsEnabled = false;

        menuItems.Add(moveDownMenuItem);

        control.ContextMenu = new();
        control.ContextMenu.Items.AddRange(menuItems);
        control.ContextMenu.IsOpen = true;
    }

    /// <summary>
    /// 将步骤控件移动到指定索引位置，并通知管线内容已变化。
    /// </summary>
    private void MoveStep(UIElement control, int index)
    {
        _stepsStackPanel.Children.Remove(control);
        _stepsStackPanel.Children.Insert(index, control);

        OnChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 追加新步骤，刷新副标题中的步骤数量并通知变更。
    /// </summary>
    private void AddStep(AbstractAutomationStepControl control)
    {
        _stepsStackPanel.Children.Add(control);
        _cardHeaderControl.Subtitle = GenerateSubtitle();
        _cardHeaderControl.SubtitleToolTip = _cardHeaderControl.Subtitle;

        control.Focus();

        OnChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 移除指定步骤并刷新副标题中的步骤数量。
    /// </summary>
    private void DeleteStep(UIElement control)
    {
        _stepsStackPanel.Children.Remove(control);
        _cardHeaderControl.Subtitle = GenerateSubtitle();
        _cardHeaderControl.SubtitleToolTip = _cardHeaderControl.Subtitle;

        OnChanged?.Invoke(this, EventArgs.Empty);
    }
}

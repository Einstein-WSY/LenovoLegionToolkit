using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Windows.Automation.TabItemContent;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Windows.Automation;

/// <summary>
/// 自动化管道触发器配置窗口，以选项卡形式编辑每种触发器，保存时组合为单个或"与"关系的触发器。
/// </summary>
public partial class AutomationPipelineTriggerConfigurationWindow
{
    private readonly IEnumerable<IAutomationPipelineTrigger> _triggers;

    /// <summary>用户点击保存并确认后触发，参数为最终合成的触发器。</summary>
    public event EventHandler<IAutomationPipelineTrigger>? OnSave;

    /// <summary>
    /// 初始化窗口。
    /// </summary>
    /// <param name="triggers">需要编辑的触发器集合。</param>
    public AutomationPipelineTriggerConfigurationWindow(IEnumerable<IAutomationPipelineTrigger> triggers)
    {
        _triggers = triggers;

        InitializeComponent();
    }

    /// <summary>
    /// 初始化时为每个触发器创建选项卡；不支持的触发器以折叠的占位选项卡保留，以便保存时原样带回。
    /// </summary>
    private void AutomationPipelineTriggerConfigurationWindow_Initialized(object? sender, EventArgs e)
    {
        foreach (var trigger in _triggers)
        {
            var content = Create(trigger);
            if (content is not null)
            {
                var header = new StackPanel { Orientation = Orientation.Horizontal };
                header.Children.Add(new SymbolIcon { Symbol = trigger.Icon(), Margin = new(8, 0, 0, 0) });
                header.Children.Add(new TextBlock { Text = trigger.DisplayName, Margin = new(4, 0, 8, 0) });

                var tabItem = new TabItem
                {
                    Header = header,
                    Content = content
                };
                AutomationProperties.SetName(tabItem, trigger.DisplayName);
                _tabControl.Items.Add(tabItem);
            }
            else
            {
                _tabControl.Items.Add(new TabItem
                {
                    Visibility = Visibility.Collapsed,
                    Tag = trigger
                });
            }
        }

        // 多个选项卡时默认选中第一个可编辑的选项卡
        if (_tabControl.Items.Count < 2)
            return;

        _tabControl.SelectedIndex = (_tabControl.Items[0] as TabItem)?.Content is null ? 1 : 0;
    }

    /// <summary>
    /// 收集所有选项卡的触发器，多个时合并为"与"触发器并触发保存事件。
    /// </summary>
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var triggers = _tabControl.Items
            .OfType<TabItem>()
            .Select(c =>
            {
                if (c.Content is IAutomationPipelineTriggerTabItemContent<IAutomationPipelineTrigger> content)
                    return content.GetTrigger();

                if (c.Tag is IAutomationPipelineTrigger trigger)
                    return trigger;

                return null;
            })
            .OfType<IAutomationPipelineTrigger>()
            .ToArray();

        var result = triggers.Length > 1
            ? new AndAutomationPipelineTrigger(triggers)
            : triggers.FirstOrDefault();

        if (result is not null)
            OnSave?.Invoke(this, result);

        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 判断触发器集合中是否至少存在一个配置有效的触发器。
    /// </summary>
    /// <param name="triggers">待校验的触发器集合。</param>
    /// <returns>存在有效触发器时返回 <see langword="true"/>。</returns>
    public static bool IsValid(IEnumerable<IAutomationPipelineTrigger> triggers) => triggers.Any(IsValid);

    /// <summary>
    /// 按触发器类型校验其配置是否有效（如周期、闲置时长必须大于零）。
    /// </summary>
    private static bool IsValid(IAutomationPipelineTrigger trigger) => trigger switch
    {
        IDeviceAutomationPipelineTrigger => true,
        IGodModePresetChangedAutomationPipelineTrigger => true,
        IPeriodicAutomationPipelineTrigger t1 when t1.Period > TimeSpan.Zero => true,
        IProcessesAutomationPipelineTrigger => true,
        IPowerModeAutomationPipelineTrigger => true,
        ITimeAutomationPipelineTrigger => true,
        IUserInactivityPipelineTrigger t2 when t2.InactivityTimeSpan > TimeSpan.Zero => true,
        IWiFiConnectedPipelineTrigger => true,
        _ => false
    };

    /// <summary>
    /// 根据触发器类型创建对应的选项卡内容控件。
    /// </summary>
    /// <param name="trigger">触发器实例。</param>
    /// <returns>对应的选项卡内容；不支持的类型返回 <see langword="null"/>。</returns>
    private static IAutomationPipelineTriggerTabItemContent<IAutomationPipelineTrigger>? Create(IAutomationPipelineTrigger trigger) => trigger switch
    {
        IDeviceAutomationPipelineTrigger dt => new DeviceAutomationPipelineTriggerTabItemContent(dt),
        IGodModePresetChangedAutomationPipelineTrigger gpt => new GodModePresetPipelineTriggerTabItemContent(gpt),
        IPeriodicAutomationPipelineTrigger pet => new PeriodicAutomationPipelineTriggerTabItemContent(pet),
        IProcessesAutomationPipelineTrigger pt => new ProcessAutomationPipelineTriggerTabItemControl(pt),
        IPowerModeAutomationPipelineTrigger pmt => new PowerModeAutomationPipelineTriggerTabItemContent(pmt),
        ITimeAutomationPipelineTrigger tt => new TimeAutomationPipelineTriggerTabItemContent(tt),
        IUserInactivityPipelineTrigger ut when ut.InactivityTimeSpan > TimeSpan.Zero => new UserInactivityPipelineTriggerTabItemContent(ut),
        IWiFiConnectedPipelineTrigger wt => new WiFiConnectedPipelineTriggerTabItemContent(wt),
        _ => null
    };
}

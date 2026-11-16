using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;
using LenovoLegionToolkit.Lib.Automation.Resources;
using LenovoLegionToolkit.Lib.Extensions;

namespace LenovoLegionToolkit.WPF.Windows.Automation.TabItemContent;

/// <summary>
/// 时间触发器的配置选项卡，支持日出、日落或指定时刻，并可选择生效的星期。
/// </summary>
public partial class TimeAutomationPipelineTriggerTabItemContent : IAutomationPipelineTriggerTabItemContent<ITimeAutomationPipelineTrigger>
{
    private readonly ITimeAutomationPipelineTrigger _trigger;
    private readonly bool _isSunrise;
    private readonly bool _isSunset;
    private readonly Time? _time;
    private readonly DayOfWeek[] _days;

    /// <summary>
    /// 初始化选项卡内容，并复制触发器的日出/日落、时间与星期配置。
    /// </summary>
    /// <param name="trigger">待编辑的时间触发器。</param>
    public TimeAutomationPipelineTriggerTabItemContent(ITimeAutomationPipelineTrigger trigger)
    {
        _trigger = trigger;
        _isSunrise = trigger.IsSunrise;
        _isSunset = trigger.IsSunset;
        _time = trigger.Time;
        _days = trigger.Days;

        InitializeComponent();
        UpdateCheckBoxes();
    }

    /// <summary>
    /// 按当前区域文化本地化星期复选框的文本。
    /// </summary>
    private void UpdateCheckBoxes()
    {
        foreach (var checkBox in _daysOfWeekPanel.Children.OfType<CheckBox>())
            checkBox.Content = Resource.Culture.DateTimeFormat.GetDayName((DayOfWeek)checkBox.Tag);
    }

    /// <summary>
    /// 将已保存的配置回填到单选按钮、时间选择器与星期复选框。
    /// </summary>
    private void TimeAutomationPipelineTriggerTabItemContent_Initialized(object? sender, EventArgs e)
    {
        _sunriseRadioButton.IsChecked = _isSunrise;
        _sunsetRadioButton.IsChecked = _isSunset;

        // 存储的时间为 UTC，展示前需转换为本地时间
        var local = _time is not null
            ? DateTimeExtensions.UtcFrom(_time.Value.Hour, _time.Value.Minute).ToLocalTime()
            : DateTime.Now;

        _timePickerHours.Value = local.Hour;
        _timePickerMinutes.Value = local.Minute;

        _timeRadioButton.IsChecked = _time is not null;
        _timePickerPanel.IsEnabled = _time is not null;

        var daysOfWeek = _days.Length != 0 ? _days : Enum.GetValues<DayOfWeek>();
        foreach (var daysOfWeekCheckbox in _daysOfWeekPanel.Children.OfType<CheckBox>())
        {
            if (daysOfWeek.Contains((DayOfWeek)daysOfWeekCheckbox.Tag))
                daysOfWeekCheckbox.IsChecked = true;
        }
    }

    /// <summary>
    /// 读取界面上的日出/日落、时间与星期设置并生成触发器副本。
    /// </summary>
    /// <returns>更新后的时间触发器。</returns>
    public ITimeAutomationPipelineTrigger GetTrigger()
    {
        var isSunrise = _sunriseRadioButton.IsChecked ?? false;
        var isSunset = _sunsetRadioButton.IsChecked ?? false;
        var time = GetSelectedTime();
        var days = GetSelectedDays();

        return _trigger.DeepCopy(isSunrise, isSunset, time, days);
    }

    /// <summary>
    /// 获取用户选择的时刻；未启用时间模式时返回 <see langword="null"/>。
    /// </summary>
    /// <returns>转换为 UTC 的时间，或 <see langword="null"/>。</returns>
    private Time? GetSelectedTime()
    {
        if (!_timePickerPanel.IsEnabled)
            return null;

        var pickedHour = (int?)_timePickerHours.Value ?? 0;
        var pickedMinute = (int?)_timePickerMinutes.Value ?? 0;

        // 用户输入的是本地时间，持久化时统一换算为 UTC
        var utc = DateTimeExtensions.LocalFrom(pickedHour, pickedMinute).ToUniversalTime();
        return new Time(utc.Hour, utc.Minute);
    }

    /// <summary>
    /// 获取选中的星期集合；未勾选任何一天时视为每天生效。
    /// </summary>
    /// <returns>生效的星期数组。</returns>
    private DayOfWeek[] GetSelectedDays()
    {
        var days = _daysOfWeekPanel.Children
            .OfType<CheckBox>()
            .Where(c => c.IsChecked == true)
            .Select(c => c.Tag)
            .Cast<DayOfWeek>()
            .ToArray();

        if (days.IsEmpty())
            days = Enum.GetValues<DayOfWeek>();

        return days;
    }

    /// <summary>
    /// 切换触发方式时同步启用或禁用时间选择面板。
    /// </summary>
    private void RadioButton_Click(object sender, RoutedEventArgs e)
    {
        _timePickerPanel.IsEnabled = _timeRadioButton.IsChecked ?? false;
    }

    /// <summary>
    /// 阻止取消勾选最后一天，保证至少存在一个生效星期。
    /// </summary>
    private void DayOfWeekCheckBox_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox)
            return;

        var anySelected = _daysOfWeekPanel.Children
            .OfType<CheckBox>()
            .Any(c => c.IsChecked == true);

        if (anySelected)
            return;

        checkBox.IsChecked = true;
    }
}

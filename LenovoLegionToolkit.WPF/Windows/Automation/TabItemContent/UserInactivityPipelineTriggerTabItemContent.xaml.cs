using System;
using Humanizer;
using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;
using LenovoLegionToolkit.WPF.Extensions;

namespace LenovoLegionToolkit.WPF.Windows.Automation.TabItemContent;

/// <summary>
/// 用户无操作触发器的配置选项卡，以下拉框形式选择闲置超时时间。
/// </summary>
public partial class UserInactivityPipelineTriggerTabItemContent : IAutomationPipelineTriggerTabItemContent<IUserInactivityPipelineTrigger>
{
    // 提供固定档位的闲置时长选项，覆盖 10 秒到 30 分钟
    private static readonly TimeSpan[] TimeSpans =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(3),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30)
    ];

    private readonly IUserInactivityPipelineTrigger _trigger;

    /// <summary>
    /// 初始化选项卡内容，并用可读文本填充超时时间下拉框。
    /// </summary>
    /// <param name="trigger">待编辑的触发器。</param>
    public UserInactivityPipelineTriggerTabItemContent(IUserInactivityPipelineTrigger trigger)
    {
        _trigger = trigger;
        InitializeComponent();

        // Humanize 将时长转换为"10 分钟"之类的本地化可读文本
        _timeoutComboBox.SetItems(TimeSpans, trigger.InactivityTimeSpan, t => t.Humanize());
    }

    /// <summary>
    /// 读取选中的闲置时长并生成触发器副本；未选中时回退为 30 秒。
    /// </summary>
    /// <returns>更新后的触发器。</returns>
    public IUserInactivityPipelineTrigger GetTrigger()
    {
        var state = _timeoutComboBox.TryGetSelectedItem(out TimeSpan tt)
            ? tt
            : TimeSpan.FromSeconds(30);
        return _trigger.DeepCopy(state);
    }
}

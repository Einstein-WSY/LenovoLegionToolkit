using System;
using System.Collections.Generic;
using System.Linq;
using LenovoLegionToolkit.Lib.Macro;

namespace LenovoLegionToolkit.WPF.Controls.Macro;

/// <summary>
/// 聚合多个连续鼠标移动事件的控件：对外仍表现为一个条目，但内部保存全部移动事件。
/// </summary>
public class MultiAbstractMacroEventControl : AbstractMacroEventControl
{
    // 聚合的连续移动事件列表。
    private readonly List<MacroEvent> _macroEvents = [];

    /// <summary>
    /// 返回全部聚合的宏事件。
    /// </summary>
    /// <returns>宏事件集合。</returns>
    public override IEnumerable<MacroEvent> GetEvents() => _macroEvents;

    /// <summary>
    /// 总延迟为所有聚合事件延迟之和。
    /// </summary>
    protected override TimeSpan TotalDelay => _macroEvents
        .Select(me => me.Delay)
        .Aggregate(TimeSpan.Zero, (subtotal, t) => subtotal.Add(t));

    /// <summary>
    /// 追加一个宏事件并刷新展示。
    /// </summary>
    /// <param name="macroEvent">要追加的宏事件。</param>
    public override void Set(MacroEvent macroEvent)
    {
        _macroEvents.Add(macroEvent);

        base.Set(macroEvent);
    }
}

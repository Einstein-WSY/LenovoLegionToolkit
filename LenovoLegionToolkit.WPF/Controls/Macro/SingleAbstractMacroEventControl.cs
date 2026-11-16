using System;
using System.Collections.Generic;
using LenovoLegionToolkit.Lib.Macro;

namespace LenovoLegionToolkit.WPF.Controls.Macro;

/// <summary>
/// 单个宏事件（非连续移动）的展示控件。
/// </summary>
public class SingleAbstractMacroEventControl : AbstractMacroEventControl
{
    // 该控件所代表的单个宏事件。
    private MacroEvent _macroEvent;

    /// <summary>
    /// 返回当前单个宏事件。
    /// </summary>
    /// <returns>仅含一个元素的宏事件集合。</returns>
    public override IEnumerable<MacroEvent> GetEvents() => [_macroEvent];

    /// <summary>
    /// 总延迟即该事件的延迟。
    /// </summary>
    protected override TimeSpan TotalDelay => _macroEvent.Delay;

    /// <summary>
    /// 记录宏事件并刷新展示。
    /// </summary>
    /// <param name="macroEvent">要展示的宏事件。</param>
    public override void Set(MacroEvent macroEvent)
    {
        _macroEvent = macroEvent;

        base.Set(macroEvent);
    }
}

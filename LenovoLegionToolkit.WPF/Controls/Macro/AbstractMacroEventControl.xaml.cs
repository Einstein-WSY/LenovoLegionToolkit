using System;
using System.Collections.Generic;
using System.Windows.Input;
using Humanizer;
using Humanizer.Localisation;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Macro;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Macro;

/// <summary>
/// 单个宏事件展示控件的抽象基类：负责根据事件类型显示方向图标与按键/滚轮说明。
/// </summary>
public abstract partial class AbstractMacroEventControl
{
    /// <summary>
    /// 初始化 XAML 组件。
    /// </summary>
    protected AbstractMacroEventControl()
    {
        InitializeComponent();
    }

    /// <summary>
    /// 获取该控件所代表的全部宏事件（组合控件可能包含多个移动事件）。
    /// </summary>
    /// <returns>宏事件集合。</returns>
    public abstract IEnumerable<MacroEvent> GetEvents();

    /// <summary>
    /// 该控件所包含事件的总延迟时长。
    /// </summary>
    protected abstract TimeSpan TotalDelay { get; }

    /// <summary>
    /// 用宏事件数据刷新卡片的方向图标、标题与副标题。
    /// </summary>
    /// <param name="macroEvent">要展示的宏事件。</param>
    public virtual void Set(MacroEvent macroEvent)
    {
        // 按事件方向选择对应图标。
        _card.Icon = macroEvent.Direction switch
        {
            MacroDirection.Up => SymbolRegular.ArrowCircleUp24,
            MacroDirection.Down => SymbolRegular.ArrowCircleDown24,
            MacroDirection.Wheel => SymbolRegular.ArrowRotateClockwise24,
            MacroDirection.HorizontalWheel => SymbolRegular.ArrowRotateClockwise24,
            MacroDirection.Move => SymbolRegular.ArrowMove24,
            _ => SymbolRegular.Empty
        };

        // 依据"来源 + 方向 + 按键值"组合生成人类可读的标题。
        // 鼠标滚轮/横向滚轮使用高位符号位区分正负方向，鼠标侧键以 >=0xFF 的编码区分。
        _header.Title = (macroEvent.Source, macroEvent.Direction, macroEvent.Key) switch
        {
            (MacroSource.Keyboard, _, _) => KeyInterop.KeyFromVirtualKey((int)macroEvent.Key).ToString(),
            (MacroSource.Mouse, MacroDirection.Move, _) => "MOVE",
            (MacroSource.Mouse, MacroDirection.Wheel, >= 0x80000000) => "WHEEL DOWN",
            (MacroSource.Mouse, MacroDirection.Wheel, _) => "WHEEL UP",
            (MacroSource.Mouse, MacroDirection.HorizontalWheel, >= 0x80000000) => "WHEEL LEFT",
            (MacroSource.Mouse, MacroDirection.HorizontalWheel, _) => "WHEEL RIGHT",
            (MacroSource.Mouse, _, >= 0xFF) => "XBUTTON" + (macroEvent.Key >> 16),
            (MacroSource.Mouse, _, 1) => "LBUTTON",
            (MacroSource.Mouse, _, 2) => "RBUTTON",
            (MacroSource.Mouse, _, 3) => "MBUTTON",
            (MacroSource.Mouse, _, _) => "BUTTON" + macroEvent.Key,
            _ => string.Empty
        };

        // 副标题显示事件来源与延迟（毫秒）。
        _header.Subtitle = macroEvent.Source.GetDisplayName() + $" • {TotalDelay.Humanize(maxUnit: TimeUnit.Millisecond)}";
    }
}

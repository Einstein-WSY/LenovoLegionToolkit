using System;
using System.ComponentModel;
using System.Drawing;
using LenovoLegionToolkit.Lib.Macro.Utils.TypeConverters;

namespace LenovoLegionToolkit.Lib.Macro;

/// <summary>
/// 单个宏事件：记录一次输入动作及其相对于上一事件的延迟。
/// </summary>
public readonly struct MacroEvent
{
    /// <summary>事件来源（键盘或鼠标）。</summary>
    public MacroSource Source { get; init; }

    /// <summary>事件方向（按下、抬起、滚轮或移动）。</summary>
    public MacroDirection Direction { get; init; }

    /// <summary>
    /// 事件的键值。键盘事件为虚拟键码；鼠标按键为 1/2/3（左/右/中）或 XBUTTON 编号；
    /// 滚轮事件为滚轮增量。
    /// </summary>
    public uint Key { get; init; }

    /// <summary>鼠标移动事件的目标坐标（屏幕坐标）；非移动事件为 <see cref="Point.Empty"/>。</summary>
    public Point Point { get; init; }

    /// <summary>该事件距上一个已录制事件的时间间隔。</summary>
    public TimeSpan Delay { get; init; }

    /// <summary>
    /// 判断该事件是否无效。来源或方向为 <c>Unknown</c> 时视为无效，不会参与回放。
    /// </summary>
    /// <returns>事件无效时返回 <see langword="true"/>。</returns>
    public bool IsUndefined() => Source == MacroSource.Unknown || Direction == MacroDirection.Unknown;

    /// <summary>
    /// 返回便于日志输出的字符串表示（包含来源、方向、键值与延迟）。
    /// </summary>
    /// <returns>事件的描述字符串。</returns>
    public override string ToString() => $"{nameof(Source)}:{Source}, {nameof(Direction)}: {Direction}, {nameof(Key)}: {Key}, {nameof(Delay)}: {Delay}";
}

/// <summary>
/// 宏序列的标识：由输入来源与键值组成，用作触发宏的按键/鼠标事件的唯一键。
/// </summary>
/// <param name="source">输入来源（键盘或鼠标）。</param>
/// <param name="key">键值，含义同 <see cref="MacroEvent.Key"/>。</param>
[TypeConverter(typeof(MacroIdentifierTypeConverter))]
public readonly struct MacroIdentifier(MacroSource source, ulong key)
{
    /// <summary>输入来源（键盘或鼠标）。</summary>
    public MacroSource Source { get; } = source;

    /// <summary>键值，含义同 <see cref="MacroEvent.Key"/>。</summary>
    public ulong Key { get; } = key;

    #region Equality

    /// <summary>
    /// 判断是否与另一个标识相等（来源与键值均相同）。
    /// </summary>
    /// <param name="obj">待比较的对象。</param>
    /// <returns>相等时返回 <see langword="true"/>。</returns>
    public override bool Equals(object? obj) => obj is MacroIdentifier other && Source == other.Source && Key == other.Key;

    /// <summary>
    /// 计算哈希值，使标识可直接作为字典键使用。
    /// </summary>
    /// <returns>组合了来源与键值的哈希值。</returns>
    public override int GetHashCode() => HashCode.Combine((int)Source, Key);

    /// <summary>相等运算符。</summary>
    /// <param name="left">左操作数。</param>
    /// <param name="right">右操作数。</param>
    /// <returns>两者相等时返回 <see langword="true"/>。</returns>
    public static bool operator ==(MacroIdentifier left, MacroIdentifier right) => left.Equals(right);

    /// <summary>不等运算符。</summary>
    /// <param name="left">左操作数。</param>
    /// <param name="right">右操作数。</param>
    /// <returns>两者不相等时返回 <see langword="true"/>。</returns>
    public static bool operator !=(MacroIdentifier left, MacroIdentifier right) => !(left == right);

    #endregion
}

/// <summary>
/// 一段可回放的宏序列及其播放选项。
/// </summary>
public readonly struct MacroSequence
{
    /// <summary>重复播放次数。</summary>
    public int RepeatCount { get; init; }

    /// <summary>是否忽略事件之间的延迟（为 <see langword="true"/> 时以最快速度连续发送）。</summary>
    public bool IgnoreDelays { get; init; }

    /// <summary>播放过程中检测到其它按键时是否中断播放。</summary>
    public bool InterruptOnOtherKey { get; init; }

    /// <summary>按录制顺序排列的事件列表。</summary>
    public MacroEvent[]? Events { get; init; }
}

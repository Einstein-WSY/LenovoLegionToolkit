using System;
using System.ComponentModel.DataAnnotations;
using LenovoLegionToolkit.Lib.Macro.Resources;

namespace LenovoLegionToolkit.Lib.Macro;

/// <summary>
/// 宏事件的输入方向，用于区分按键或鼠标事件的具体类型。
/// </summary>
public enum MacroDirection
{
    /// <summary>未知方向，表示该事件无效（不会录制或回放）。</summary>
    Unknown,
    /// <summary>按下（按键按下或鼠标按钮按下）。</summary>
    Down,
    /// <summary>抬起（按键抬起或鼠标按钮抬起）。</summary>
    Up,
    /// <summary>垂直滚轮滚动。</summary>
    Wheel,
    /// <summary>水平滚轮滚动。</summary>
    HorizontalWheel,
    /// <summary>鼠标指针移动。</summary>
    Move
}

/// <summary>
/// 宏录制器需要捕获的输入类别，可按位组合使用。
/// </summary>
[Flags]
public enum MacroRecorderSettings
{
    /// <summary>捕获键盘事件。</summary>
    Keyboard = 1,
    /// <summary>捕获鼠标按键与滚轮事件。</summary>
    Mouse = 2,
    /// <summary>捕获鼠标移动事件（仅在同时启用 <see cref="Mouse"/> 时生效）。</summary>
    Movement = 4
}

/// <summary>
/// 宏事件的输入来源（键盘或鼠标）。
/// </summary>
public enum MacroSource
{
    /// <summary>未知来源，表示该事件无效。</summary>
    Unknown,
    /// <summary>键盘输入。</summary>
    [Display(ResourceType = typeof(Resource), Name = "MacroSource_Keyboard")]
    Keyboard,
    /// <summary>鼠标输入。</summary>
    [Display(ResourceType = typeof(Resource), Name = "MacroSource_Mouse")]
    Mouse
}

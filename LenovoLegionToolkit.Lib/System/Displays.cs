using System.Linq;
using WindowsDisplayAPI;

namespace LenovoLegionToolkit.Lib.System;

/// <summary>
/// 显示器枚举辅助类，用于获取当前系统中全部（含内屏与外接屏）的显示器。
/// </summary>
public static class Displays
{
    /// <summary>
    /// 获取当前连接到系统的所有显示器。
    /// </summary>
    /// <returns>显示器数组，顺序与系统枚举顺序一致。</returns>
    public static Display[] Get() => Display.GetDisplays().ToArray();
}

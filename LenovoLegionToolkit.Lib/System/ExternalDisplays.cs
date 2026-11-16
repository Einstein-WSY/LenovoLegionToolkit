using System.Linq;
using WindowsDisplayAPI;

namespace LenovoLegionToolkit.Lib.System;

/// <summary>
/// 外接显示器枚举辅助类。
/// </summary>
public static class ExternalDisplays
{
    /// <summary>
    /// 获取当前连接的所有外接显示器（即排除笔记本内屏后的显示器集合）。
    /// </summary>
    /// <returns>外接显示器数组；若无法识别内屏，则返回全部显示器。</returns>
    public static Display[] Get()
    {
        var internalDisplay = InternalDisplay.Get();
        var allDisplays = Display.GetDisplays();
        // 通过设备路径（DevicePath）比对剔除内屏；内屏不存在时 internalDisplay 为 null，此时全部视为外接屏
        return allDisplays.Where(d => d.DevicePath != internalDisplay?.DevicePath).ToArray();
    }
}

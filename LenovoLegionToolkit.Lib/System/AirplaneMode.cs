using System.Diagnostics;

namespace LenovoLegionToolkit.Lib.System;

/// <summary>
/// 飞行模式辅助类。
/// </summary>
public static class AirplaneMode
{
    /// <summary>
    /// 打开 Windows 设置中的"飞行模式"页面。
    /// </summary>
    public static void Open()
    {
        // 通过 ms-settings: 协议启动系统设置页；必须 UseShellExecute=true 才能解析协议
        Process.Start(new ProcessStartInfo
        {
            FileName = "cmd",
            Arguments = "/c \"start ms-settings:network-airplanemode\"",
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
    }
}

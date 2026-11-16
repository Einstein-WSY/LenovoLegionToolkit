using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System.Management;

namespace LenovoLegionToolkit.Lib.Listeners;

/// <summary>
/// Win 键锁定状态监听器：仅用于感知 WMI 的按键锁定状态事件并通知外部，
/// 不进行额外处理。事件本身可被当作"Win 键锁定状态可能改变"的信号。
/// </summary>
public class WinKeyListener()
    : AbstractWMIListener<EventArgs, WinKeyChanged, int>(WMI.LenovoGameZoneKeyLockStatusEvent.Listen)
{
    // 事件不携带可解析的数值，仅作为变更信号使用
    protected override WinKeyChanged GetValue(int value) => default;

    protected override EventArgs GetEventArgs(WinKeyChanged value) => EventArgs.Empty;

    // 无需额外处理：订阅者收到 Changed 后会自行查询锁键状态
    protected override Task OnChangedAsync(WinKeyChanged value) => Task.CompletedTask;
}

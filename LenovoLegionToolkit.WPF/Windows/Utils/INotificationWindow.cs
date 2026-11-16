namespace LenovoLegionToolkit.WPF.Windows.Utils;

/// <summary>
/// 通知窗口的通用接口，统一控制通知的显示与关闭行为。
/// </summary>
public interface INotificationWindow
{
    /// <summary>
    /// 显示通知窗口。
    /// </summary>
    /// <param name="closeAfter">自动关闭前的停留毫秒数；为 0 或负值表示不自动关闭。</param>
    public void Show(int closeAfter);

    /// <summary>
    /// 关闭通知窗口。
    /// </summary>
    /// <param name="immediate">为 <see langword="true"/> 时跳过淡出动画立即关闭。</param>
    public void Close(bool immediate);
}

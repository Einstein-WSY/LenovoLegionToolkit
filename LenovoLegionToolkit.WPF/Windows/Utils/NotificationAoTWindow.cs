using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using LenovoLegionToolkit.Lib;

namespace LenovoLegionToolkit.WPF.Windows.Utils;

/// <summary>
/// 基于预渲染位图的置顶通知窗口，使用 <see cref="NativeLayeredWindow"/> 直接呈现位图。
/// </summary>
/// <remarks>"AoT" 指 Always-on-Top；相比 WPF 窗口，逐像素位图呈现更轻量，适合频繁弹出的提示。</remarks>
public class NotificationAoTWindow : NativeLayeredWindow, INotificationWindow
{
    private readonly Bitmap _bitmap;
    private readonly ScreenInfo _screenInfo;
    private readonly NotificationPosition _pos;

    /// <summary>
    /// 初始化窗口，并按屏幕信息计算显示位置。
    /// </summary>
    /// <param name="bitmap">已渲染好的通知位图。</param>
    /// <param name="screenInfo">目标屏幕信息。</param>
    /// <param name="position">通知显示位置。</param>
    public NotificationAoTWindow(Bitmap bitmap, ScreenInfo screenInfo, NotificationPosition position)
    {
        _bitmap = bitmap;
        _screenInfo = screenInfo;
        _pos = position;
        UpdatePosition();
    }

    /// <summary>
    /// 显示通知，并在指定延迟后以淡出方式关闭。
    /// </summary>
    /// <param name="closeAfter">自动关闭前的延迟毫秒数。</param>
    public void Show(int closeAfter)
    {
        Show();
        Task.Delay(closeAfter).ContinueWith(_ =>
        {
            Close(false);
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// 将预渲染位图绘制到分层窗口的绘图表面。
    /// </summary>
    protected override void Paint(PaintEventArgs e)
    {
        e.Graphics.DrawImage(_bitmap, new Rectangle(0, 0, _bitmap.Width, _bitmap.Height));
    }

    /// <summary>
    /// 根据屏幕 DPI 缩放工作区，并按通知位置计算窗口坐标。
    /// </summary>
    private void UpdatePosition()
    {
        // 工作区尺寸为 DIP，需要乘以 DPI 缩放系数换算成物理像素
        var multiplierX = _screenInfo.DpiX / 96d;
        var multiplierY = _screenInfo.DpiY / 96d;
        var wa = _screenInfo.WorkArea;
        Rect workArea = new(wa.Left, wa.Top, wa.Width * multiplierX, wa.Height * multiplierY);

        Size = new(_bitmap.Width, _bitmap.Height);

        const int margin = 16;
        // 边距同样按 DPI 缩放，保证高 DPI 下与边缘的距离一致
        var marginX = margin * multiplierX;
        var marginY = margin * multiplierY;

        double left = 0;
        double top = 0;

        switch (_pos)
        {
            case NotificationPosition.BottomRight:
                left = workArea.Right - Width - marginX;
                top = workArea.Bottom - Height - marginY;
                break;
            case NotificationPosition.BottomCenter:
                left = workArea.Left + (workArea.Width - Width) / 2;
                top = workArea.Bottom - Height - marginY;
                break;
            case NotificationPosition.BottomLeft:
                left = workArea.Left + marginX;
                top = workArea.Bottom - Height - marginY;
                break;
            case NotificationPosition.CenterLeft:
                left = workArea.Left + marginX;
                top = workArea.Top + (workArea.Height - Height) / 2;
                break;
            case NotificationPosition.TopLeft:
                left = workArea.Left + marginX;
                top = workArea.Top + marginY;
                break;
            case NotificationPosition.TopCenter:
                left = workArea.Left + (workArea.Width - Width) / 2;
                top = workArea.Top + marginY;
                break;
            case NotificationPosition.TopRight:
                left = workArea.Right - Width - marginX;
                top = workArea.Top + marginY;
                break;
            case NotificationPosition.CenterRight:
                left = workArea.Right - Width - marginX;
                top = workArea.Top + (workArea.Height - Height) / 2;
                break;
            case NotificationPosition.Center:
                left = workArea.Left + (workArea.Width - Width) / 2;
                top = workArea.Top + (workArea.Height - Height) / 2;
                break;
        }

        Position = new((int)left, (int)top);
    }
}

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace LenovoLegionToolkit.WPF.Windows.Utils;

/// <summary>
/// 基于 Win32 分层窗口（WS_EX_LAYERED）的轻量通知窗口基类，带淡入淡出动画与像素级透明。
/// </summary>
/// <remarks>通过 <see cref="Paint"/> 在 GDI 位图上绘制内容，再由 UpdateLayeredWindow 整体推送，因此不走 WPF 渲染管线。</remarks>
public class NativeLayeredWindow : NativeWindow, IDisposable
{
    // 淡入淡出总时长与定时器刷新间隔，决定每帧的不透明度步进
    private const int ANIMATION_DURATION_MS = 70;
    private const int ANIMATION_INTERVAL_MS = 10;

    private readonly Timer _animationTimer = new() { Interval = ANIMATION_INTERVAL_MS };

    private bool _disposed;
    private Size _size = new(350, 50);
    private Point _pos = new(50, 50);
    private int _animationStep;
    private byte _opacity;

    /// <summary>
    /// 获取或设置窗口尺寸；句柄已创建时同步调整原生窗口并重新绘制。
    /// </summary>
    protected Size Size
    {
        get => _size;
        set
        {
            if (Handle != nint.Zero)
            {
                UpdateWindowSizePosition(_pos.X, _pos.Y, value.Width, value.Height);
                PInvoke.GetWindowRect((HWND)Handle, out var rect);
                _size = new(rect.Width, rect.Height);
                UpdateLayeredWindow();
            }
            else
            {
                _size = value;
            }
        }
    }

    /// <summary>获取或设置窗口宽度（逻辑尺寸）。</summary>
    public int Width
    {
        get => _size.Width;
        set => _size.Width = value;
    }

    /// <summary>获取或设置窗口高度（逻辑尺寸）。</summary>
    public int Height
    {
        get => _size.Height;
        set => _size.Height = value;
    }

    /// <summary>
    /// 获取或设置窗口左上角位置；句柄已创建时同步移动原生窗口并重新绘制。
    /// </summary>
    protected Point Position
    {
        get => _pos;
        set
        {
            if (Handle != nint.Zero)
            {
                UpdateWindowSizePosition(value.X, value.Y, _size.Width, _size.Height);
                PInvoke.GetWindowRect((HWND)Handle, out var rect);
                _pos = new(rect.left, rect.top);
                UpdateLayeredWindow();
            }
            else
            {
                _pos = value;
            }
        }
    }

    /// <summary>
    /// 初始化窗口并注册动画定时器回调。
    /// </summary>
    protected NativeLayeredWindow()
    {
        _animationTimer.Tick += AnimationTimer_Tick;
    }

    /// <summary>
    /// 创建原生窗口（若尚未创建），并以淡入动画显示。
    /// </summary>
    protected void Show()
    {
        if (Handle == nint.Zero)
            CreateLayeredWindow();

        _opacity = 0;
        // 计算每帧不透明度增量，使总时长接近 ANIMATION_DURATION_MS
        _animationStep = 255 / (ANIMATION_DURATION_MS / ANIMATION_INTERVAL_MS);
        _animationTimer.Start();
        // SW_SHOWNOACTIVATE 保证显示时不会抢走前台窗口焦点
        PInvoke.ShowWindow((HWND)Handle, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
    }

    /// <summary>
    /// 隐藏窗口；非立即模式下通过淡出动画过渡。
    /// </summary>
    /// <param name="immediate">为 <see langword="true"/> 时直接隐藏。</param>
    private void Hide(bool immediate)
    {
        if (Handle == nint.Zero)
            return;

        if (immediate)
        {
            PInvoke.ShowWindow((HWND)Handle, SHOW_WINDOW_CMD.SW_HIDE);
            return;
        }

        // 反向步进实现淡出
        _animationStep = -255 / (ANIMATION_DURATION_MS / ANIMATION_INTERVAL_MS);
        _animationTimer.Start();
    }

    /// <summary>
    /// 关闭窗口；立即模式下同时释放原生资源。
    /// </summary>
    /// <param name="immediate">为 <see langword="true"/> 时跳过淡出动画。</param>
    public void Close(bool immediate)
    {
        Hide(immediate);
        if (immediate)
        {
            Dispose();
        }
    }

    /// <summary>
    /// 销毁原生窗口句柄并标记为已释放。
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            DestroyHandle();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 创建分层弹出窗口：置顶、不占任务栏、不激活、点击穿透。
    /// </summary>
    private void CreateLayeredWindow()
    {
        const WINDOW_STYLE style = WINDOW_STYLE.WS_POPUP;
        const WINDOW_EX_STYLE exStyle = WINDOW_EX_STYLE.WS_EX_TOPMOST |
                                        WINDOW_EX_STYLE.WS_EX_TOOLWINDOW |
                                        WINDOW_EX_STYLE.WS_EX_LAYERED |
                                        WINDOW_EX_STYLE.WS_EX_NOACTIVATE |
                                        WINDOW_EX_STYLE.WS_EX_TRANSPARENT;
        CreateParams cp = new()
        {
            Caption = "LenovoLegionToolkit-NativeLayeredWindow",
            X = Position.X,
            Y = Position.Y,
            Height = Size.Height,
            Width = Size.Width,
            Parent = nint.Zero,
            Style = unchecked((int)style),
            ExStyle = (int)exStyle
        };
        CreateHandle(cp);
        UpdateLayeredWindow();
    }

    /// <summary>
    /// 绘制窗口内容。派生类在此使用 GDI+ 绘制到传入的绘图表面。
    /// </summary>
    /// <param name="e">包含绘图表面与裁剪矩形的参数。</param>
    protected virtual void Paint(PaintEventArgs e) { }

    /// <summary>
    /// 将位图内容以指定不透明度推送到分层窗口。
    /// </summary>
    private void UpdateLayeredWindow()
    {
        using var bitmap = new Bitmap(Size.Width, Size.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        Rectangle cr = new(0, 0, Size.Width, Size.Height);
        Paint(new(graphics, cr));
        var hdcDst = PInvoke.GetDC(HWND.Null);
        var hdcSrc = PInvoke.CreateCompatibleDC(hdcDst);
        var hbitmap = bitmap.GetHbitmap(Color.FromArgb(0));
        var hObject = SelectObject(hdcSrc, hbitmap);
        SIZE size = new(Size.Width, Size.Height);
        Point ptDst = new(Position.X, Position.Y);
        Point ptSrc = new(0, 0);
        COLORREF colorRef = new(0); // rgbBlack
        // 使用 AC_SRC_ALPHA 实现逐像素 Alpha 混合，SourceConstantAlpha 控制整体淡入淡出
        BLENDFUNCTION blend = new()
        {
            BlendOp = (byte)PInvoke.AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = _opacity,
            AlphaFormat = (byte)PInvoke.AC_SRC_ALPHA
        };
        PInvoke.UpdateLayeredWindow((HWND)Handle, hdcDst, ptDst, size, hdcSrc, ptSrc, colorRef, blend, UPDATE_LAYERED_WINDOW_FLAGS.ULW_ALPHA);
        SelectObject(hdcSrc, hObject);
        _ = PInvoke.ReleaseDC(HWND.Null, hdcDst);
        PInvoke.DeleteObject((HGDIOBJ)hbitmap);
        PInvoke.DeleteDC(hdcSrc);
    }

    /// <summary>
    /// 更新窗口位置与尺寸；仅变化的维度才会被实际修改。
    /// </summary>
    private void UpdateWindowSizePosition(int x, int y, int width, int height)
    {
        if (Position.X == x && Position.Y == y && Size.Width == width && Size.Height == height)
            return;

        if (Handle != nint.Zero)
        {
            SET_WINDOW_POS_FLAGS flags = 0;
            if (Position.X == x && Position.Y == y)
                flags |= SET_WINDOW_POS_FLAGS.SWP_NOMOVE;
            if (Size.Width == width && Size.Height == height)
                flags |= SET_WINDOW_POS_FLAGS.SWP_NOSIZE;

            PInvoke.SetWindowPos((HWND)Handle, HWND.Null, x, y, width, height, flags);
        }
        else
        {
            Size = new(width, height);
            Position = new(x, y);
        }
    }

    /// <summary>
    /// 动画帧回调：逐步调整不透明度直到 0 或 255，淡出结束时隐藏并释放窗口。
    /// </summary>
    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        _opacity = (byte)Math.Clamp(_opacity + _animationStep, 0, 255);
        UpdateLayeredWindow();

        if (_opacity is not (0 or 255))
            return;
        _animationTimer.Stop();

        if (_opacity != 0)
            return;

        PInvoke.ShowWindow((HWND)Handle, SHOW_WINDOW_CMD.SW_HIDE);
        Dispose();
    }

    [DllImport("gdi32.dll", CharSet = CharSet.Auto)]
    private static extern nint SelectObject(nint hDc, nint hObject);
}

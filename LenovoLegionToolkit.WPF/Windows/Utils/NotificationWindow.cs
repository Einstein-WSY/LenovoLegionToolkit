using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.WPF.Utils;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Appearance;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Windows.Utils;

/// <summary>
/// WPF 通知窗口，以无边框置顶窗口显示图标与文本，支持按屏幕位置与 DPI 精确定位。
/// </summary>
public class NotificationWindow : UiWindow, INotificationWindow
{
    private readonly ScreenInfo _screenInfo;

    private readonly Grid _mainGrid = new()
    {
        ColumnDefinitions =
        {
            new() { Width = GridLength.Auto, },
            new() { Width = new(1, GridUnitType.Star) },
        },
        Margin = new(16, 16, 32, 16),
    };

    private readonly SymbolIcon _symbolIcon = new()
    {
        FontSize = 32,
        Margin = new(0, 0, 16, 0),
    };

    private readonly SymbolIcon _overlaySymbolIcon = new()
    {
        FontSize = 32,
        Margin = new(0, 0, 16, 0),
    };

    private readonly Label _textBlock = new()
    {
        FontSize = 16,
        FontWeight = FontWeights.Medium,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    private bool _gettingBitMap;

    /// <summary>
    /// 初始化通知窗口并注册位置计算与点击关闭行为。
    /// </summary>
    /// <param name="symbol">主图标。</param>
    /// <param name="overlaySymbol">可选的叠加小图标。</param>
    /// <param name="symbolTransform">可选的图标外观调整回调。</param>
    /// <param name="text">通知正文。</param>
    /// <param name="clickAction">点击通知后执行的操作。</param>
    /// <param name="screenInfo">目标屏幕信息，包含工作区与 DPI。</param>
    /// <param name="position">通知在屏幕上的显示位置。</param>
    public NotificationWindow(SymbolRegular symbol, SymbolRegular? overlaySymbol, Action<SymbolIcon>? symbolTransform, string text, Action? clickAction, ScreenInfo screenInfo, NotificationPosition position)
    {
        InitializeStyle();
        InitializeContent(symbol, overlaySymbol, symbolTransform, text);

        _screenInfo = screenInfo;

        SourceInitialized += (_, _) => InitializePosition(screenInfo.WorkArea, screenInfo.DpiX, screenInfo.DpiY, position);
        MouseDown += (_, _) =>
        {
            Close();
            clickAction?.Invoke();
        };
    }

    /// <summary>
    /// 显示通知，并在指定延迟后自动关闭。
    /// </summary>
    /// <param name="closeAfter">自动关闭前的延迟毫秒数。</param>
    public void Show(int closeAfter)
    {
        Show();
        Task.Delay(closeAfter).ContinueWith(_ =>
        {
            Close();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// 关闭通知窗口。
    /// </summary>
    /// <param name="immediate">为 <see langword="true"/> 时立即关闭，不做过渡处理。</param>
    public void Close(bool immediate)
    {
        WindowStyle = WindowStyle.None;
        Close();
    }

    /// <summary>
    /// 将窗口渲染为位图并裁剪圆角，供原生分层窗口显示使用。
    /// </summary>
    /// <returns>按屏幕 DPI 缩放后的通知位图。</returns>
    public Bitmap GetBitmapView()
    {
        // 借助一次显示/关闭让窗口完成布局测量，从而获得有效尺寸
        _gettingBitMap = true;
        Show();
        Close();
        _gettingBitMap = false;

        RenderTargetBitmap rtb = new((int)Width, (int)Height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(this);
        var ms = new MemoryStream();
        var encoder = new BmpBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        encoder.Save(ms);
        using var bitmap = new Bitmap(ms);

        // 96 DPI 下的位图需要按屏幕缩放比例放大，才能保持视觉尺寸一致
        var multiplierX = _screenInfo.DpiX / 96d;
        var multiplierY = _screenInfo.DpiY / 96d;
        var newWidth = (int)(bitmap.Width * multiplierX);
        var newHeight = (int)(bitmap.Height * multiplierY);
        var resizedBitmap = new Bitmap(newWidth, newHeight);
        using var graphics = Graphics.FromImage(resizedBitmap);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var borderPath = GetRoundedRectanglePath(new(0, 0, newWidth, newHeight), 10);
        var penPath = GetRoundedRectanglePath(new(1, 1, newWidth - 3, newHeight - 3), 10);

        // 裁剪为圆角矩形后再绘制边框，避免四角出现直角残留
        graphics.SetClip(borderPath);
        graphics.DrawImage(bitmap, 0, 0, newWidth, newHeight);
        graphics.ResetClip();

        using var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(64, 64, 64), 3);
        graphics.DrawPath(pen, penPath);

        return resizedBitmap;
    }

    /// <summary>
    /// 设置通知窗口样式：无边框、置顶、不激活、不显示在任务栏，并适配本地化方向。
    /// </summary>
    private void InitializeStyle()
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        ResizeMode = ResizeMode.NoResize;
        WindowBackdropType = BackgroundType.None;

        Focusable = false;
        Topmost = true;
        ExtendsContentIntoTitleBar = true;
        ShowInTaskbar = false;
        ShowActivated = false;

        _mainGrid.FlowDirection = LocalizationHelper.Direction;
        _textBlock.Foreground = (SolidColorBrush)FindResource("TextFillColorPrimaryBrush");
    }

    /// <summary>
    /// 按通知位置与屏幕工作区计算窗口坐标，并直接通过 Win32 设置窗口位置。
    /// </summary>
    /// <param name="workArea">屏幕工作区（DIP）。</param>
    /// <param name="dpiX">屏幕水平 DPI。</param>
    /// <param name="dpiY">屏幕垂直 DPI。</param>
    /// <param name="position">通知显示位置。</param>
    private void InitializePosition(Rect workArea, uint dpiX, uint dpiY, NotificationPosition position)
    {
        _mainGrid.Measure(new System.Windows.Size(double.PositiveInfinity, 80));

        // DIP 与物理像素的换算系数
        var multiplierX = dpiX / 96d;
        var multiplierY = dpiY / 96d;
        Rect nativeWorkArea = new(workArea.Left, workArea.Top, workArea.Width * multiplierX, workArea.Height * multiplierY);

        Width = MaxWidth = MinWidth = Math.Max(_mainGrid.DesiredSize.Width, 300);
        Height = MaxHeight = MinHeight = _mainGrid.DesiredSize.Height;

        double nativeLeft = 0;
        double nativeTop = 0;

        if (_gettingBitMap)
        {
            // 离屏渲染位图时把窗口移到屏幕外，避免闪烁
            nativeLeft = -1048576;
            nativeTop = -1048576;
        }
        else
        {
            var nativeWidth = Width * multiplierX;
            var nativeHeight = Height * multiplierY;

            const int margin = 16;
            var nativeMarginX = margin * multiplierX;
            var nativeMarginY = margin * multiplierY;

            switch (position)
            {
                case NotificationPosition.BottomRight:
                    nativeLeft = nativeWorkArea.Right - nativeWidth - nativeMarginX;
                    nativeTop = nativeWorkArea.Bottom - nativeHeight - nativeMarginY;
                    break;
                case NotificationPosition.BottomCenter:
                    nativeLeft = nativeWorkArea.Left + (nativeWorkArea.Width - nativeWidth) / 2;
                    nativeTop = nativeWorkArea.Bottom - nativeHeight - nativeMarginY;
                    break;
                case NotificationPosition.BottomLeft:
                    nativeLeft = nativeWorkArea.Left + nativeMarginX;
                    nativeTop = nativeWorkArea.Bottom - nativeHeight - nativeMarginY;
                    break;
                case NotificationPosition.CenterLeft:
                    nativeLeft = nativeWorkArea.Left + nativeMarginX;
                    nativeTop = nativeWorkArea.Top + (nativeWorkArea.Height - nativeHeight) / 2;
                    break;
                case NotificationPosition.TopLeft:
                    nativeLeft = nativeWorkArea.Left + nativeMarginX;
                    nativeTop = nativeWorkArea.Top + nativeMarginY;
                    break;
                case NotificationPosition.TopCenter:
                    nativeLeft = nativeWorkArea.Left + (nativeWorkArea.Width - nativeWidth) / 2;
                    nativeTop = nativeWorkArea.Top + nativeMarginY;
                    break;
                case NotificationPosition.TopRight:
                    nativeLeft = nativeWorkArea.Right - nativeWidth - nativeMarginX;
                    nativeTop = nativeWorkArea.Top + nativeMarginY;
                    break;
                case NotificationPosition.CenterRight:
                    nativeLeft = nativeWorkArea.Right - nativeWidth - nativeMarginX;
                    nativeTop = nativeWorkArea.Top + (nativeWorkArea.Height - nativeHeight) / 2;
                    break;
                case NotificationPosition.Center:
                    nativeLeft = nativeWorkArea.Left + (nativeWorkArea.Width - nativeWidth) / 2;
                    nativeTop = nativeWorkArea.Top + (nativeWorkArea.Height - nativeHeight) / 2;
                    break;
            }
        }

        var windowInteropHandler = new WindowInteropHelper(this);

        PInvoke.SetWindowPos((HWND)windowInteropHandler.Handle, HWND.Null, (int)nativeLeft, (int)nativeTop, 0, 0, SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE);
    }

    /// <summary>
    /// 组装通知内容：图标、可选叠加图标与正文文本。
    /// </summary>
    private void InitializeContent(SymbolRegular symbol, SymbolRegular? overlaySymbol, Action<SymbolIcon>? symbolTransform, string text)
    {
        _symbolIcon.Symbol = symbol;
        _textBlock.Content = text;

        Grid.SetColumn(_symbolIcon, 0);
        Grid.SetColumn(_textBlock, 1);

        _mainGrid.Children.Add(_symbolIcon);
        _mainGrid.Children.Add(_textBlock);

        if (overlaySymbol.HasValue)
        {
            _overlaySymbolIcon.Symbol = overlaySymbol.Value;
            Grid.SetColumn(_overlaySymbolIcon, 0);
            _mainGrid.Children.Add(_overlaySymbolIcon);
        }

        symbolTransform?.Invoke(_symbolIcon);

        Content = _mainGrid;
    }

    /// <summary>
    /// 构造圆角矩形路径，用于位图裁剪与边框绘制。
    /// </summary>
    /// <param name="rect">矩形范围。</param>
    /// <param name="radius">圆角半径。</param>
    /// <returns>闭合的圆角矩形路径。</returns>
    private GraphicsPath GetRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

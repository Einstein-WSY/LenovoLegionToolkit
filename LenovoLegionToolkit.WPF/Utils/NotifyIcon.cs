using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Interop;
using LenovoLegionToolkit.Lib.Utils;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Utils;

/// <summary>
/// 托盘图标封装：通过 Win32 Shell_NotifyIcon 创建托盘图标，并处理悬停提示、左键点击与右键菜单。
/// </summary>
public class NotifyIcon : NativeWindow, IDisposable
{
    // 自定义托盘回调消息 ID
    private const uint TRAY_MESSAGE_ID = PInvoke.WM_USER + 1069;

    // 资源管理器重启后会广播该消息，用于重新添加图标
    private static readonly uint TaskbarCreatedMessage = PInvoke.RegisterWindowMessage("TaskbarCreated");

    private static uint _nextId;

    private readonly object _lock = new();
    private readonly uint _id = ++_nextId;

    private bool _added;
    private CancellationTokenSource? _showToolTipCancellationTokenSource;

    private bool _visible;
    /// <summary>设置图标是否可见，并立即更新托盘状态。</summary>
    public bool Visible
    {
        set
        {
            _visible = value;
            UpdateIcon();
        }
    }

    private Icon? _icon;
    /// <summary>设置托盘图标。</summary>
    public Icon? Icon
    {
        set
        {
            _icon = value;
            UpdateIcon();

        }
    }

    private string? _text;
    /// <summary>设置托盘图标的悬浮文本（无自定义提示窗口时生效）。</summary>
    public string? Text
    {
        set
        {
            _text = value;
            UpdateIcon();
        }
    }

    private UiWindow? _currentToolTipWindow;

    private Func<Task<UiWindow>>? _toolTipWindow;
    /// <summary>设置自定义悬浮提示窗口的工厂方法。</summary>
    public Func<Task<UiWindow>>? ToolTipWindow
    {
        set
        {
            _toolTipWindow = value;
            UpdateIcon();
        }
    }

    /// <summary>右键菜单。</summary>
    public ContextMenu? ContextMenu { get; set; }

    /// <summary>左键点击图标时触发。</summary>
    public event EventHandler? OnClick;

    public NotifyIcon()
    {
        UpdateIcon();
    }

    /// <summary>
    /// 处理托盘图标的窗口消息：悬停、左键点击、右键菜单与资源管理器重启。
    /// </summary>
    /// <param name="m">窗口消息。</param>
    protected override void WndProc(ref Message m)
    {
        switch ((uint)m.Msg)
        {
            case TRAY_MESSAGE_ID:
                // 低 16 位为具体的鼠标/提示事件
                switch ((uint)m.LParam & 0xFFFF)
                {
                    case PInvoke.NIN_POPUPOPEN:
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"NIN_POPUPOPEN");
                        ShowToolTipAsync();
                        break;
                    case PInvoke.NIN_POPUPCLOSE:
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"NIN_POPUPCLOSE");
                        HideToolTip();
                        break;
                    case PInvoke.WM_LBUTTONUP:
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"WM_LBUTTONUP");
                        HideToolTip();
                        HideContextMenu();
                        OnClick?.Invoke(this, EventArgs.Empty);
                        break;
                    case PInvoke.WM_RBUTTONUP:
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"WM_RBUTTONUP");
                        HideToolTip();
                        ShowContextMenu();
                        break;
                }
                break;
            case PInvoke.WM_DESTROY:
                _visible = false;
                UpdateIcon();
                break;
            default:
                // 资源管理器重启后重新添加图标
                if (m.Msg == TaskbarCreatedMessage && _visible)
                {
                    _visible = true;
                    _added = false;
                    UpdateIcon();
                }

                DefWndProc(ref m);
                break;
        }
    }

    // 延迟 500ms 后显示自定义提示窗口，避免悬停时闪烁
    private async void ShowToolTipAsync()
    {
        if (_toolTipWindow is null)
            return;

        if (_showToolTipCancellationTokenSource is not null)
            await _showToolTipCancellationTokenSource.CancelAsync();
        _showToolTipCancellationTokenSource = new();

        var token = _showToolTipCancellationTokenSource.Token;

        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), token);

            // 右键菜单打开时不显示提示
            if (ContextMenu is not null && ContextMenu.IsOpen)
                return;

            _currentToolTipWindow?.Close();
            _currentToolTipWindow = await _toolTipWindow();

            token.ThrowIfCancellationRequested();

            _currentToolTipWindow?.Show();
        }
        catch (OperationCanceledException)
        {
            _currentToolTipWindow?.Close();
            _currentToolTipWindow = null;
        }
        catch (Exception ex)
        {
            _currentToolTipWindow?.Close();
            _currentToolTipWindow = null;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to show tooltip.", ex);
        }
    }

    private void HideToolTip()
    {
        if (_toolTipWindow is null)
            return;

        _showToolTipCancellationTokenSource?.Cancel();

        _currentToolTipWindow?.Hide();
        _currentToolTipWindow = null;
    }

    // 以鼠标位置为基准弹出右键菜单，并前置其宿主窗口
    private void ShowContextMenu()
    {
        if (ContextMenu is null)
            return;

        ContextMenu.Placement = PlacementMode.Mouse;
        ContextMenu.PlacementRectangle = Rect.Empty;
        ContextMenu.PlacementTarget = null;
        ContextMenu.IsOpen = true;

        // 让菜单窗口获得前景，点击其他位置时能正常关闭
        if (PresentationSource.FromVisual(ContextMenu) is HwndSource source && source.Handle != IntPtr.Zero)
            PInvoke.SetForegroundWindow(new HWND(source.Handle));
    }

    private void HideContextMenu()
    {
        if (ContextMenu is null || !ContextMenu.IsOpen)
            return;

        ContextMenu.IsOpen = false;
    }

    // 根据当前状态添加、修改或删除托盘图标
    private void UpdateIcon()
    {
        lock (_lock)
        {
            var data = new NOTIFYICONDATAW
            {
                cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                uID = _id,
                uCallbackMessage = TRAY_MESSAGE_ID,
                uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_TIP,
                szTip = " "
            };

            // 首次可见时才创建隐藏窗口句柄
            if (_visible && Handle == IntPtr.Zero)
                CreateHandle(new CreateParams());

            data.hWnd = new HWND(Handle);

            if (_icon is not null)
            {
                data.uFlags |= NOTIFY_ICON_DATA_FLAGS.NIF_ICON;
                data.hIcon = new HICON(_icon.Handle);
            }

            // 无自定义提示窗口时使用系统悬浮文本
            if (_text is not null && _toolTipWindow is null)
            {
                data.uFlags |= NOTIFY_ICON_DATA_FLAGS.NIF_SHOWTIP;
                data.szTip = _text;
            }

            switch (_visible, _added)
            {
                case (true, false):
                    // 先添加图标，再切换为 v4 行为以支持 NIN_POPUPOPEN 等消息
                    PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, data);
                    data.Anonymous = new() { uVersion = 4 };
                    PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_SETVERSION, data);
                    _added = true;
                    break;
                case (true, true):
                    PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, data);
                    break;
                case (false, true):
                    PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, data);
                    _added = false;
                    break;
            }
        }
    }

    /// <summary>释放图标、菜单与窗口句柄等资源。</summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        HideContextMenu();
        HideToolTip();

        _visible = false;
        UpdateIcon();

        _icon?.Dispose();

        _icon = null;
        _text = null;
        _toolTipWindow = null;
        ContextMenu = null;

        ReleaseHandle();
    }
}

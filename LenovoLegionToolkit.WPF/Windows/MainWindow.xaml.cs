using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Pages;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;
using LenovoLegionToolkit.WPF.Windows.Utils;
using Microsoft.Xaml.Behaviors.Core;
using Windows.Win32;
using Windows.Win32.System.Threading;
using Wpf.Ui.Controls;
#if !DEBUG
using System.Reflection;
using LenovoLegionToolkit.Lib.Extensions;
#endif

#pragma warning disable CA1416

namespace LenovoLegionToolkit.WPF.Windows;

/// <summary>
/// 应用主窗口，承载导航栏与各功能页面，并管理托盘图标、窗口尺寸记忆与状态指示器。
/// </summary>
/// <remarks>主窗口是整个应用的宿主，关闭时根据设置决定是最小化还是退出应用。</remarks>
public partial class MainWindow
{
    private readonly ApplicationSettings _applicationSettings = IoCContainer.Resolve<ApplicationSettings>();
    private readonly SpecialKeyListener _specialKeyListener = IoCContainer.Resolve<SpecialKeyListener>();
    private readonly VantageDisabler _vantageDisabler = IoCContainer.Resolve<VantageDisabler>();
    private readonly LegionZoneDisabler _legionZoneDisabler = IoCContainer.Resolve<LegionZoneDisabler>();
    private readonly FnKeysDisabler _fnKeysDisabler = IoCContainer.Resolve<FnKeysDisabler>();

    private TrayHelper? _trayHelper;

    /// <summary>是否在托盘图标的提示文本中显示附加信息。</summary>
    public bool TrayTooltipEnabled { get; init; } = true;

    /// <summary>是否禁用与 Vantage、Legion Zone 等冲突软件的警告指示器。</summary>
    public bool DisableConflictingSoftwareWarning { get; set; }

    /// <summary>是否跳过关闭事件处理器，用于程序主动退出时的流程控制。</summary>
    public bool SuppressClosingEventHandler { get; set; }

    /// <summary>窗口内的消息条，供页面显示操作结果提示。</summary>
    public Snackbar Snackbar => _snackbar;

    /// <summary>
    /// 初始化主窗口，注册生命周期事件并刷新标题中的调试、Beta 与日志状态标记。
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();

        Closing += MainWindow_Closing;
        Closed += MainWindow_Closed;
        Loaded += MainWindow_Loaded;
        SourceInitialized += MainWindow_SourceInitialized;
        StateChanged += MainWindow_StateChanged;

#if DEBUG
        _title.Text += Debugger.IsAttached ? " [DEBUGGER ATTACHED]" : " [DEBUG]";
#else
        var version = Assembly.GetEntryAssembly()?.GetName().Version;
        if (version is not null && version.IsBeta())
            _title.Text += " [BETA]";
#endif

        if (Log.Instance.IsTraceEnabled)
        {
            _title.Text += " [LOGGING ENABLED]";
            _openLogIndicator.Visibility = Visibility.Visible;
        }

        Title = _title.Text;
    }

    /// <summary>
    /// 窗口句柄创建完成后恢复上次保存的窗口尺寸与位置。
    /// </summary>
    private void MainWindow_SourceInitialized(object? sender, EventArgs e) => RestoreSize();

    /// <summary>
    /// 窗口加载完成后初始化导航、快捷键、托盘图标与设备信息等运行期资源。
    /// </summary>
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // 初始化期间先隐藏内容，避免未就绪的控件闪烁
        _contentGrid.Visibility = Visibility.Hidden;

        // 硬件不支持键盘背光时移除对应导航项
        if (!await KeyboardBacklightPage.IsSupportedAsync())
            _navigationStore.Items.Remove(_keyboardItem);

        // Fn+N 等特殊按键触发时将窗口激活到前台
        SmartKeyHelper.Instance.BringToForeground = () => Dispatcher.Invoke(BringToForeground);

        _specialKeyListener.Changed += SpecialKeyListener_Changed;

        _contentGrid.Visibility = Visibility.Visible;

        LoadDeviceInfo();
        UpdateIndicators();

        // Ctrl+Tab / Ctrl+Shift+Tab 在页面间循环切换；Ctrl+数字 直接跳转到对应导航项
        InputBindings.Add(new KeyBinding(new ActionCommand(_navigationStore.NavigateToNext), Key.Tab, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new ActionCommand(_navigationStore.NavigateToPrevious), Key.Tab, ModifierKeys.Control | ModifierKeys.Shift));

        var key = (int)Key.D1;
        foreach (var item in _navigationStore.Items.OfType<NavigationItem>())
            InputBindings.Add(new KeyBinding(new ActionCommand(() => _navigationStore.Navigate(item.PageTag)), (Key)key++, ModifierKeys.Control));

        var trayHelper = new TrayHelper(_navigationStore, BringToForeground, TrayTooltipEnabled);
        await trayHelper.InitializeAsync();
        trayHelper.MakeVisible();
        _trayHelper = trayHelper;
    }

    /// <summary>
    /// 处理窗口关闭：保存尺寸，并按设置选择最小化到托盘还是退出应用。
    /// </summary>
    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        SaveSize();

        if (SuppressClosingEventHandler)
            return;

        if (_applicationSettings.Store.MinimizeOnClose)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Minimizing...");

            // 取消真正的关闭，仅将窗口最小化
            WindowState = WindowState.Minimized;
            e.Cancel = true;
        }
        else
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Closing...");

            await App.Current.ShutdownAsync();
        }
    }

    /// <summary>
    /// 窗口关闭后释放托盘图标资源，防止残留图标。
    /// </summary>
    private void MainWindow_Closed(object? sender, EventArgs args)
    {
        _specialKeyListener.Changed -= SpecialKeyListener_Changed;

        _vantageDisabler.OnRefreshed -= VantageDisabler_OnRefreshed;
        _legionZoneDisabler.OnRefreshed -= LegionZoneDisabler_OnRefreshed;
        _fnKeysDisabler.OnRefreshed -= FnKeysDisabler_OnRefreshed;

        _trayHelper?.Dispose();
        _trayHelper = null;
    }

    /// <summary>
    /// 响应窗口状态变化：最小化时进入效率模式并隐藏到托盘，恢复时回到前台。
    /// </summary>
    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Window state changed to {WindowState}");

        switch (WindowState)
        {
            case WindowState.Minimized:
                SetEfficiencyMode(true);
                SendToTray();
                break;
            case WindowState.Normal:
                SetEfficiencyMode(false);
                BringToForeground();
                break;
        }
    }

    /// <summary>点击日志指示器时打开日志文件所在位置。</summary>
    private void OpenLogIndicator_Click(object sender, MouseButtonEventArgs e) => OpenLog();

    /// <summary>键盘回车或空格触发日志指示器，保证可访问性。</summary>
    private void OpenLogIndicator_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Enter and not Key.Space)
            return;

        OpenLog();
    }

    /// <summary>点击设备信息指示器时打开设备信息窗口。</summary>
    private void DeviceInfoIndicator_Click(object sender, MouseButtonEventArgs e) => ShowDeviceInfoWindow();

    /// <summary>键盘回车或空格触发设备信息指示器。</summary>
    private void DeviceInfoIndicator_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not Key.Enter and not Key.Space)
            return;

        ShowDeviceInfoWindow();
    }

    /// <summary>
    /// 在后台读取机器型号，并在 UI 线程回填到设备信息指示器。
    /// </summary>
    private void LoadDeviceInfo()
    {
        Task.Run(Compatibility.GetMachineInformationAsync)
            .ContinueWith(mi =>
            {
                try
                {
                    _deviceInfoIndicator.Content = mi.Result.Model;
                    _deviceInfoIndicator.Visibility = Visibility.Visible;
                }
                catch { /* Ignored. */ }
            }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    /// <summary>
    /// 订阅冲突软件状态变化，在检测到 Vantage / Legion Zone / Fn 键驱动启用时显示警告指示器。
    /// </summary>
    private void UpdateIndicators()
    {
        if (DisableConflictingSoftwareWarning)
            return;

        _vantageDisabler.OnRefreshed += VantageDisabler_OnRefreshed;

        _legionZoneDisabler.OnRefreshed += LegionZoneDisabler_OnRefreshed;

        _fnKeysDisabler.OnRefreshed += FnKeysDisabler_OnRefreshed;

        Task.Run(async () =>
        {
            _ = await _vantageDisabler.GetStatusAsync().ConfigureAwait(false);
            _ = await _legionZoneDisabler.GetStatusAsync().ConfigureAwait(false);
            _ = await _fnKeysDisabler.GetStatusAsync().ConfigureAwait(false);
        });
    }

    /// <summary>特殊按键变化：Fn+N 触发时把主窗口激活到前台。</summary>
    private void SpecialKeyListener_Changed(object? sender, SpecialKeyListener.ChangedEventArgs args)
    {
        if (args.SpecialKey == SpecialKey.FnN)
            Dispatcher.Invoke(BringToForeground);
    }

    /// <summary>Vantage 状态刷新：同步冲突软件警告指示器显隐。</summary>
    private void VantageDisabler_OnRefreshed(object? sender, AbstractSoftwareDisabler.AbstractSoftwareDisablerEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _vantageIndicator.Visibility = e.Status == SoftwareStatus.Enabled ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    /// <summary>Legion Zone 状态刷新：同步冲突软件警告指示器显隐。</summary>
    private void LegionZoneDisabler_OnRefreshed(object? sender, AbstractSoftwareDisabler.AbstractSoftwareDisablerEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _legionZoneIndicator.Visibility = e.Status == SoftwareStatus.Enabled ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    /// <summary>原厂热键服务状态刷新：同步冲突软件警告指示器显隐。</summary>
    private void FnKeysDisabler_OnRefreshed(object? sender, AbstractSoftwareDisabler.AbstractSoftwareDisablerEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _fnKeysIndicator.Visibility = e.Status == SoftwareStatus.Enabled ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    /// <summary>
    /// 恢复上次保存的窗口尺寸，并在主显示器工作区内居中显示。
    /// </summary>
    private void RestoreSize()
    {
        if (!_applicationSettings.Store.WindowSize.HasValue)
            return;

        Width = Math.Max(MinWidth, _applicationSettings.Store.WindowSize.Value.Width);
        Height = Math.Max(MinHeight, _applicationSettings.Store.WindowSize.Value.Height);

        ScreenHelper.UpdateScreenInfos();
        var primaryScreen = ScreenHelper.PrimaryScreen;

        if (!primaryScreen.HasValue)
            return;

        var desktopWorkingArea = primaryScreen.Value.WorkArea;
        Left = (desktopWorkingArea.Width - Width) / 2 + desktopWorkingArea.Left;
        Top = (desktopWorkingArea.Height - Height) / 2 + desktopWorkingArea.Top;
    }

    /// <summary>
    /// 保存当前窗口尺寸；窗口处于最大化等状态时保存还原后的边界尺寸。
    /// </summary>
    private void SaveSize()
    {
        _applicationSettings.Store.WindowSize = WindowState != WindowState.Normal
            ? new(RestoreBounds.Width, RestoreBounds.Height)
            : new(Width, Height);
        _applicationSettings.SynchronizeStore();
    }

    private void BringToForeground() => WindowExtensions.BringToForeground(this);

    /// <summary>
    /// 用资源管理器打开日志文件，便于用户查看诊断信息。
    /// </summary>
    private static void OpenLog()
    {
        try
        {
            if (!Directory.Exists(Folders.AppData))
                return;

            Process.Start("explorer", Log.Instance.LogPath);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to open log.", ex);
        }
    }

    /// <summary>
    /// 以模态方式显示设备信息窗口。
    /// </summary>
    private void ShowDeviceInfoWindow()
    {
        var window = new DeviceInformationWindow { Owner = this };
        window.ShowDialog();
    }

    /// <summary>
    /// 在启用"最小化到托盘"设置时隐藏窗口并降低进程优先级。
    /// </summary>
    public void SendToTray()
    {
        if (!_applicationSettings.Store.MinimizeToTray)
            return;

        SetEfficiencyMode(true);
        // 先从任务栏移除窗口，再隐藏，托盘图标由 TrayHelper 维持
        Hide();
        ShowInTaskbar = true;
    }

    /// <summary>
    /// 通过 Win32 API 切换进程的效率模式（降低 CPU 优先级并启用电源限制）。
    /// </summary>
    /// <param name="enabled">为 <see langword="true"/> 时进入效率模式。</param>
    private static unsafe void SetEfficiencyMode(bool enabled)
    {
        var ptr = IntPtr.Zero;

        try
        {
            var priorityClass = enabled
                ? PROCESS_CREATION_FLAGS.IDLE_PRIORITY_CLASS
                : PROCESS_CREATION_FLAGS.NORMAL_PRIORITY_CLASS;
            PInvoke.SetPriorityClass(PInvoke.GetCurrentProcess(), priorityClass);

            var state = new PROCESS_POWER_THROTTLING_STATE
            {
                Version = PInvoke.PROCESS_POWER_THROTTLING_CURRENT_VERSION,
                ControlMask = PInvoke.PROCESS_POWER_THROTTLING_EXECUTION_SPEED,
                // StateMask 置位表示对执行速度进行限制，清零则解除限制
                StateMask = enabled ? PInvoke.PROCESS_POWER_THROTTLING_EXECUTION_SPEED : 0,
            };

            var size = Marshal.SizeOf<PROCESS_POWER_THROTTLING_STATE>();
            ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(state, ptr, false);

            PInvoke.SetProcessInformation(PInvoke.GetCurrentProcess(),
                PROCESS_INFORMATION_CLASS.ProcessPowerThrottling,
                ptr.ToPointer(),
                (uint)size);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}

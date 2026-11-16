using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;
using LenovoLegionToolkit.WPF.Windows.KeyboardBacklight.Spectrum;
using Microsoft.Win32;
using NeoSmart.AsyncLock;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.Spectrum;

/// <summary>
/// Spectrum 键盘背光主控制面板：提供亮度调节、6 组预设配置切换、按键级灯效编辑与导入导出，
/// 并通过约 50ms 的轮询循环在设备示意图上实时回显硬件各按键的当前颜色。
/// </summary>
public partial class SpectrumKeyboardBacklightControl
{
    /// <summary>亮度写入节流器：250ms 内的连续滑动只上报一次，避免高频写硬件。</summary>
    private readonly ThrottleLastDispatcher _changeBrightnessDispatcher = new(TimeSpan.FromMilliseconds(250), "ChangeBrightnessDispatcher");
    /// <summary>按键颜色回显的轮询间隔（50ms，约 20 帧每秒）。</summary>
    private readonly TimeSpan _refreshStateInterval = TimeSpan.FromMilliseconds(50);
    /// <summary>串行化动画的启动/停止，避免并发操作轮询任务与令牌源。</summary>
    private readonly AsyncLock _startStopAnimationLock = new();

    /// <summary>Spectrum 键盘背光控制器。</summary>
    private readonly SpectrumKeyboardBacklightController _controller = IoCContainer.Resolve<SpectrumKeyboardBacklightController>();
    /// <summary>特殊键监听器，用于响应 Spectrum 专用快捷键。</summary>
    private readonly SpecialKeyListener _listener = IoCContainer.Resolve<SpecialKeyListener>();
    /// <summary>Vantage 软件禁用器，用于检测冲突软件是否仍在运行。</summary>
    private readonly VantageDisabler _vantageDisabler = IoCContainer.Resolve<VantageDisabler>();
    /// <summary>Spectrum 键盘设置（如用户选择的键盘物理布局）。</summary>
    private readonly SpectrumKeyboardSettings _settings = IoCContainer.Resolve<SpectrumKeyboardSettings>();

    /// <summary>按键颜色轮询循环的取消令牌源。</summary>
    private CancellationTokenSource? _refreshStateCancellationTokenSource;
    /// <summary>按键颜色轮询循环任务。</summary>
    private Task? _refreshStateTask;

    /// <summary>6 个预设配置按钮（RadioButton 互斥）。</summary>
    private RadioButton[] ProfileButtons =>
    [
        _profileButton1,
        _profileButton2,
        _profileButton3,
        _profileButton4,
        _profileButton5,
        _profileButton6
    ];

    /// <summary>
    /// 刷新期间保持交互可用：亮度与预设切换都是即时硬件操作，不需要锁定界面。
    /// </summary>
    protected override bool DisablesWhileRefreshing => false;

    /// <summary>
    /// 初始化控件并加载 XAML 视图，订阅可见性、尺寸、特殊键变化与跨模块消息。
    /// </summary>
    public SpectrumKeyboardBacklightControl()
    {
        InitializeComponent();

        IsVisibleChanged += SpectrumKeyboardBacklightControl_IsVisibleChanged;
        SizeChanged += SpectrumKeyboardBacklightControl_SizeChanged;

        _listener.Changed += Listener_Changed;

        // 其他入口修改 Spectrum 背光后广播消息，页面可见时同步亮度、预设与灯效列表。
        MessagingCenter.Subscribe<SpectrumBacklightChangedMessage>(this, () => Dispatcher.InvokeTask(async () =>
        {
            if (!IsVisible)
                return;

            await RefreshBrightnessAsync();
            await RefreshProfileAsync();
            await RefreshProfileDescriptionAsync();
        }));
    }

    /// <summary>
    /// 可见性变化回调：页面隐藏时停止颜色轮询并清空灯效列表，释放渲染开销。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">依赖属性变化参数。</param>
    private async void SpectrumKeyboardBacklightControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            return;

        await StopAnimationAsync();
        _effects.Children.Clear();
    }

    /// <summary>
    /// 尺寸变化回调：按可用宽度等比缩放设备示意图，缩放比例限制在 0.5~1.5 之间。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">尺寸变化参数。</param>
    private void SpectrumKeyboardBacklightControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_device.LayoutTransform is not ScaleTransform scaleTransform)
            return;

        // 目标宽度取容器宽度的 75%，再换算为相对于示意图原始宽度的缩放比。
        var target = 0.75 * ActualWidth / _device.ActualWidth;
        var scale = Math.Clamp(target, 0.5, 1.5);

        scaleTransform.ScaleX = scale;
        scaleTransform.ScaleY = scale;
    }

    /// <summary>
    /// 特殊键变化回调：亮度键触发亮度同步，预设键触发预设同步；冲突软件运行或页面不可见时忽略。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">包含触发按键的参数。</param>
    private void Listener_Changed(object? sender, SpecialKeyListener.ChangedEventArgs e) => Dispatcher.Invoke(async () =>
    {
        if (!IsLoaded || !IsVisible)
            return;

        if (!await _controller.IsSupportedAsync())
            return;

        // Vantage 占用灯控时硬件状态不可靠，跳过刷新。
        if (await _vantageDisabler.GetStatusAsync() == SoftwareStatus.Enabled)
            return;

        switch (e.SpecialKey)
        {
            case SpecialKey.SpectrumBacklightOff
                or SpecialKey.SpectrumBacklight1
                or SpecialKey.SpectrumBacklight2
                or SpecialKey.SpectrumBacklight3:
                // 亮度档位快捷键：同步亮度滑杆显示。
                await RefreshBrightnessAsync();
                break;
            case SpecialKey.SpectrumPreset1
                or SpecialKey.SpectrumPreset2
                or SpecialKey.SpectrumPreset3
                or SpecialKey.SpectrumPreset4
                or SpecialKey.SpectrumPreset5
                or SpecialKey.SpectrumPreset6:
                // 预设快捷键：同步当前预设及灯效列表。
                await RefreshProfileAsync();
                break;
        }
    });

    /// <summary>
    /// 亮度滑杆值变化：经 250ms 节流后仅在数值确实变化时写入硬件。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">值变化参数。</param>
    private async void BrightnessSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => await _changeBrightnessDispatcher.DispatchAsync(async () =>
    {
        await Dispatcher.InvokeAsync(async () =>
        {
            var value = (int)_brightnessSlider.Value;
            // 与硬件当前值相同则跳过写入，减少无谓的 HID 通信。
            if (await _controller.GetBrightnessAsync() != value)
                await _controller.SetBrightnessAsync(value);
        });
    });

    /// <summary>
    /// 预设按钮点击：切换预设期间暂停动画与交互，完成后重新描述预设并恢复动画。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private async void ProfileButton_OnClick(object sender, RoutedEventArgs e)
    {
        await StopAnimationAsync();

        if ((sender as RadioButton)?.Tag is not int profile)
            return;

        // 写硬件期间禁用亮度与预设按钮，避免并发切换。
        _brightnessSlider.IsEnabled = false;
        foreach (var profileButton in ProfileButtons)
            profileButton.IsEnabled = false;

        if (await _controller.GetProfileAsync() != profile)
        {
            await _controller.SetProfileAsync(profile);
            await RefreshProfileDescriptionAsync();
        }

        foreach (var profileButton in ProfileButtons)
            profileButton.IsEnabled = true;
        _brightnessSlider.IsEnabled = true;

        if (IsVisible)
            await StartAnimationAsync();
    }

    /// <summary>
    /// 框选回调：对未被选中的按键按是否落入选择框中心范围来决定选中状态。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">包含选择区域判定方法的参数。</param>
    private void SelectableControl_Selected(object? sender, SelectableControl.SelectedEventArgs e)
    {
        // 仅增量勾选未被选中的按键，已选中的保持原状。
        foreach (var button in _device.GetVisibleButtons().Where(b => !(b.IsChecked ?? false)))
            button.IsChecked = e.ContainsCenter(button);
    }

    /// <summary>
    /// “全选”按钮点击：选中所有可见按键。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void SelectAll_Click(object sender, RoutedEventArgs e) => SelectAllButtons();

    /// <summary>
    /// “取消全选”按钮点击：取消所有可见按键的选中状态。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void DeselectAll_Click(object sender, RoutedEventArgs e) => DeselectAllButtons();

    /// <summary>
    /// 切换键盘布局：按 ANSI → ISO → JIS → ANSI 循环切换，持久化后重建设备示意图。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private async void SwitchKeyboardLayout_Click(object sender, RoutedEventArgs e)
    {
        await StopAnimationAsync();

        var buttons = _device.GetVisibleButtons();
        foreach (var button in buttons)
            button.IsChecked = false;

        // 循环切换到下一个物理键盘布局。
        var currentKeyboardLayout = _settings.Store.KeyboardLayout;
        var keyboardLayout = currentKeyboardLayout switch
        {
            KeyboardLayout.Ansi => KeyboardLayout.Iso,
            KeyboardLayout.Iso => KeyboardLayout.Jis,
            KeyboardLayout.Jis => KeyboardLayout.Ansi,
            _ => throw new ArgumentException(nameof(currentKeyboardLayout))
        };

        _settings.Store.KeyboardLayout = keyboardLayout;
        _settings.SynchronizeStore();

        var (spectrumLayout, _, keys) = await _controller.GetKeyboardLayoutAsync();

        _device.SetLayout(spectrumLayout, keyboardLayout, keys);

        if (IsVisible)
            await StartAnimationAsync();
    }

    /// <summary>
    /// 导出当前预设的灯效描述为 JSON 文件，失败时以提示条告知用户。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var sfd = new SaveFileDialog
            {
                Title = Resource.Export,
                // 该 CLSID 表示“此电脑”，作为对话框初始目录。
                InitialDirectory = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
                Filter = "Json Files (.json)|*.json",
            };

            var result = sfd.ShowDialog();

            if (!result.HasValue || !result.Value)
                return;

            var profile = await _controller.GetProfileAsync();
            await _controller.ExportProfileDescriptionAsync(profile, sfd.FileName);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't export profile.", ex);

            await SnackbarHelper.ShowAsync(Resource.SpectrumKeyboardBacklightControl_ExportProfileError_Title, Resource.SpectrumKeyboardBacklightControl_ExportProfileError_Message, SnackbarType.Error);
        }
    }

    /// <summary>
    /// 从 JSON 文件导入灯效描述到当前预设，成功后刷新灯效列表。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var ofd = new OpenFileDialog
            {
                Title = Resource.Import,
                // 该 CLSID 表示“此电脑”，作为对话框初始目录。
                InitialDirectory = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
                Filter = "Json Files (.json)|*.json",
                CheckFileExists = true,
            };

            var result = ofd.ShowDialog() ?? false;
            if (!result)
                return;

            var profile = await _controller.GetProfileAsync();
            await _controller.ImportProfileDescription(profile, ofd.FileName);

            await RefreshProfileDescriptionAsync();
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't import profile.", ex);

            await SnackbarHelper.ShowAsync(Resource.SpectrumKeyboardBacklightControl_ImportProfileError_Title, Resource.SpectrumKeyboardBacklightControl_ImportProfileError_Message, SnackbarType.Error);
        }
    }

    /// <summary>
    /// “添加灯效”按钮点击：以当前选中按键为作用范围打开灯效编辑窗口；未选中任何按键时默认全选。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void AddEffectButton_Click(object sender, RoutedEventArgs e)
    {
        var buttons = _device.GetVisibleButtons().ToArray();
        var checkedButtons = buttons.Where(b => b.IsChecked ?? false).ToArray();

        // 没有选中按键时先把全部按键选中，再以全部按键作为新灯效范围。
        if (checkedButtons.IsEmpty())
        {
            SelectAllButtons();
            checkedButtons = buttons;
        }

        var keyCodes = checkedButtons.Select(b => b.KeyCode).ToArray();

        // 键盘按键码用于编辑窗口中提供“全键盘”快捷选择。
        var allKeyboardKeyCodes = _device.GetVisibleKeyboardButtons()
            .Select(b => b.KeyCode)
            .ToArray();

        CreateEffect(keyCodes, allKeyboardKeyCodes);
    }

    /// <summary>
    /// “恢复默认”按钮点击：取消按键选中并将当前预设重置为出厂灯效。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private async void ResetToDefaultButton_Click(object sender, RoutedEventArgs e) => await ResetToDefaultAsync();

    /// <summary>
    /// 刷新界面：检测支持性与 Vantage 冲突状态，应用键盘布局并同步亮度、预设与动画。
    /// </summary>
    /// <returns>异步刷新任务。</returns>
    protected override async Task OnRefreshAsync()
    {
        if (!await _controller.IsSupportedAsync())
            throw new InvalidOperationException("Spectrum Keyboard does not seem to be supported");

        var vantageStatus = await _vantageDisabler.GetStatusAsync();

        // Vantage 占用灯控时禁用整个内容区并显示提示条。
        if (vantageStatus == SoftwareStatus.Enabled)
        {
            _vantageWarningInfoBar.IsOpen = true;

            _device.SetLayout(SpectrumLayout.Full, KeyboardLayout.Ansi, []);
            _content.IsEnabled = false;

            _noEffectsText.Visibility = Visibility.Collapsed;
            return;
        }

        _vantageWarningInfoBar.IsOpen = false;

        var (spectrumLayout, keyboardLayout, keys) = await _controller.GetKeyboardLayoutAsync();

        // 首次使用时以硬件上报的布局作为默认值并持久化，之后沿用用户选择。
        if (!_settings.Store.KeyboardLayout.HasValue)
        {
            _settings.Store.KeyboardLayout = keyboardLayout;
            _settings.SynchronizeStore();
        }
        else
        {
            keyboardLayout = _settings.Store.KeyboardLayout.Value;
        }

        _device.SetLayout(spectrumLayout, keyboardLayout, keys);

        _content.IsEnabled = true;

        await RefreshBrightnessAsync();
        await RefreshProfileAsync();

        if (IsVisible)
            await StartAnimationAsync();
    }

    /// <summary>
    /// 加载完成回调，此控件无需额外处理。
    /// </summary>
    protected override void OnFinishedLoading() { }

    /// <summary>
    /// 按灯效作用范围更新按键选中状态：全体灯效全选，否则仅选中该灯效涉及的按键。
    /// </summary>
    /// <param name="effect">要反映的灯效。</param>
    private void SelectButtons(SpectrumKeyboardBacklightEffect effect)
    {
        if (effect.Type.IsAllLightsEffect())
        {
            SelectAllButtons();
            return;
        }

        DeselectAllButtons();

        foreach (var button in _device.GetVisibleButtons())
        {
            if (!effect.Keys.Contains(button.KeyCode))
                continue;

            button.IsChecked = true;
        }
    }

    /// <summary>
    /// 选中全部可见按键。
    /// </summary>
    private void SelectAllButtons()
    {
        foreach (var button in _device.GetVisibleButtons())
            button.IsChecked = true;
    }

    /// <summary>
    /// 取消全部可见按键的选中状态。
    /// </summary>
    private void DeselectAllButtons()
    {
        foreach (var button in _device.GetVisibleButtons())
            button.IsChecked = false;
    }

    /// <summary>
    /// 启动按键颜色轮询动画：先停止既有循环，再以新的取消令牌启动刷新任务。
    /// </summary>
    /// <returns>异步操作任务。</returns>
    private async Task StartAnimationAsync()
    {
        // 加锁保证启动/停止成对执行，避免两个循环同时存在。
        using (await _startStopAnimationLock.LockAsync())
        {
            await StopAnimationAsync();

            if (_refreshStateCancellationTokenSource is not null)
                await _refreshStateCancellationTokenSource.CancelAsync();

            _refreshStateCancellationTokenSource = new();

            _refreshStateTask = RefreshStateAsync(_refreshStateCancellationTokenSource.Token);
        }
    }

    /// <summary>
    /// 停止按键颜色轮询动画：取消令牌并等待循环任务退出。
    /// </summary>
    /// <returns>异步操作任务。</returns>
    private async Task StopAnimationAsync()
    {
        using (await _startStopAnimationLock.LockAsync())
        {
            if (_refreshStateCancellationTokenSource is not null)
                await _refreshStateCancellationTokenSource.CancelAsync();

            _refreshStateCancellationTokenSource = new();

            if (_refreshStateTask is not null)
                await _refreshStateTask;

            _refreshStateTask = null;
        }
    }

    /// <summary>
    /// 按键颜色回显循环：按固定间隔读取硬件状态并更新示意图上每个按键的底色，直到取消或页面隐藏。
    /// </summary>
    /// <param name="token">用于取消循环的令牌。</param>
    /// <returns>循环结束时的任务。</returns>
    private async Task RefreshStateAsync(CancellationToken token)
    {
        var buttons = _device.GetVisibleButtons().ToArray();

        if (buttons.Length < 1)
            return;

        var firstCheck = true;

        try
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();

                if (!IsVisible)
                    break;

                // 先启动延时再读取状态，使读取与等待重叠，稳定维持约 50ms 的刷新节奏。
                var delay = Task.Delay(_refreshStateInterval, token);
                var state = await Task.Run(() => _controller.GetStateAsync(!firstCheck), token);

                foreach (var button in buttons)
                {
                    if (!state.TryGetValue(button.KeyCode, out var rgb))
                    {
                        button.Color = null;
                        continue;
                    }

                    // 三个通道都接近 0 视为熄灭，清空底色以隐藏该按键的发光效果。
                    if (rgb is { R: < 1, G: < 1, B: < 1 })
                    {
                        button.Color = null;
                        continue;
                    }

                    // 硬件上报的是 RGB 8bit 分量，直接映射为 WPF 颜色显示。
                    button.Color = Color.FromRgb(rgb.R, rgb.G, rgb.B);
                }

                await delay;

                firstCheck = false;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to refresh state.", ex);
        }
        finally
        {
            // 循环结束时清除所有按键底色，避免留下过期颜色。
            foreach (var button in buttons)
                button._background.Background = null;
        }
    }

    /// <summary>
    /// 读取硬件当前亮度并同步到滑杆。
    /// </summary>
    /// <returns>异步操作任务。</returns>
    private async Task RefreshBrightnessAsync()
    {
        _brightnessSlider.Value = await _controller.GetBrightnessAsync();
    }

    /// <summary>
    /// 读取当前预设并勾选对应按钮，随后刷新灯效列表。
    /// </summary>
    /// <returns>异步操作任务。</returns>
    private async Task RefreshProfileAsync()
    {
        var profile = await _controller.GetProfileAsync();
        var profileButton = ProfileButtons.FirstOrDefault(pb => pb.Tag.Equals(profile));
        if (profileButton is null)
            return;

        profileButton.IsChecked = true;

        await RefreshProfileDescriptionAsync();
    }

    /// <summary>
    /// 读取当前预设的灯效列表并重建界面上的灯效卡片，同时控制“暂无灯效”提示的可见性。
    /// </summary>
    /// <returns>异步操作任务。</returns>
    private async Task RefreshProfileDescriptionAsync()
    {
        var profile = await _controller.GetProfileAsync();
        var (_, effects) = await _controller.GetProfileDescriptionAsync(profile);

        DeleteAllEffects();

        foreach (var effect in effects)
        {
            var control = CreateEffectControl(effect);
            _effects.Children.Add(control);
        }

        _noEffectsText.Visibility = effects.IsEmpty() ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// 把界面上的灯效列表写回当前预设，失败时以提示条告知用户。
    /// </summary>
    /// <returns>异步操作任务。</returns>
    private async Task ApplyProfileAsync()
    {
        var profile = await _controller.GetProfileAsync();
        var effects = _effects.Children.OfType<SpectrumKeyboardEffectControl>().Select(c => c.Effect).ToArray();

        try
        {
            await _controller.SetProfileDescriptionAsync(profile, effects);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't apply profile.", ex);

            await SnackbarHelper.ShowAsync(Resource.SpectrumKeyboardBacklightControl_ApplyProfileError_Title, Resource.SpectrumKeyboardBacklightControl_ApplyProfileError_Message, SnackbarType.Error);
        }

        await RefreshProfileDescriptionAsync();
    }

    /// <summary>
    /// 将当前预设恢复为出厂默认灯效并取消按键选中。
    /// </summary>
    /// <returns>异步操作任务。</returns>
    private async Task ResetToDefaultAsync()
    {
        DeselectAllButtons();

        var profile = await _controller.GetProfileAsync();
        await _controller.SetProfileDefaultAsync(profile);

        await RefreshProfileDescriptionAsync();
    }

    /// <summary>
    /// 打开灯效编辑窗口以创建新灯效，用户确认后通过回调写入预设。
    /// </summary>
    /// <param name="keyCodes">本次可选中的按键码集合。</param>
    /// <param name="allKeyboardKeyCodes">全部键盘按键码，供编辑窗口提供全键盘快捷操作。</param>
    private void CreateEffect(ushort[] keyCodes, ushort[] allKeyboardKeyCodes)
    {
        var window = new SpectrumKeyboardBacklightEditEffectWindow(keyCodes, allKeyboardKeyCodes) { Owner = Window.GetWindow(this) };
        window.Apply += async (_, e) => await AddEffect(e);
        window.ShowDialog();
    }

    /// <summary>
    /// 根据灯效配置创建灯效卡片，并把点击/编辑/删除事件连接到本面板的处理逻辑。
    /// </summary>
    /// <param name="effect">灯效配置。</param>
    /// <returns>配置完成的灯效卡片控件。</returns>
    private SpectrumKeyboardEffectControl CreateEffectControl(SpectrumKeyboardBacklightEffect effect)
    {
        var control = new SpectrumKeyboardEffectControl(effect);
        control.Click += (_, _) => SelectButtons(effect);
        control.Edit += (_, _) => EditEffect(control);
        control.Delete += async (_, _) => await DeleteEffectAsync(control);
        return control;
    }

    /// <summary>
    /// 将新创建的灯效加入列表并立即应用。
    /// </summary>
    /// <param name="effect">要添加的灯效。</param>
    /// <returns>异步操作任务。</returns>
    private async Task AddEffect(SpectrumKeyboardBacklightEffect effect)
    {
        DeselectAllButtons();

        var control = CreateEffectControl(effect);
        _effects.Children.Add(control);

        await ApplyProfileAsync();
    }

    /// <summary>
    /// 打开灯效编辑窗口修改指定灯效，用户确认后以新灯效原位替换。
    /// </summary>
    /// <param name="effectControl">被编辑的灯效卡片。</param>
    private void EditEffect(SpectrumKeyboardEffectControl effectControl)
    {
        var keyCodes = _device.GetVisibleButtons()
            .Select(b => b.KeyCode)
            .ToArray();
        var allKeyboardKeyCodes = _device.GetVisibleKeyboardButtons()
            .Select(b => b.KeyCode)
            .ToArray();

        var window = new SpectrumKeyboardBacklightEditEffectWindow(effectControl.Effect, keyCodes, allKeyboardKeyCodes) { Owner = Window.GetWindow(this) };
        window.Apply += async (_, e) => await ReplaceEffectAsync(effectControl, e);
        window.ShowDialog();
    }

    /// <summary>
    /// 用编辑后的灯效替换原卡片（保持列表位置），并立即应用预设。
    /// </summary>
    /// <param name="effectControl">原灯效卡片。</param>
    /// <param name="effect">编辑后的灯效。</param>
    /// <returns>异步操作任务。</returns>
    private async Task ReplaceEffectAsync(UIElement effectControl, SpectrumKeyboardBacklightEffect effect)
    {
        DeselectAllButtons();

        var control = new SpectrumKeyboardEffectControl(effect);
        control.Click += (_, _) => SelectButtons(effect);
        control.Edit += (_, _) => EditEffect(control);
        control.Delete += async (_, _) => await DeleteEffectAsync(control);

        // 原位替换以保持灯效顺序；找不到原项时追加到末尾。
        var index = _effects.Children.IndexOf(effectControl);
        if (index < 0)
        {
            _effects.Children.Add(control);
        }
        else
        {
            _effects.Children.RemoveAt(index);
            _effects.Children.Insert(index, control);
        }

        await ApplyProfileAsync();
    }

    /// <summary>
    /// 移除指定灯效卡片并立即应用预设。
    /// </summary>
    /// <param name="effectControl">要删除的灯效卡片。</param>
    /// <returns>异步操作任务。</returns>
    private async Task DeleteEffectAsync(UIElement effectControl)
    {
        _effects.Children.Remove(effectControl);

        await ApplyProfileAsync();
    }

    /// <summary>
    /// 清空灯效列表中的全部卡片（仅移除界面元素，不回写硬件）。
    /// </summary>
    private void DeleteAllEffects() => _effects.Children.Clear();
}

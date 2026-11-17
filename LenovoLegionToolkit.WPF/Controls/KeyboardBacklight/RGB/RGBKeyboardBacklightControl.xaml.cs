using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.WPF.Extensions;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;
using Button = Wpf.Ui.Controls.Button;
using MenuItem = System.Windows.Controls.MenuItem;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.RGB;

/// <summary>
/// RGB 键盘背光主控制面板：提供预设切换、亮度/灯效/速度选择与四分区取色，
/// 并将颜色按 RGB 8bit 值写入控制器；窗口较窄时自动切换分区布局。
/// </summary>
public partial class RGBKeyboardBacklightControl
{
    /// <summary>预设按钮集合（关闭 + 4 个自定义预设）。</summary>
    private Button[] PresetButtons => [_offPresetButton, _preset1Button, _preset2Button, _preset3Button, _preset4Button];

    /// <summary>四个分区的取色控件集合。</summary>
    private ColorPickerControl[] Zones => [_zone1ColorPicker, _zone2ColorPicker, _zone3ColorPicker, _zone4ColorPicker];

    /// <summary>RGB 键盘背光控制器。</summary>
    private readonly RGBKeyboardBacklightController _controller = IoCContainer.Resolve<RGBKeyboardBacklightController>();
    /// <summary>RGB 键盘背光变化监听器。</summary>
    private readonly RGBKeyboardBacklightListener _listener = IoCContainer.Resolve<RGBKeyboardBacklightListener>();
    /// <summary>Vantage 软件禁用器，用于检测冲突软件是否仍在运行。</summary>
    private readonly VantageDisabler _vantageDisabler = IoCContainer.Resolve<VantageDisabler>();

    /// <summary>
    /// 刷新期间保持交互可用：切换预设/颜色是即时回写硬件的操作，无需锁定界面。
    /// </summary>
    protected override bool DisablesWhileRefreshing => false;

    /// <summary>
    /// 初始化控件并加载 XAML 视图，订阅背光变化、尺寸变化与跨模块消息。
    /// </summary>
    public RGBKeyboardBacklightControl()
    {
        InitializeComponent();

        _listener.Changed += Listener_Changed;

        SizeChanged += RGBKeyboardBacklightControl_SizeChanged;

        // 其他入口修改背光后广播消息，这里在页面可见时重新读取状态。
        MessagingCenter.Subscribe<RGBKeyboardBacklightChangedMessage>(this, () => Dispatcher.InvokeTask(async () =>
        {
            if (!IsVisible)
                return;

            await RefreshAsync();
        }));
    }

    /// <summary>
    /// 背光变化回调：页面可见时切回 UI 线程重新读取硬件状态。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数（无附加数据）。</param>
    private void Listener_Changed(object? sender, EventArgs e) => Dispatcher.Invoke(async () =>
    {
        if (!IsLoaded || !IsVisible)
            return;

        await RefreshAsync();
    });

    /// <summary>
    /// 尺寸变化回调：按宽度切换分区布局（宽屏四列并排，窄屏单列堆叠）。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">尺寸变化参数。</param>
    private void RGBKeyboardBacklightControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 仅宽度改变时才需要重排布局。
        if (!e.WidthChanged)
            return;

        // 950px 以上可容纳四列分区，否则改为纵向堆叠。
        if (e.NewSize.Width > 950)
            Expand();
        else
            Collapse();
    }

    /// <summary>
    /// 预设按钮点击：切换到该按钮对应的预设并回读最新状态。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private async void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        // 已是当前预设（Primary 外观）或发送者类型不符时忽略点击。
        if (sender is not Button presetButton || presetButton.Appearance == ControlAppearance.Primary)
            return;

        var selectedPreset = (RGBKeyboardBacklightPreset)presetButton.Tag;
        var state = await _controller.GetStateAsync();
        await _controller.SetStateAsync(new(selectedPreset, state.Presets));

        await RefreshAsync();
    }

    /// <summary>
    /// “同步分区颜色”菜单项点击：把被右键的分区颜色复制到其余所有分区并保存。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private async void SynchroniseZonesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // 通过可视化树定位被右键的分区取色控件。
        if (sender is not MenuItem { Parent: ContextMenu { PlacementTarget: CardControl { Content: ColorPickerControl pickerControl } } })
            return;

        foreach (var zone in Zones)
            zone.SelectedColor = pickerControl.SelectedColor;

        await SaveState();
        await RefreshAsync();
    }

    /// <summary>
    /// 任意下拉卡片变更回调：保存当前预设配置并刷新界面。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">事件参数（无附加数据）。</param>
    private async void CardControl_Changed(object? sender, EventArgs e)
    {
        await SaveState();
        await RefreshAsync();
    }

    /// <summary>
    /// 刷新界面：检测支持性与 Vantage 冲突状态，随后按当前预设同步各控件的值与可用性。
    /// </summary>
    /// <returns>异步刷新任务。</returns>
    protected override async Task OnRefreshAsync()
    {
        if (!await _controller.IsSupportedAsync())
            throw new InvalidOperationException("RGB Keyboard does not seem to be supported");

        var vantageStatus = await _vantageDisabler.GetStatusAsync();

        // Vantage 占用键盘灯控时，禁用全部交互并显示提示条。
        if (vantageStatus == SoftwareStatus.Enabled)
        {
            _vantageWarningInfoBar.IsOpen = true;

            foreach (var presetButton in PresetButtons)
                presetButton.IsEnabled = false;

            _brightnessControl.IsEnabled = false;
            _effectControl.IsEnabled = false;

            _zone1ColorPicker.Visibility = Visibility.Hidden;
            _zone2ColorPicker.Visibility = Visibility.Hidden;
            _zone3ColorPicker.Visibility = Visibility.Hidden;
            _zone4ColorPicker.Visibility = Visibility.Hidden;

            _speedControl.IsEnabled = false;
            _zone1Control.IsEnabled = false;
            _zone2Control.IsEnabled = false;
            _zone3Control.IsEnabled = false;
            _zone4Control.IsEnabled = false;

            Visibility = Visibility.Visible;

            return;
        }

        var state = await _controller.GetStateAsync();

        // 当前预设按钮使用主色外观高亮，其余为次色。
        foreach (var presetButton in PresetButtons)
        {
            var buttonPreset = (RGBKeyboardBacklightPreset)presetButton.Tag;
            var selected = state.SelectedPreset == buttonPreset;
            presetButton.Appearance = selected ? ControlAppearance.Primary : ControlAppearance.Secondary;
        }

        _vantageWarningInfoBar.IsOpen = false;

        foreach (var presetButton in PresetButtons)
            presetButton.IsEnabled = true;

        // 预设为“关闭”时没有可调项，全部禁用并隐藏取色器。
        if (state.SelectedPreset == RGBKeyboardBacklightPreset.Off)
        {
            _effectControl.IsEnabled = false;
            _speedControl.IsEnabled = false;
            _brightnessControl.IsEnabled = false;

            _zone1ColorPicker.Visibility = Visibility.Hidden;
            _zone2ColorPicker.Visibility = Visibility.Hidden;
            _zone3ColorPicker.Visibility = Visibility.Hidden;
            _zone4ColorPicker.Visibility = Visibility.Hidden;

            _zone1Control.IsEnabled = false;
            _zone2Control.IsEnabled = false;
            _zone3Control.IsEnabled = false;
            _zone4Control.IsEnabled = false;

            return;
        }

        // 预设缺失时回退到内置默认配置。
        var preset = state.Presets.GetValueOrDefault(state.SelectedPreset, RGBKeyboardBacklightBacklightPresetDescription.Default);

        // 静态灯效没有速度含义；分区取色仅在静态与呼吸灯效下有效。
        var speedEnabled = preset.Effect is not RGBKeyboardBacklightEffect.Static;
        var zonesEnabled = preset.Effect is RGBKeyboardBacklightEffect.Static or RGBKeyboardBacklightEffect.Breath;

        _brightnessControl.SetItems(Enum.GetValues<RGBKeyboardBacklightBrightness>(), preset.Brightness, v => v.GetDisplayName());
        _effectControl.SetItems(Enum.GetValues<RGBKeyboardBacklightEffect>(), preset.Effect, v => v.GetDisplayName());
        if (speedEnabled)
            _speedControl.SetItems(Enum.GetValues<RGBKeyboardBacklightSpeed>(), preset.Speed, v => v.GetDisplayName());

        if (zonesEnabled)
        {
            // 颜色的内部表示转为 ColorPicker 使用的 WPF 颜色。
            _zone1ColorPicker.SelectedColor = preset.Zone1.ToColor();
            _zone2ColorPicker.SelectedColor = preset.Zone2.ToColor();
            _zone3ColorPicker.SelectedColor = preset.Zone3.ToColor();
            _zone4ColorPicker.SelectedColor = preset.Zone4.ToColor();

            _zone1ColorPicker.Visibility = Visibility.Visible;
            _zone2ColorPicker.Visibility = Visibility.Visible;
            _zone3ColorPicker.Visibility = Visibility.Visible;
            _zone4ColorPicker.Visibility = Visibility.Visible;
        }
        else
        {
            _zone1ColorPicker.Visibility = Visibility.Hidden;
            _zone2ColorPicker.Visibility = Visibility.Hidden;
            _zone3ColorPicker.Visibility = Visibility.Hidden;
            _zone4ColorPicker.Visibility = Visibility.Hidden;
        }

        _brightnessControl.IsEnabled = true;
        _effectControl.IsEnabled = true;
        _speedControl.IsEnabled = speedEnabled;

        _zone1Control.IsEnabled = zonesEnabled;
        _zone2Control.IsEnabled = zonesEnabled;
        _zone3Control.IsEnabled = zonesEnabled;
        _zone4Control.IsEnabled = zonesEnabled;
    }

    /// <summary>
    /// 加载完成回调，此控件无需额外处理。
    /// </summary>
    protected override void OnFinishedLoading() { }

    /// <summary>
    /// 将界面上的灯效、速度、亮度与四个分区颜色写入当前预设并回写控制器。
    /// </summary>
    /// <returns>异步保存任务。</returns>
    private async Task SaveState()
    {
        var state = await _controller.GetStateAsync();

        var selectedPreset = state.SelectedPreset;
        var presets = state.Presets;

        // 关闭状态下没有预设可编辑，直接返回。
        if (selectedPreset == RGBKeyboardBacklightPreset.Off)
            return;

        // 颜色由 ColorPicker 的 WPF 颜色转为控制器所需的 RGB 结构。
        // 速度下拉被禁用时会清空选项，此时保留预设中已保存的速度，避免写回默认值。
        var speed = _speedControl.IsEnabled ? _speedControl.SelectedItem : presets[selectedPreset].Speed;
        presets[selectedPreset] = new(_effectControl.SelectedItem,
            speed,
            _brightnessControl.SelectedItem,
            _zone1ColorPicker.SelectedColor.ToRGBColor(),
            _zone2ColorPicker.SelectedColor.ToRGBColor(),
            _zone3ColorPicker.SelectedColor.ToRGBColor(),
            _zone4ColorPicker.SelectedColor.ToRGBColor());

        await _controller.SetStateAsync(new(selectedPreset, presets));
    }

    /// <summary>
    /// 宽屏布局：四个分区并排放在同一行，各占一列。
    /// </summary>
    private void Expand()
    {
        Grid.SetColumn(_zone1Control, 0);
        Grid.SetColumn(_zone2Control, 1);
        Grid.SetColumn(_zone3Control, 2);
        Grid.SetColumn(_zone4Control, 3);

        Grid.SetRow(_zone1Control, 5);
        Grid.SetRow(_zone2Control, 5);
        Grid.SetRow(_zone3Control, 5);
        Grid.SetRow(_zone4Control, 5);

        Grid.SetColumnSpan(_zone1Control, 1);
        Grid.SetColumnSpan(_zone2Control, 1);
        Grid.SetColumnSpan(_zone3Control, 1);
        Grid.SetColumnSpan(_zone4Control, 1);
    }

    /// <summary>
    /// 窄屏布局：四个分区纵向堆叠，各自横跨整行。
    /// </summary>
    private void Collapse()
    {
        Grid.SetColumn(_zone1Control, 0);
        Grid.SetColumn(_zone2Control, 0);
        Grid.SetColumn(_zone3Control, 0);
        Grid.SetColumn(_zone4Control, 0);

        Grid.SetRow(_zone1Control, 5);
        Grid.SetRow(_zone2Control, 6);
        Grid.SetRow(_zone3Control, 7);
        Grid.SetRow(_zone4Control, 8);

        Grid.SetColumnSpan(_zone1Control, 4);
        Grid.SetColumnSpan(_zone2Control, 4);
        Grid.SetColumnSpan(_zone3Control, 4);
        Grid.SetColumnSpan(_zone4Control, 4);
    }
}

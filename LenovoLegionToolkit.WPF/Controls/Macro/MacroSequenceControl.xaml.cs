using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Macro;
using LenovoLegionToolkit.WPF.Extensions;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Windows.Macro;

namespace LenovoLegionToolkit.WPF.Controls.Macro;

/// <summary>
/// 宏序列编辑控件：录制/展示某个按键或鼠标按键触发的一组宏事件，并编辑重复次数、忽略延迟等选项。
/// </summary>
public partial class MacroSequenceControl
{
    // 宏控制器：负责录制与序列的存取。
    private readonly MacroController _controller = IoCContainer.Resolve<MacroController>();

    // 录制过程中显示的提示窗口。
    private MacroRecordingWindow? _recordingWindow;

    // 当前正在编辑的宏标识（哪个按键触发）。
    private MacroIdentifier _macroIdentifier;

    // 刷新界面期间置位，避免控件赋值触发回写造成递归保存。
    private bool _isRefreshing;

    /// <summary>
    /// 初始化控件并订阅录制事件。
    /// </summary>
    public MacroSequenceControl()
    {
        InitializeComponent();

        _controller.RecorderReceived += Controller_RecorderReceived;
        _controller.RecorderStopped += Controller_RecorderStopped;
    }

    /// <summary>
    /// 切换到指定宏标识：停止录制、载入该宏的序列并刷新全部编辑控件与事件列表。
    /// </summary>
    /// <param name="macroIdentifier">要编辑的宏标识。</param>
    public void Set(MacroIdentifier macroIdentifier)
    {
        // 刷新期间屏蔽 Save，防止控件赋值反向触发保存。
        _isRefreshing = true;

        _macroIdentifier = macroIdentifier;

        _controller.StopRecording();

        Mouse.OverrideCursor = null;

        var sequence = _controller.GetSequences().GetValueOrDefault(_macroIdentifier);
        var sequenceHasEvents = sequence.Events?.Length > 0;

        // 无事件时禁用依赖序列的选项，避免编辑无意义的设置。
        _repeatCard.IsEnabled = sequenceHasEvents;
        _ignoreDelaysCard.IsEnabled = sequenceHasEvents;
        _interruptOnOtherKeyCard.IsEnabled = sequenceHasEvents;

        // 录制范围选项：仅键盘 / 键盘+鼠标 / 键盘+鼠标+移动。
        _settingsComboBox.SetItems([MacroRecorderSettings.Keyboard, MacroRecorderSettings.Keyboard | MacroRecorderSettings.Mouse, MacroRecorderSettings.Keyboard | MacroRecorderSettings.Mouse | MacroRecorderSettings.Movement],
            MacroRecorderSettings.Keyboard,
            v => v switch
            {
                MacroRecorderSettings.Keyboard => Resource.MacroSequenceControl_Keyboard,
                MacroRecorderSettings.Keyboard | MacroRecorderSettings.Mouse => Resource.MacroSequenceControl_KeyboardMouse,
                MacroRecorderSettings.Keyboard | MacroRecorderSettings.Mouse | MacroRecorderSettings.Movement => Resource.MacroSequenceControl_KeyboardMouseMovement,
                _ => string.Empty
            });
        // 重复次数限制在 1-10，1 显示为"不重复"。
        _repeatComboBox.SetItems(MacroController.AllowedRepeatCounts,
            Math.Clamp(sequence.RepeatCount, 1, 10),
            v => v == 1 ? Resource.MacroSequenceControl_DontRepeat : v.ToString());
        _ignoreDelaysToggle.IsChecked = sequence.IgnoreDelays;
        _interruptOnOtherKeyToggle.IsChecked = sequence.InterruptOnOtherKey;

        _recordButton.IsEnabled = true;
        _clearButton.Visibility = sequenceHasEvents ? Visibility.Visible : Visibility.Collapsed;

        _macroEventsPanel.Children.Clear();
        foreach (var macroEvent in sequence.Events ?? [])
            CreateControl(macroEvent);

        _isRefreshing = false;
    }

    /// <summary>
    /// 根据事件类型创建并追加展示控件：连续鼠标移动合并到同一个多事件控件中，其余各占一个条目。
    /// </summary>
    private void CreateControl(MacroEvent macroEvent)
    {
        if (macroEvent.Direction is MacroDirection.Move)
        {
            if (_macroEventsPanel.Children.OfType<AbstractMacroEventControl>().LastOrDefault() is MultiAbstractMacroEventControl last)
            {
                last.Set(macroEvent);
            }
            else
            {
                var macroEventControl = new MultiAbstractMacroEventControl();
                macroEventControl.Set(macroEvent);
                _macroEventsPanel.Children.Add(macroEventControl);
            }
        }
        else
        {
            var macroEventControl = new SingleAbstractMacroEventControl();
            macroEventControl.Set(macroEvent);
            _macroEventsPanel.Children.Add(macroEventControl);
        }
    }

    /// <summary>
    /// 录制器每捕获一个事件就追加到界面。
    /// </summary>
    private void Controller_RecorderReceived(object? sender, MacroController.RecorderReceivedEventArgs e) => CreateControl(e.MacroEvent);

    /// <summary>
    /// 录制结束时关闭提示窗口：被中断则清空序列，否则保存。
    /// </summary>
    private void Controller_RecorderStopped(object? sender, MacroController.RecorderStoppedEventArgs e)
    {
        _recordingWindow?.Close();
        _recordingWindow = null;

        if (e.Interrupted)
            Clear();
        else
            Save();
    }

    /// <summary>
    /// 重复次数变化后立即保存。
    /// </summary>
    private void RepeatComboBox_SelectionChanged(object sender, RoutedEventArgs e) => Save();

    /// <summary>
    /// “忽略延迟”切换后立即保存。
    /// </summary>
    private void IgnoreDelaysToggle_Click(object sender, RoutedEventArgs e) => Save();

    /// <summary>
    /// “其他按键中断”切换后立即保存。
    /// </summary>
    private void InterruptOnOtherKeyToggle_Click(object sender, RoutedEventArgs e) => Save();

    /// <summary>
    /// 清空按钮：清除当前序列。
    /// </summary>
    private void ClearButton_Click(object sender, RoutedEventArgs e) => Clear();

    /// <summary>
    /// 录制按钮：按所选录制范围开始录制。
    /// </summary>
    private async void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_settingsComboBox.TryGetSelectedItem(out MacroRecorderSettings settings))
            return;

        await RecordAsync(settings);
    }

    /// <summary>
    /// 执行录制流程：先清空现有事件并进入等待状态；若包含鼠标移动则先显示 3 秒准备倒计时，随后开始正式录制。
    /// </summary>
    /// <param name="settings">录制范围设置。</param>
    private async Task RecordAsync(MacroRecorderSettings settings)
    {
        _macroEventsPanel.Children.Clear();
        _recordButton.IsEnabled = false;
        _clearButton.Visibility = Visibility.Collapsed;

        Mouse.OverrideCursor = Cursors.Wait;

        if (settings.HasFlag(MacroRecorderSettings.Mouse) && settings.HasFlag(MacroRecorderSettings.Movement))
        {
            _recordingWindow = MacroRecordingWindow.CreatePreparing();
            _recordingWindow.Owner = Window.GetWindow(this);
            _recordingWindow.Show();

            await Task.Delay(TimeSpan.FromSeconds(3));

            _recordingWindow.Close();
        }

        _recordingWindow = MacroRecordingWindow.CreateRecording();
        _recordingWindow.Owner = Window.GetWindow(this);
        _recordingWindow.Show();

        _controller.StartRecording(settings);
    }

    /// <summary>
    /// 清空界面中的事件并保存（保存后事件数组为空）。
    /// </summary>
    private void Clear()
    {
        _macroEventsPanel.Children.Clear();

        Save();
    }

    /// <summary>
    /// 将界面上的选项与事件列表写回控制器中的序列数据，并重新载入以同步界面。
    /// </summary>
    private void Save()
    {
        if (_isRefreshing)
            return;

        Mouse.OverrideCursor = null;

        var repeatCount = _repeatComboBox.TryGetSelectedItem(out int repeat) ? repeat : 1;
        var ignoreDelays = _ignoreDelaysToggle.IsChecked ?? false;
        var interruptOnOtherKey = _interruptOnOtherKeyToggle.IsChecked ?? false;
        var macroEvents = _macroEventsPanel.Children
            .OfType<AbstractMacroEventControl>()
            .SelectMany(c => c.GetEvents())
            .ToArray();

        var sequences = _controller.GetSequences();
        sequences[_macroIdentifier] = new MacroSequence
        {
            RepeatCount = repeatCount,
            IgnoreDelays = ignoreDelays,
            InterruptOnOtherKey = interruptOnOtherKey,
            Events = macroEvents
        };
        _controller.SetSequences(sequences);

        Set(_macroIdentifier);
    }
}

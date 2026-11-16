using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.System;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Windows.Automation.TabItemContent;

/// <summary>
/// 设备连接/断开触发器的配置选项卡，列出系统中的设备并支持按条件筛选与多选。
/// </summary>
public partial class DeviceAutomationPipelineTriggerTabItemContent : IAutomationPipelineTriggerTabItemContent<INativeWindowsMessagePipelineTrigger>
{
    private readonly NativeWindowsMessageListener _listener = IoCContainer.Resolve<NativeWindowsMessageListener>();

    private readonly IDeviceAutomationPipelineTrigger _trigger;

    private readonly HashSet<string> _instanceIds;

    private readonly List<Device> _devices = [];

    private bool _onlySelected;
    private bool _onlyConnected;
    private bool _onlyRemovable = true;

    private CancellationTokenSource? _filterDebounceCancellationTokenSource;

    /// <summary>
    /// 初始化选项卡内容，并复制触发器中的已选设备实例 ID。
    /// </summary>
    /// <param name="trigger">待编辑的设备触发器。</param>
    public DeviceAutomationPipelineTriggerTabItemContent(IDeviceAutomationPipelineTrigger trigger)
    {
        _trigger = trigger;
        _instanceIds = [.. trigger.InstanceIds];

        InitializeComponent();
    }

    /// <summary>
    /// 首次显示时刷新筛选按钮并异步加载设备列表。
    /// </summary>
    private async void DeviceAutomationPipelineTriggerTabItemContent_Initialized(object? sender, EventArgs e)
    {
        RefreshButtons();
        await LoadAsync();
    }

    /// <summary>
    /// 监听系统设备插拔消息，在设备连接或断开时刷新列表。
    /// </summary>
    private async void NativeWindowsMessageListener_Changed(object? sender, NativeWindowsMessageListener.ChangedEventArgs e)
    {
        if (e.Message is not NativeWindowsMessage.DeviceConnected and not NativeWindowsMessage.DeviceDisconnected)
            return;

        await LoadAsync();
    }

    /// <summary>
    /// 筛选输入变化后做 500ms 防抖，避免逐字符触发列表重建。
    /// </summary>
    private async void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        try
        {
            // 取消上一次尚未结束的延迟任务，实现防抖
            if (_filterDebounceCancellationTokenSource is not null)
                await _filterDebounceCancellationTokenSource.CancelAsync();

            _filterDebounceCancellationTokenSource = new();

            await Task.Delay(500, _filterDebounceCancellationTokenSource.Token);

            _content.Children.Clear();
            _scrollViewer.ScrollToHome();

            Reload();
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>
    /// 切换"仅显示已选设备"筛选条件并刷新列表。
    /// </summary>
    private void OnlySelected_Click(object sender, RoutedEventArgs e)
    {
        _onlySelected = !_onlySelected;
        RefreshButtons();
        Reload();
    }

    /// <summary>
    /// 切换"仅显示已连接设备"筛选条件并刷新列表。
    /// </summary>
    private void OnlyConnectedButton_Click(object sender, RoutedEventArgs e)
    {
        _onlyConnected = !_onlyConnected;
        RefreshButtons();
        Reload();
    }

    /// <summary>
    /// 切换"仅显示可移除设备"筛选条件并刷新列表。
    /// </summary>
    private void OnlyRemovableButton_Click(object sender, RoutedEventArgs e)
    {
        _onlyRemovable = !_onlyRemovable;
        RefreshButtons();
        Reload();
    }

    /// <summary>
    /// 在后台枚举系统中所有设备并重新渲染列表，枚举期间临时退订变更事件。
    /// </summary>
    private async Task LoadAsync()
    {
        _listener.Changed -= NativeWindowsMessageListener_Changed;

        _devices.Clear();
        _devices.AddRange(await Task.Run(Devices.GetAll));

        _listener.Changed += NativeWindowsMessageListener_Changed;

        Reload();
    }

    /// <summary>
    /// 清空并按当前筛选条件重建设备列表。
    /// </summary>
    private void Reload()
    {
        _content.Children.Clear();

        if (_devices.Count == 0)
            return;

        var devices = SortAndFilter(_devices);

        foreach (var device in devices)
        {
            var listItem = new ListItem(device, _instanceIds.Contains(device.DeviceInstanceId));
            listItem.Checked += (_, args) =>
            {
                _instanceIds.Add(device.DeviceInstanceId);
                args.Handled = true;
            };
            listItem.Unchecked += (_, args) =>
            {
                _instanceIds.Remove(device.DeviceInstanceId);
                if (_onlySelected)
                    Reload();
                args.Handled = true;
            };
            _content.Children.Add(listItem);
        }
    }

    /// <summary>
    /// 依次应用选中、连接状态、可移除状态与文本筛选，并按名称排序。
    /// </summary>
    private List<Device> SortAndFilter(List<Device> devices)
    {
        var result = devices.AsEnumerable();
        if (_onlySelected)
            result = result.Where(d => _instanceIds.Contains(d.DeviceInstanceId));
        if (_onlyConnected)
            result = result.Where(d => !d.IsDisconnected);
        if (_onlyRemovable)
            result = result.Where(d => d.IsRemovable);

        result = result.OrderBy(d => d.Name);

        if (!string.IsNullOrWhiteSpace(_filterTextBox.Text))
            result = result.Where(p => p.Index.Contains(_filterTextBox.Text, StringComparison.InvariantCultureIgnoreCase));

        return result.ToList();
    }

    /// <summary>
    /// 根据筛选开关的启用状态更新按钮外观（主色表示已启用）。
    /// </summary>
    private void RefreshButtons()
    {
        _onlySelectedButton.Appearance = _onlySelected ? ControlAppearance.Primary : ControlAppearance.Secondary;
        _onlyConnectedButton.Appearance = _onlyConnected ? ControlAppearance.Primary : ControlAppearance.Secondary;
        _onlyRemovableButton.Appearance = _onlyRemovable ? ControlAppearance.Primary : ControlAppearance.Secondary;
    }

    /// <summary>
    /// 生成包含当前所选设备实例 ID 的触发器副本。
    /// </summary>
    /// <returns>更新后的触发器。</returns>
    public INativeWindowsMessagePipelineTrigger GetTrigger() => _trigger.DeepCopy([.. _instanceIds]);

    /// <summary>
    /// 设备列表中的单项控件，展示设备名称与标识并提供勾选状态。
    /// </summary>
    private class ListItem : UserControl
    {
        private readonly CardControl _cardControl = new()
        {
            Margin = new(0, 0, 0, 8)
        };

        private readonly StackPanel _stackPanel = new()
        {
            Orientation = Orientation.Vertical
        };

        private readonly TextBlock _notConnected = new()
        {
            Margin = new(0, 0, 0, 2),
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 12,
            Text = "Not connected"
        };

        private readonly TextBlock _name = new()
        {
            Margin = new(0, 0, 0, 4),
            FontWeight = FontWeights.Medium
        };

        private readonly TextBlock _description = new()
        {
            Margin = new(0, 0, 0, 4)
        };

        private readonly TextBlock _busReportedDeviceDescription = new()
        {
            Margin = new(0, 0, 0, 4)
        };

        private readonly TextBlock _deviceInstanceId = new()
        {
            Margin = new(0, 4, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontSize = 12
        };

        private readonly CheckBox _checkBox = new();

        private readonly Device _device;

        public event RoutedEventHandler Checked
        {
            add => _checkBox.Checked += value;
            remove => _checkBox.Checked -= value;
        }

        public event RoutedEventHandler Unchecked
        {
            add => _checkBox.Unchecked += value;
            remove => _checkBox.Unchecked -= value;
        }

        /// <summary>
        /// 初始化列表项。
        /// </summary>
        /// <param name="device">关联的设备信息。</param>
        /// <param name="isChecked">初始是否勾选。</param>
        public ListItem(Device device, bool isChecked)
        {
            _device = device;

            _checkBox.IsChecked = isChecked;

            InitializeComponent();
        }

        /// <summary>
        /// 按设备信息组装卡片内容，按需显示描述、总线描述与实例 ID。
        /// </summary>
        private void InitializeComponent()
        {
            if (_device.IsDisconnected)
            {
                _notConnected.SetResourceReference(ForegroundProperty, "SystemFillColorCautionBrush");
                _stackPanel.Children.Add(_notConnected);
            }

            _name.Text = _device.Name;
            _stackPanel.Children.Add(_name);

            if (_device.Description.Length > 0 && _device.Description != _device.Name)
            {
                _description.Text = _device.Description;
                _stackPanel.Children.Add(_description);
            }

            if (_device.BusReportedDeviceDescription.Length > 0 &&
                _device.BusReportedDeviceDescription != _device.Name &&
                _device.BusReportedDeviceDescription != _device.Description)
            {
                _busReportedDeviceDescription.Text = _device.BusReportedDeviceDescription;
                _stackPanel.Children.Add(_busReportedDeviceDescription);
            }

            _deviceInstanceId.Text = _device.DeviceInstanceId;
            _deviceInstanceId.SetResourceReference(ForegroundProperty, "TextFillColorSecondaryBrush");
            _stackPanel.Children.Add(_deviceInstanceId);

            _cardControl.Header = _stackPanel;
            _cardControl.Content = _checkBox;

            _cardControl.Click += (_, args) =>
            {
                // 点击复选框本身时不重复切换，避免状态被反转两次
                if (args.Source is CheckBox)
                    return;

                _checkBox.IsChecked = !(_checkBox.IsChecked ?? false);
                args.Handled = true;
            };

            Content = _cardControl;
        }
    }
}

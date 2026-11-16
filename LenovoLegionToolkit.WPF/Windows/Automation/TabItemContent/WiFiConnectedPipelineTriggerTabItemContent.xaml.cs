using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System;
using Wpf.Ui.Common;
using Button = Wpf.Ui.Controls.Button;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace LenovoLegionToolkit.WPF.Windows.Automation.TabItemContent;

/// <summary>
/// Wi-Fi 连接触发器的配置选项卡，维护一组需要匹配的 SSID。
/// </summary>
public partial class WiFiConnectedPipelineTriggerTabItemContent : IAutomationPipelineTriggerTabItemContent<IWiFiConnectedPipelineTrigger>
{
    private readonly IWiFiConnectedPipelineTrigger _trigger;

    /// <summary>
    /// 初始化选项卡内容，并为每个已配置的 SSID 生成输入项。
    /// </summary>
    /// <param name="trigger">待编辑的 Wi-Fi 触发器。</param>
    public WiFiConnectedPipelineTriggerTabItemContent(IWiFiConnectedPipelineTrigger trigger)
    {
        _trigger = trigger;

        InitializeComponent();

        // 至少保留一个空输入项，便于用户直接填写
        trigger.Ssids
            .DefaultIfEmpty(string.Empty)
            .Select(CreateControl)
            .ForEach(c => _ssidStackPanel.Children.Add(c));
    }

    /// <summary>
    /// 收集所有非空的 SSID 并生成触发器副本。
    /// </summary>
    /// <returns>更新后的 Wi-Fi 触发器。</returns>
    public IWiFiConnectedPipelineTrigger GetTrigger()
    {
        var ssids = _ssidStackPanel.Children
            .OfType<ItemControl>()
            .Select(c => c.Text)
            .Distinct()
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArray();
        return _trigger.DeepCopy(ssids);
    }

    /// <summary>
    /// 追加一个新的空 SSID 输入项。
    /// </summary>
    private void AddNetworkNameButton_OnClick(object sender, RoutedEventArgs e)
    {
        _ssidStackPanel.Children.Add(CreateControl(string.Empty));
    }

    /// <summary>
    /// 获取当前已连接的 Wi-Fi 名称并填入最后一个空输入项；若无空项则新增一项。
    /// </summary>
    private void CopyCurrentNetworkNameButton_OnClick(object sender, RoutedEventArgs e)
    {
        var ssid = WiFi.GetConnectedNetworkSsid();
        if (ssid is null)
            return;

        var last = _ssidStackPanel.Children
            .OfType<ItemControl>()
            .LastOrDefault();

        if (last is not null && string.IsNullOrEmpty(last.Text))
            last.Text = ssid;
        else
            _ssidStackPanel.Children.Add(CreateControl(ssid));
    }

    /// <summary>
    /// 创建一个 SSID 输入控件，并处理删除时自动补齐空项的逻辑。
    /// </summary>
    /// <param name="ssid">初始 SSID 文本。</param>
    /// <returns>SSID 输入控件。</returns>
    private UserControl CreateControl(string ssid)
    {
        var control = new ItemControl { Text = ssid };
        control.Delete += (s, _) =>
        {
            if (s is not UIElement element)
                return;

            _ssidStackPanel.Children.Remove(element);

            if (_ssidStackPanel.Children.OfType<ItemControl>().Any())
                return;

            _ssidStackPanel.Children.Add(CreateControl(string.Empty));
        };
        return control;
    }

    /// <summary>
    /// SSID 输入项控件，由文本框与删除按钮组成。
    /// </summary>
    private class ItemControl : UserControl
    {
        private readonly TextBox _textBox = new();

        private readonly Button _removeButton = new()
        {
            Margin = new(8, 0, 0, 0),
            Icon = SymbolRegular.Delete24
        };

        private readonly Grid _grid = new()
        {
            Margin = new(0, 0, 0, 8),
            ColumnDefinitions =
            {
                new() { Width = new GridLength(1, GridUnitType.Star) },
                new() { Width = new GridLength(1, GridUnitType.Auto) }
            }
        };

        /// <summary>获取或设置输入的 SSID 文本。</summary>
        public string Text
        {
            get => _textBox.Text;
            set => _textBox.Text = value;
        }

        /// <summary>用户点击删除按钮时触发。</summary>
        public event EventHandler? Delete;

        /// <summary>
        /// 初始化控件布局，并让删除按钮高度与文本框保持一致。
        /// </summary>
        public ItemControl()
        {
            _removeButton.SetBinding(HeightProperty, new Binding(nameof(_textBox.ActualHeight)) { Source = _textBox });
            _removeButton.Click += (_, _) => Delete?.Invoke(this, EventArgs.Empty);

            Grid.SetColumn(_textBox, 0);
            Grid.SetColumn(_removeButton, 1);

            _grid.Children.Add(_textBox);
            _grid.Children.Add(_removeButton);

            Content = _grid;
        }
    }
}

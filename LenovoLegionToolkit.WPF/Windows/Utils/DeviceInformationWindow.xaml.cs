using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Extensions;
using Wpf.Ui.Controls;

namespace LenovoLegionToolkit.WPF.Windows.Utils;

/// <summary>
/// 设备信息窗口，展示机器型号、序列号与 BIOS 版本。
/// 【已清理】保修查询功能已移除，不再展示保修起止日期与查询链接。
/// </summary>
public partial class DeviceInformationWindow
{
    public DeviceInformationWindow() => InitializeComponent();

    private async void DeviceInformationWindow_Loaded(object sender, RoutedEventArgs e) => await RefreshAsync();

    /// <summary>
    /// 加载机器基础信息并回填到界面。
    /// </summary>
    private async Task RefreshAsync()
    {
        var mi = await Compatibility.GetMachineInformationAsync();

        _manufacturerLabel.Text = mi.Vendor;
        _modelLabel.Text = mi.Model;
        _mtmLabel.Text = mi.MachineType;
        _serialNumberLabel.Text = mi.SerialNumber;
        _biosLabel.Text = mi.BiosVersionRaw;
    }

    /// <summary>
    /// 点击信息卡片时将对应文本复制到剪贴板并给出提示。
    /// </summary>
    private async void DeviceCardControl_Click(object sender, RoutedEventArgs e)
    {
        if (((sender as CardControl)?.Content as TextBlock)?.Text is not { } str)
            return;

        try
        {
            Clipboard.SetText(str);
            await _snackBar.ShowAsync(Resource.CopiedToClipboard_Title, string.Format(Resource.CopiedToClipboard_Message_WithParam, str));
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't copy to clipboard", ex);
        }
    }
}

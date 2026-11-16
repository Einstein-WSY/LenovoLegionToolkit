using System.Windows;
using LenovoLegionToolkit.WPF.Extensions;

namespace LenovoLegionToolkit.WPF.Pages;

/// <summary>
/// “捐赠”页面：展示捐赠方式，并在点击按钮时用默认浏览器打开捐赠链接。
/// </summary>
public partial class DonatePage
{
    /// <summary>
    /// 初始化页面并加载对应的 XAML 视图。
    /// </summary>
    public DonatePage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// PayPal 捐赠按钮点击：打开捐赠链接并标记事件已处理。
    /// 【已注释】赞助信息功能已停用，事件处理保留但不再执行打开捐赠链接的操作。
    /// </summary>
    /// <param name="sender">事件源。</param>
    /// <param name="e">路由事件参数。</param>
    private void PayPalDonateButton_Click(object sender, RoutedEventArgs e)
    {
        //Constants.PayPalUri.Open();
        e.Handled = true;
    }
}

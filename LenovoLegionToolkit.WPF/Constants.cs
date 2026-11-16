using System;

namespace LenovoLegionToolkit.WPF;

/// <summary>
/// 存放应用级常量（如外部链接地址）。
/// </summary>
public static class Constants
{
    /// <summary>最新版本发布页地址，用于检查更新与“关于”页面。</summary>
    public static readonly Uri LatestReleaseUri = new("https://github.com/BartoszCichecki/LenovoLegionToolkit/releases/latest");
    /// <summary>PayPal 捐赠页地址。</summary>
    public static readonly Uri PayPalUri = new("https://www.paypal.com/donate/?hosted_button_id=22AZE2NBP3HTL");
}

using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;

namespace LenovoLegionToolkit.WPF.Controls.Custom;

/// <summary>
/// 重写 WPF-UI 的 <see cref="Wpf.Ui.Controls.InfoBar"/>，使提示信息可被读屏软件正确朗读。
/// </summary>
public class InfoBar : Wpf.Ui.Controls.InfoBar
{
    /// <summary>创建自定义自动化 Peer。</summary>
    /// <returns>自动化 Peer。</returns>
    protected override AutomationPeer OnCreateAutomationPeer() => new InfoBarAutomationPeer(this);

    // 自定义自动化 Peer：把信息条暴露为面板类型
    private class InfoBarAutomationPeer(InfoBar owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(InfoBar);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        public override object? GetPattern(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.ItemContainer)
                return this;

            return base.GetPattern(patternInterface);
        }

        // 以内容元素作为标签来源
        protected override AutomationPeer? GetLabeledByCore()
        {
            if (owner.Content is UIElement element)
                return CreatePeerForElement(element);

            return base.GetLabeledByCore();
        }

        // 名称回退为“标题, 消息”组合，保证提示内容完整
        protected override string GetNameCore()
        {
            var result = base.GetNameCore() ?? string.Empty;

            if (result == string.Empty)
                result = AutomationProperties.GetName(owner);

            if (result == string.Empty)
                result = $"{owner.Title}, {owner.Message}";

            return result;
        }
    }
}

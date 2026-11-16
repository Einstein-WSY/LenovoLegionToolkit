using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;

namespace LenovoLegionToolkit.WPF.Controls.Custom;

/// <summary>
/// 重写 WPF-UI 的 <see cref="Wpf.Ui.Controls.CardExpander"/>，为可展开卡片补充无障碍信息。
/// </summary>
public class CardExpander : Wpf.Ui.Controls.CardExpander
{
    /// <summary>创建自定义自动化 Peer。</summary>
    /// <returns>自动化 Peer。</returns>
    protected override AutomationPeer OnCreateAutomationPeer() => new CardExpanderAutomationPeer(this);

    // 自定义自动化 Peer：把可展开卡片暴露为面板类型
    private class CardExpanderAutomationPeer(CardExpander owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(CardExpander);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        public override object? GetPattern(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.ItemContainer)
                return this;

            return base.GetPattern(patternInterface);
        }

        // 以 Header 元素作为标签来源
        protected override AutomationPeer? GetLabeledByCore()
        {
            if (owner.Header is UIElement element)
                return CreatePeerForElement(element);

            return base.GetLabeledByCore();
        }

        // 名称回退顺序：基础名称 -> 附加属性 -> Header 元素名称 -> 字符串 Header
        protected override string GetNameCore()
        {
            var result = base.GetNameCore() ?? string.Empty;

            if (result == string.Empty)
                result = AutomationProperties.GetName(owner);

            if (result == string.Empty && owner.Header is DependencyObject d)
                result = AutomationProperties.GetName(d);

            if (result == string.Empty && owner.Header is string s)
                result = s;

            return result;
        }
    }
}

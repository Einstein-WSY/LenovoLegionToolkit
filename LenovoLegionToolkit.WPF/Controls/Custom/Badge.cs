using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;

namespace LenovoLegionToolkit.WPF.Controls.Custom;

/// <summary>
/// 重写 WPF-UI 的 <see cref="Wpf.Ui.Controls.Badge"/>，为其提供更完整的无障碍（UI Automation）信息。
/// </summary>
public class Badge : Wpf.Ui.Controls.Badge
{
    /// <summary>创建自定义自动化 Peer。</summary>
    /// <returns>自动化 Peer。</returns>
    protected override AutomationPeer OnCreateAutomationPeer() => new BadgeAutomationPeer(this);

    // 自定义自动化 Peer：将徽章暴露为按钮类型，并以内容作为无障碍名称
    private class BadgeAutomationPeer(Badge owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(Badge);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        public override object? GetPattern(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.ItemContainer)
                return this;

            return base.GetPattern(patternInterface);
        }

        // 以内容元素作为标签来源，便于读屏软件关联
        protected override AutomationPeer? GetLabeledByCore()
        {
            if (owner.Content is UIElement element)
                return CreatePeerForElement(element);

            return base.GetLabeledByCore();
        }

        // 名称回退顺序：基础名称 -> 附加属性 -> 内容元素名称 -> 字符串内容
        protected override string GetNameCore()
        {
            var result = base.GetNameCore() ?? string.Empty;

            if (result == string.Empty)
                result = AutomationProperties.GetName(owner);

            if (result == string.Empty && owner.Content is DependencyObject d)
                result = AutomationProperties.GetName(d);

            if (result == string.Empty && owner.Content is string s)
                result = s;

            return result;
        }
    }
}

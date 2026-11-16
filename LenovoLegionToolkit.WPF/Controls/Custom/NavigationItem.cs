using System.Windows.Automation;
using System.Windows.Automation.Peers;

namespace LenovoLegionToolkit.WPF.Controls.Custom;

/// <summary>
/// 重写 WPF-UI 的 <see cref="Wpf.Ui.Controls.NavigationItem"/>，为导航项补充无障碍名称。
/// </summary>
public class NavigationItem : Wpf.Ui.Controls.NavigationItem
{
    /// <summary>创建自定义自动化 Peer。</summary>
    /// <returns>自动化 Peer。</returns>
    protected override AutomationPeer OnCreateAutomationPeer() => new NavigationItemAutomationPeer(this);

    // 自定义自动化 Peer：把导航项暴露为面板类型
    private class NavigationItemAutomationPeer(NavigationItem owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(NavigationItem);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        public override object? GetPattern(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.ItemContainer)
                return this;

            return base.GetPattern(patternInterface);
        }

        // 名称回退为附加的 AutomationProperties.Name
        protected override string GetNameCore()
        {
            var result = base.GetNameCore() ?? string.Empty;

            if (result == string.Empty)
                result = AutomationProperties.GetName(owner);

            return result;
        }
    }
}

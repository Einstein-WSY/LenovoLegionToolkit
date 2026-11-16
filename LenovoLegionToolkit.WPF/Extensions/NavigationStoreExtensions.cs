using System.Linq;
using Wpf.Ui.Controls;
using Wpf.Ui.Controls.Interfaces;

namespace LenovoLegionToolkit.WPF.Extensions;

/// <summary>
/// 提供导航栏顺序切换的扩展方法。
/// </summary>
public static class NavigationStoreExtensions
{
    /// <summary>
    /// 导航到当前项之后的下一个导航项，到达末尾时循环回到第一项。
    /// </summary>
    /// <param name="navigationStore">目标导航容器。</param>
    public static void NavigateToNext(this NavigationStore navigationStore)
    {
        var navigationItems = navigationStore.Items.OfType<INavigationItem>().ToList();
        var current = navigationStore.Current ?? navigationItems.FirstOrDefault();

        if (current is null)
            return;

        // 通过取模实现环形遍历，避免越界
        var index = (navigationItems.IndexOf(current) + 1) % navigationItems.Count;
        var next = navigationItems[index];

        navigationStore.Navigate(next.PageTag);
    }

    /// <summary>
    /// 导航到当前项之前的上一项，已在首项时循环跳转到最后一项。
    /// </summary>
    /// <param name="navigationStore">目标导航容器。</param>
    public static void NavigateToPrevious(this NavigationStore navigationStore)
    {
        var navigationItems = navigationStore.Items.OfType<INavigationItem>().ToList();
        var current = navigationStore.Current ?? navigationItems.FirstOrDefault();

        if (current is null)
            return;

        var index = navigationItems.IndexOf(current) - 1;
        if (index < 0)
            index = navigationItems.Count - 1;
        var next = navigationItems[index];

        navigationStore.Navigate(next.PageTag);
    }
}

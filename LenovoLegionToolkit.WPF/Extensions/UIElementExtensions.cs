using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace LenovoLegionToolkit.WPF.Extensions;

/// <summary>
/// 提供可视化树遍历相关的扩展方法。
/// </summary>
public static class UIElementExtensions
{
    /// <summary>
    /// 深度优先遍历可视化树，返回所有可见的指定类型子元素。
    /// </summary>
    /// <typeparam name="T">要筛选的子元素类型。</typeparam>
    /// <param name="depObj">遍历起始元素。</param>
    /// <returns>可见的指定类型子元素序列。</returns>
    /// <remarks>起始元素本身不可见时直接结束，不再向下遍历，以跳过折叠分支。</remarks>
    public static IEnumerable<T> GetVisibleChildrenOfType<T>(this UIElement depObj) where T : UIElement
    {
        if (depObj.Visibility != Visibility.Visible)
            yield break;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = VisualTreeHelper.GetChild(depObj, i);

            switch (child)
            {
                case T value:
                    {
                        yield return value;
                        break;
                    }
                case UIElement element:
                    {
                        foreach (var sub in GetVisibleChildrenOfType<T>(element))
                            yield return sub;
                        break;
                    }
            }
        }
    }
}

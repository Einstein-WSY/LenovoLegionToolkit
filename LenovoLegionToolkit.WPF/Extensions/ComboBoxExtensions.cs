using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace LenovoLegionToolkit.WPF.Extensions;

/// <summary>
/// 提供 <see cref="ComboBox"/> 与强类型选项集合之间交互的扩展方法。
/// </summary>
public static class ComboBoxExtensions
{
    /// <summary>
    /// 获取下拉框中当前包含的所有强类型选项值。
    /// </summary>
    /// <typeparam name="T">选项值的类型。</typeparam>
    /// <param name="comboBox">目标下拉框。</param>
    /// <returns>下拉框中全部选项的值。</returns>
    public static IEnumerable<T> GetItems<T>(this ComboBox comboBox)
    {
        return comboBox.Items.OfType<ComboBoxItem<T>>().Select(item => item.Value);
    }

    /// <summary>
    /// 用指定选项集合重新填充下拉框，并选中给定项。
    /// </summary>
    /// <typeparam name="T">选项值的类型。</typeparam>
    /// <param name="comboBox">目标下拉框。</param>
    /// <param name="items">要填充的选项值集合。</param>
    /// <param name="selectedItem">需要默认选中的项。</param>
    /// <param name="displayValueConverter">可选的显示文本转换函数。</param>
    public static void SetItems<T>(this ComboBox comboBox, IEnumerable<T> items, T selectedItem, Func<T, object>? displayValueConverter = null)
    {
        var boxedItems = items.Select(v => new ComboBoxItem<T>(v, displayValueConverter)).ToArray();
        var selectedBoxedItem = boxedItems.FirstOrDefault(bv => EqualityComparer<T>.Default.Equals(bv.Value, selectedItem));

        comboBox.Items.Clear();
        comboBox.Items.AddRange(boxedItems);
        comboBox.SelectedValue = selectedBoxedItem;
    }

    /// <summary>
    /// 在下拉框中选中与给定值相等的选项。
    /// </summary>
    /// <typeparam name="T">选项值的类型。</typeparam>
    /// <param name="comboBox">目标下拉框。</param>
    /// <param name="item">要选中的值。</param>
    public static void SelectItem<T>(this ComboBox comboBox, T item) where T : struct
    {
        var boxedItems = comboBox.Items.OfType<ComboBoxItem<T>>().Select(i => i.Value).ToArray();
        comboBox.SelectedIndex = Array.IndexOf(boxedItems, item);
    }

    /// <summary>
    /// 清空下拉框中的所有选项并取消选中状态。
    /// </summary>
    /// <param name="comboBox">目标下拉框。</param>
    public static void ClearItems(this ComboBox comboBox)
    {
        comboBox.Items.Clear();
        comboBox.SelectedValue = null;
    }

    /// <summary>
    /// 尝试获取下拉框当前选中项的值。
    /// </summary>
    /// <typeparam name="T">选项值的类型。</typeparam>
    /// <param name="comboBox">目标下拉框。</param>
    /// <param name="value">输出参数，选中项的值；未选中时为其默认值。</param>
    /// <returns>存在有效选中项时返回 <see langword="true"/>。</returns>
    public static bool TryGetSelectedItem<T>(this ComboBox comboBox, out T? value)
    {
        if (comboBox.SelectedItem is ComboBoxItem<T> selectedBoxedItem)
        {
            value = selectedBoxedItem.Value;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// 从选择变更事件参数中获取新选中的值。
    /// </summary>
    /// <typeparam name="T">选项值的类型。</typeparam>
    /// <param name="args">选择变更事件参数。</param>
    /// <returns>新选中的值；无有效新选项时返回 <see langword="null"/>。</returns>
    public static T? GetNewValue<T>(this SelectionChangedEventArgs args) where T : struct
    {
        var items = args.AddedItems;
        if (items.Count < 1 || items[0] is not ComboBoxItem<T> item)
            return null;
        return item.Value;
    }

    /// <summary>
    /// 从选择变更事件参数中获取此前选中的值。
    /// </summary>
    /// <typeparam name="T">选项值的类型。</typeparam>
    /// <param name="args">选择变更事件参数。</param>
    /// <returns>此前选中的值；无有效旧选项时返回 <see langword="null"/>。</returns>
    public static T? GetOldValue<T>(this SelectionChangedEventArgs args) where T : struct
    {
        var items = args.RemovedItems;
        if (items.Count < 1 || items[0] is not ComboBoxItem<T> item)
            return null;
        return item.Value;
    }

    /// <summary>
    /// 下拉框内部的强类型选项包装类，用于在 <see cref="ComboBox"/> 中承载原始值。
    /// </summary>
    /// <typeparam name="T">选项值的类型。</typeparam>
    private class ComboBoxItem<T>(T value, Func<T, object>? displayString)
    {
        public static bool operator ==(ComboBoxItem<T> left, ComboBoxItem<T> right) => left.Equals(right);

        public static bool operator !=(ComboBoxItem<T> left, ComboBoxItem<T> right) => !(left == right);

        public T Value { get; } = value;

        public override bool Equals(object? obj) => obj is ComboBoxItem<T> item && EqualityComparer<T>.Default.Equals(Value, item.Value);

        public override int GetHashCode() => HashCode.Combine(Value);

        public override string ToString() => displayString?.Invoke(Value).ToString() ?? Value?.ToString() ?? string.Empty;
    }
}

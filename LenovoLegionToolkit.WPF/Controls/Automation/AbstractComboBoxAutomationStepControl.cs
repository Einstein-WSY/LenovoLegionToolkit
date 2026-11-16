using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.WPF.Extensions;

namespace LenovoLegionToolkit.WPF.Controls.Automation;

/// <summary>
/// 基于下拉框的自动化步骤控件基类：自动枚举步骤所有可用状态并以下拉框供用户选择。
/// </summary>
/// <typeparam name="T">步骤状态值类型（枚举或结构体）。</typeparam>
/// <param name="step">要编辑的自动化步骤实例。</param>
public abstract class AbstractComboBoxAutomationStepCardControl<T>(IAutomationStep<T> step)
    : AbstractAutomationStepControl<IAutomationStep<T>>(step) where T : struct
{
    private readonly ComboBox _comboBox = new()
    {
        MinWidth = 150,
        Visibility = Visibility.Hidden,
        Margin = new(8, 0, 0, 0)
    };

    private T _state;

    /// <summary>
    /// 提供下拉框控件，并在加载完成前保持隐藏以避免闪烁。
    /// </summary>
    /// <returns>下拉框控件。</returns>
    protected override UIElement GetCustomControl()
    {
        _comboBox.SelectionChanged += ComboBox_SelectionChanged;

        return _comboBox;
    }

    /// <summary>
    /// 下拉选择变化时更新本地状态，仅当选中值确实发生变化时才通知上层。
    /// </summary>
    private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_comboBox.TryGetSelectedItem(out T selectedState) || _state.Equals(selectedState))
            return;

        _state = selectedState;

        RaiseChanged();
    }

    /// <summary>
    /// 以当前选中的状态值通过反射重建对应类型的自动化步骤实例。
    /// </summary>
    /// <returns>新建的自动化步骤实例。</returns>
    public override IAutomationStep CreateAutomationStep()
    {
        var obj = Activator.CreateInstance(AutomationStep.GetType(), _state);
        if (obj is not IAutomationStep<T> step)
            throw new InvalidOperationException("Couldn't create automation step");
        return step;
    }

    /// <summary>
    /// 生成下拉项的显示文本：优先使用显示名称接口，其次本地化枚举名。
    /// </summary>
    /// <param name="value">状态值。</param>
    /// <returns>用于展示的文本。</returns>
    protected virtual string ComboBoxItemDisplayName(T value) => value switch
    {
        IDisplayName dn => dn.DisplayName,
        Enum e => e.GetDisplayName(),
        _ => value.ToString() ?? throw new InvalidOperationException("Unsupported type")
    };

    /// <summary>
    /// 拉取所有可用状态填充下拉框，并在无可用状态时禁用下拉框。
    /// </summary>
    protected override async Task RefreshAsync()
    {
        AutomationProperties.SetName(_comboBox, Title);

        var items = await AutomationStep.GetAllStatesAsync();
        var selectedItem = AutomationStep.State;

        _state = selectedItem;
        _comboBox.SetItems(items, selectedItem, ComboBoxItemDisplayName);
        _comboBox.IsEnabled = items.Length != 0;
    }

    /// <summary>
    /// 加载完成后显示下拉框。
    /// </summary>
    protected override void OnFinishedLoading() => _comboBox.Visibility = Visibility.Visible;
}

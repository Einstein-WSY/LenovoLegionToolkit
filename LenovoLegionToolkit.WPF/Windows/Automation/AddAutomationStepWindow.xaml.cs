using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using LenovoLegionToolkit.WPF.Controls;
using LenovoLegionToolkit.WPF.Controls.Automation;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;
using CardControl = LenovoLegionToolkit.WPF.Controls.Custom.CardControl;

namespace LenovoLegionToolkit.WPF.Windows.Automation;

/// <summary>
/// "添加自动化步骤"选择窗口，以卡片列表形式展示可选的步骤类型，选中后回调给调用方。
/// </summary>
public partial class AddAutomationStepWindow
{
    private readonly List<AbstractAutomationStepControl> _controls;
    private readonly Action<AbstractAutomationStepControl> _addStepControl;

    /// <summary>
    /// 初始化窗口。
    /// </summary>
    /// <param name="controls">可供选择的步骤控件集合。</param>
    /// <param name="addStepControl">用户选中某个步骤后的回调。</param>
    public AddAutomationStepWindow(List<AbstractAutomationStepControl> controls, Action<AbstractAutomationStepControl> addStepControl)
    {
        _controls = controls;
        _addStepControl = addStepControl;

        InitializeComponent();

        IsVisibleChanged += AddAutomationStepWindow_IsVisibleChanged;
    }

    /// <summary>
    /// 窗口每次变为可见时重建步骤卡片列表。
    /// </summary>
    private async void AddAutomationStepWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            await RefreshAsync();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// 根据步骤控件集合重新生成卡片列表。
    /// </summary>
    private Task RefreshAsync()
    {
        _content.Children.Clear();

        foreach (var control in _controls)
            _content.Children.Add(CreateCardControl(control));

        return Task.CompletedTask;
    }

    /// <summary>
    /// 将一个步骤控件包装为可点击卡片，点击后触发回调并关闭窗口。
    /// </summary>
    /// <param name="stepControl">要展示的步骤控件。</param>
    /// <returns>用于列表展示的卡片控件。</returns>
    private CardControl CreateCardControl(AbstractAutomationStepControl stepControl)
    {
        var control = new CardControl
        {
            Icon = stepControl.Icon,
            Header = new CardHeaderControl
            {
                Title = stepControl.Title,
                Accessory = new SymbolIcon { Symbol = SymbolRegular.ChevronRight24 }
            },
            Margin = new(0, 8, 0, 0),
        };

        control.Click += (_, _) =>
        {
            _addStepControl(stepControl);
            Close();
        };

        return control;
    }
}

using System;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “DPI 缩放”步骤的编辑器：通过下拉框选择 <see cref="DpiScale"/>，并监听显示配置变化以刷新可选值。
/// </summary>
public class DpiScaleAutomationStepControl : AbstractComboBoxAutomationStepCardControl<DpiScale>
{
    // 显示配置监听器：分辨率/缩放变化后重新拉取可用项。
    private readonly DisplayConfigurationListener _listener = IoCContainer.Resolve<DisplayConfigurationListener>();

    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本，同时订阅显示配置变化事件。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public DpiScaleAutomationStepControl(IAutomationStep<DpiScale> step) : base(step)
    {
        Icon = SymbolRegular.TextFontSize24;
        Title = Resource.DpiScaleAutomationStepControl_Title;
        Subtitle = Resource.DpiScaleAutomationStepControl_Message;

        _listener.Changed += Listener_Changed;
    }

    /// <summary>
    /// 生成下拉项显示文本，并强制按从左到右排版以适配 RTL 语言环境。
    /// </summary>
    /// <param name="value">缩放值。</param>
    /// <returns>显示文本。</returns>
    protected override string ComboBoxItemDisplayName(DpiScale value)
    {
        var str = base.ComboBoxItemDisplayName(value);
        return LocalizationHelper.ForceLeftToRight(str);
    }

    /// <summary>
    /// 显示配置变化时在 UI 线程刷新下拉项（仅当控件已加载）。
    /// </summary>
    private void Listener_Changed(object? sender, EventArgs e) => Dispatcher.Invoke(async () =>
    {
        if (IsLoaded)
            await RefreshAsync();
    });
}

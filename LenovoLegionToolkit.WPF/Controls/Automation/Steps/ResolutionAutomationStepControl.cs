using System;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “分辨率”步骤的编辑器：通过下拉框选择 <see cref="Resolution"/>，并监听显示配置变化以刷新可选值。
/// </summary>
public class ResolutionAutomationStepControl : AbstractComboBoxAutomationStepCardControl<Resolution>
{
    // 显示配置监听器：分辨率变化后重新拉取可用项。
    private readonly DisplayConfigurationListener _listener = IoCContainer.Resolve<DisplayConfigurationListener>();

    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本，同时订阅显示配置变化事件。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public ResolutionAutomationStepControl(IAutomationStep<Resolution> step) : base(step)
    {
        Icon = SymbolRegular.ScaleFill24;
        Title = Resource.ResolutionAutomationStepControl_Title;
        Subtitle = Resource.ResolutionAutomationStepControl_Message;

        _listener.Changed += Listener_Changed;
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

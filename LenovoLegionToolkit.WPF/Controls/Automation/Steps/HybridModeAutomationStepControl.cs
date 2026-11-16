using System.Threading.Tasks;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Resources;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// 混合模式步骤编辑器的工厂：根据机型是否支持 iGPU 模式，分别返回下拉框或开关形式的编辑器。
/// </summary>
public static class HybridModeAutomationStepControlFactory
{
    /// <summary>
    /// 查询机型能力并创建合适的混合模式步骤编辑器。
    /// </summary>
    /// <param name="step">要编辑的混合模式步骤。</param>
    /// <returns>步骤编辑器控件。</returns>
    public static async Task<AbstractAutomationStepControl<IAutomationStep<HybridModeState>>> GetControlAsync(IAutomationStep<HybridModeState> step)
    {
        var mi = await Compatibility.GetMachineInformationAsync();
        return mi.Properties.SupportsIGPUMode
            ? new ComboBoxHybridModeAutomationStepControl(step)
            : new ToggleHybridModeAutomationStepControl(step);
    }

    /// <summary>
    /// 支持 iGPU 模式机型的编辑器（以下拉框呈现多种混合模式）。
    /// </summary>
    private class ComboBoxHybridModeAutomationStepControl : AbstractComboBoxAutomationStepCardControl<HybridModeState>
    {
        /// <summary>
        /// 初始化下拉框编辑器并设置图标、标题与说明文本。
        /// </summary>
        /// <param name="step">要编辑的步骤实例。</param>
        public ComboBoxHybridModeAutomationStepControl(IAutomationStep<HybridModeState> step) : base(step)
        {
            Icon = SymbolRegular.LeafOne24;
            Title = Resource.ComboBoxHybridModeAutomationStepControl_Title;
            Subtitle = Resource.ComboBoxHybridModeAutomationStepControl_Message;
        }
    }

    /// <summary>
    /// 不支持 iGPU 模式机型的编辑器（仅可在开关两种状态间切换）。
    /// </summary>
    private class ToggleHybridModeAutomationStepControl : AbstractComboBoxAutomationStepCardControl<HybridModeState>
    {
        /// <summary>
        /// 初始化开关式编辑器并设置图标、标题与说明文本。
        /// </summary>
        /// <param name="step">要编辑的步骤实例。</param>
        public ToggleHybridModeAutomationStepControl(IAutomationStep<HybridModeState> step) : base(step)
        {
            Icon = SymbolRegular.LeafOne24;
            Title = Resource.ToggleHybridModeAutomationStepControl_Title;
            Subtitle = Resource.ToggleHybridModeAutomationStepControl_Message;
        }
    }
}

using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Microsoft.Win32;
using Wpf.Ui.Common;
using Button = Wpf.Ui.Controls.Button;
using TextBox = Wpf.Ui.Controls.TextBox;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “导入 Spectrum 键盘背光配置”步骤的编辑器：编辑 JSON 配置文件路径，并提供文件选择按钮。
/// </summary>
public class SpectrumKeyboardBacklightImportProfileAutomationStepControl : AbstractAutomationStepControl<SpectrumKeyboardBacklightImportProfileAutomationStep>
{
    // 配置文件路径输入框。
    private readonly TextBox _path = new()
    {
        PlaceholderText = Resource.SpectrumKeyboardBacklightImportProfileAutomationStepControl_Path,
        Width = 300
    };

    // 浏览文件按钮。
    private readonly Button _openButton = new()
    {
        Icon = SymbolRegular.MoreHorizontal24,
        MinWidth = 34,
        Height = 34,
        Margin = new(8, 0, 0, 0)
    };

    // 横向排列输入框与浏览按钮的容器。
    private readonly StackPanel _stackPanel = new()
    {
        Orientation = Orientation.Horizontal
    };

    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本，同时订阅尺寸变化以自适应输入框宽度。
    /// </summary>
    /// <param name="step">要编辑的步骤实例。</param>
    public SpectrumKeyboardBacklightImportProfileAutomationStepControl(SpectrumKeyboardBacklightImportProfileAutomationStep step) : base(step)
    {
        Icon = SymbolRegular.BrightnessHigh24;
        Title = Resource.SpectrumKeyboardBacklightImportProfileAutomationStepControl_Title;
        Subtitle = Resource.SpectrumKeyboardBacklightImportProfileAutomationStepControl_Message;

        SizeChanged += RunAutomationStepControl_SizeChanged;
    }

    /// <summary>
    /// 宽度变化时将路径输入框宽度调整为卡片宽度的三分之一。
    /// </summary>
    private void RunAutomationStepControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!e.WidthChanged)
            return;

        var newWidth = e.NewSize.Width / 3;
        _path.Width = newWidth;
    }

    /// <summary>
    /// 以输入框中的配置文件路径创建步骤。
    /// </summary>
    /// <returns>新的步骤实例。</returns>
    public override IAutomationStep CreateAutomationStep() => new SpectrumKeyboardBacklightImportProfileAutomationStep(_path.Text);

    /// <summary>
    /// 构建编辑区：文本变化时通知上层，浏览按钮以 JSON 过滤器打开文件对话框并回填路径。
    /// </summary>
    /// <returns>自定义编辑控件。</returns>
    protected override UIElement GetCustomControl()
    {
        _path.TextChanged += (_, _) =>
        {
            if (_path.Text != AutomationStep.Path)
                RaiseChanged();
        };

        _openButton.Click += (_, _) =>
        {
            var ofd = new OpenFileDialog
            {
                Title = Resource.Import,
                InitialDirectory = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
                Filter = "Json Files (.json)|*.json",
                CheckFileExists = true,
            };

            var result = ofd.ShowDialog() ?? false;
            if (!result)
                return;

            _path.Text = ofd.FileName;
        };

        _stackPanel.Children.Add(_path);
        _stackPanel.Children.Add(_openButton);

        return _stackPanel;
    }

    /// <summary>
    /// 无加载后处理。
    /// </summary>
    protected override void OnFinishedLoading() { }

    /// <summary>
    /// 将步骤中的配置文件路径同步到输入框。
    /// </summary>
    protected override Task RefreshAsync()
    {
        _path.Text = AutomationStep.Path ?? string.Empty;
        return Task.CompletedTask;
    }
}

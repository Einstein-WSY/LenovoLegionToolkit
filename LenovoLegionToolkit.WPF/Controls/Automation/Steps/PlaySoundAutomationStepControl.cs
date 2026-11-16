using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.WPF.Resources;
using Microsoft.Win32;
using Wpf.Ui.Common;
using Button = Wpf.Ui.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;

namespace LenovoLegionToolkit.WPF.Controls.Automation.Steps;

/// <summary>
/// “播放声音”步骤的编辑器：显示已选音频文件名，并通过文件对话框选择音频路径。
/// </summary>
public class PlaySoundAutomationStepControl : AbstractAutomationStepControl<PlaySoundAutomationStep>
{
    // 当前选中的音频文件路径。
    private string? _path;

    // 显示音频文件名（不含目录）。
    private readonly TextBlock _titleTextBlock = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
    };

    // 打开文件选择对话框的按钮。
    private readonly Button _openButton = new()
    {
        Icon = SymbolRegular.Folder20,
        MinWidth = 34,
        Height = 34,
        Margin = new(8, 0, 0, 0)
    };

    // 横向排列文件名与选择按钮的容器。
    private readonly StackPanel _stackPanel = new()
    {
        Orientation = Orientation.Horizontal
    };

    /// <summary>
    /// 初始化编辑器并设置图标、标题与说明文本。
    /// </summary>
    /// <param name="automationStep">要编辑的步骤实例。</param>
    public PlaySoundAutomationStepControl(PlaySoundAutomationStep automationStep) : base(automationStep)
    {
        Icon = SymbolRegular.MusicNote2Play20;
        Title = Resource.PlaySoundAutomationStepControl_Title;
        Subtitle = Resource.PlaySoundAutomationStepControl_Message;
    }

    /// <summary>
    /// 以当前选中的音频路径创建步骤。
    /// </summary>
    /// <returns>新的步骤实例。</returns>
    public override IAutomationStep CreateAutomationStep()
    {
        return new PlaySoundAutomationStep(_path);
    }

    /// <summary>
    /// 构建"文件名 + 选择按钮"编辑区，选择文件后更新显示并通知上层。
    /// </summary>
    /// <returns>自定义编辑控件。</returns>
    protected override UIElement GetCustomControl()
    {
        _openButton.Click += (_, _) =>
        {
            var ofd = new OpenFileDialog
            {
                Title = Resource.Import,
                InitialDirectory = @"C:\Windows\Media",
                CheckFileExists = true,
            };

            var result = ofd.ShowDialog() ?? false;
            if (!result)
                return;

            _path = ofd.FileName;
            _titleTextBlock.Text = Path.GetFileName(_path);

            RaiseChanged();
        };

        _stackPanel.Children.Add(_titleTextBlock);
        _stackPanel.Children.Add(_openButton);

        return _stackPanel;
    }

    /// <summary>
    /// 无加载后处理。
    /// </summary>
    protected override void OnFinishedLoading() { }

    /// <summary>
    /// 将步骤中的音频路径同步到界面，未设置时显示占位符 "-"。
    /// </summary>
    protected override Task RefreshAsync()
    {
        _path = AutomationStep.Path;
        _titleTextBlock.Text = Path.GetFileName(_path) ?? "-";
        return Task.CompletedTask;
    }
}

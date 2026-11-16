using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using Microsoft.Win32;
using Wpf.Ui.Common;
using Button = Wpf.Ui.Controls.Button;

namespace LenovoLegionToolkit.WPF.Windows.Automation.TabItemContent;

/// <summary>
/// 进程运行/退出触发器的配置选项卡，支持手动添加进程及通过剪贴板批量导入。
/// </summary>
public partial class ProcessAutomationPipelineTriggerTabItemControl : IAutomationPipelineTriggerTabItemContent<IProcessesAutomationPipelineTrigger>
{
    private readonly IProcessesAutomationPipelineTrigger _trigger;
    private readonly List<ProcessInfo> _processes;

    /// <summary>
    /// 初始化选项卡内容，并复制触发器中的进程列表。
    /// </summary>
    /// <param name="trigger">待编辑的进程触发器。</param>
    public ProcessAutomationPipelineTriggerTabItemControl(IProcessesAutomationPipelineTrigger trigger)
    {
        _trigger = trigger;
        _processes = [.. trigger.Processes];

        InitializeComponent();
    }

    /// <summary>
    /// 生成包含当前进程列表的触发器副本。
    /// </summary>
    /// <returns>更新后的触发器。</returns>
    public IProcessesAutomationPipelineTrigger GetTrigger() => _trigger.DeepCopy([.. _processes]);

    /// <summary>
    /// 注册 Ctrl+C / Ctrl+V 剪贴板命令并首次渲染进程列表。
    /// </summary>
    private void ProcessAutomationPipelineTriggerTabItemControl_Initialized(object? sender, EventArgs e)
    {
        var copyCommand = new RoutedCommand();
        copyCommand.InputGestures.Add(new KeyGesture(Key.C, ModifierKeys.Control));
        CommandBindings.Add(new CommandBinding(copyCommand, CopyShortcut));

        var pasteCommand = new RoutedCommand();
        pasteCommand.InputGestures.Add(new KeyGesture(Key.V, ModifierKeys.Control));
        CommandBindings.Add(new CommandBinding(pasteCommand, PasteShortcut));

        Refresh();
    }

    /// <summary>
    /// 处理列表项删除事件，移除对应进程后刷新列表。
    /// </summary>
    private void Item_OnDelete(object? sender, EventArgs e)
    {
        if (sender is not ListItem listItem)
            return;

        _processes.Remove(listItem.Process);
        Refresh();
    }

    /// <summary>
    /// 通过文件选择对话框添加一个可执行文件。
    /// </summary>
    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new OpenFileDialog
        {
            // 使用"此电脑"的 CLSID 作为初始目录
            InitialDirectory = "::{20D04FE0-3AEA-1069-A2D8-08002B30309D}",
            Filter = "Exe Files (.exe)|*.exe",
            CheckFileExists = true,
        };

        var result = ofd.ShowDialog() ?? false;
        if (!result)
            return;

        var processInfo = ProcessInfo.FromPath(ofd.FileName);
        if (_processes.Contains(processInfo))
            return;

        _processes.Add(processInfo);
        Refresh();
    }

    /// <summary>
    /// 清空全部进程后刷新列表。
    /// </summary>
    private void DeleteAllButton_Click(object sender, RoutedEventArgs e)
    {
        _processes.Clear();
        Refresh();
    }

    /// <summary>
    /// 将当前进程列表写入剪贴板。
    /// </summary>
    private void CopyShortcut(object sender, RoutedEventArgs e)
    {
        try
        {
            ClipboardExtensions.SetProcesses(_processes);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't copy to clipboard", ex);
        }
    }

    /// <summary>
    /// 从剪贴板读取进程路径并合并到列表，跳过已存在的项。
    /// </summary>
    private void PasteShortcut(object sender, RoutedEventArgs e)
    {
        try
        {
            var processes = ClipboardExtensions.GetProcesses()
                .Where(p => !_processes.Contains(p))
                .ToArray();
            if (processes.Length == 0)
                return;

            _processes.AddRange(processes);
            Refresh();
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Couldn't paste from clipboard", ex);
        }
    }

    /// <summary>
    /// 按名称排序后重建进程列表界面。
    /// </summary>
    private void Refresh()
    {
        _list.Items.Clear();
        foreach (var process in _processes.OrderBy(p => p))
        {
            var item = new ListItem(process);
            item.OnDelete += Item_OnDelete;
            _list.Items.Add(item);
        }
    }

    /// <summary>
    /// 进程列表中的单项控件，展示图标、名称、路径并提供删除按钮。
    /// </summary>
    private class ListItem : UserControl
    {
        private readonly Grid _grid = new()
        {
            Margin = new(8, 4, 0, 16),
            ColumnDefinitions =
            {
                new() { Width = GridLength.Auto },
                new() { Width = new(1, GridUnitType.Star) },
                new() { Width = GridLength.Auto },
            },
            RowDefinitions =
            {
                new() { Height = GridLength.Auto },
                new() { Height = GridLength.Auto },
            },
        };

        private readonly Image _icon = new()
        {
            Width = 24,
            Height = 24,
            Margin = new(0, 0, 8, 0),
        };

        private readonly TextBlock _nameTextBox = new()
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        private readonly TextBlock _pathTextBox = new()
        {
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new(0, 4, 0, 0),
        };

        private readonly Button _deleteButton = new()
        {
            Icon = SymbolRegular.Delete24,
            FontSize = 18,
            Margin = new(8, 0, 0, 0),
        };

        /// <summary>当前列表项对应的进程信息。</summary>
        public ProcessInfo Process { get; }

        /// <summary>用户点击删除按钮时触发。</summary>
        public event EventHandler? OnDelete;

        /// <summary>
        /// 初始化列表项。
        /// </summary>
        /// <param name="process">关联的进程信息。</param>
        public ListItem(ProcessInfo process)
        {
            Process = process;

            InitializeComponent();
        }

        /// <summary>
        /// 组装列表项布局，提取可执行文件图标并在缺失时使用默认图标。
        /// </summary>
        private void InitializeComponent()
        {
            _icon.Source = ImageSourceExtensions.ApplicationIcon(Process.ExecutablePath) ?? ImageSourceExtensions.FromResource("Assets/default_exe.png");
            _nameTextBox.Text = Process.Name;
            _pathTextBox.Text = Process.ExecutablePath;

            ToolTipService.SetToolTip(_pathTextBox, Process.ExecutablePath);

            _pathTextBox.SetResourceReference(ForegroundProperty, "TextFillColorSecondaryBrush");

            _deleteButton.Click += (_, _) => OnDelete?.Invoke(this, EventArgs.Empty);

            Grid.SetColumn(_icon, 0);
            Grid.SetRow(_icon, 0);
            Grid.SetRowSpan(_icon, 2);

            Grid.SetColumn(_nameTextBox, 1);
            Grid.SetRow(_nameTextBox, 0);

            Grid.SetColumn(_pathTextBox, 1);
            Grid.SetRow(_pathTextBox, 1);

            Grid.SetColumn(_deleteButton, 2);
            Grid.SetRow(_deleteButton, 0);
            Grid.SetRowSpan(_deleteButton, 2);

            _grid.Children.Add(_icon);
            _grid.Children.Add(_nameTextBox);
            _grid.Children.Add(_pathTextBox);
            _grid.Children.Add(_deleteButton);

            Content = _grid;
        }
    }
}

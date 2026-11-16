using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;

namespace LenovoLegionToolkit.WPF.Controls;

/// <summary>
/// 卡片头部控件，展示标题、副标题与警告文本，并在右侧预留附件（如开关、下拉框）位置。
/// 布局采用两行两列的网格：左列文本、右列自适应附件。
/// </summary>
public class CardHeaderControl : UserControl
{
    private readonly TextBlock _titleTextBlock = new()
    {
        FontSize = 14,
        FontWeight = FontWeights.Medium,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    private readonly TextBlock _subtitleTextBlock = new()
    {
        FontSize = 12,
        Margin = new(0, 4, 0, 0),
        TextWrapping = TextWrapping.Wrap,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    private readonly TextBlock _warningTextBlock = new()
    {
        FontSize = 12,
        Margin = new(0, 4, 0, 0),
        TextWrapping = TextWrapping.Wrap,
        TextTrimming = TextTrimming.CharacterEllipsis,
    };

    private readonly StackPanel _stackPanel = new();

    private readonly Grid _grid = new()
    {
        ColumnDefinitions =
        {
            new ColumnDefinition { Width = new(1, GridUnitType.Star) },
            new ColumnDefinition { Width = GridLength.Auto },
        },
        RowDefinitions =
        {
            new RowDefinition { Height = GridLength.Auto },
            new RowDefinition { Height = GridLength.Auto },
        },
    };

    private UIElement? _accessory;

    /// <summary>标题文本。</summary>
    public string Title
    {
        get => _titleTextBlock.Text;
        set
        {
            _titleTextBlock.Text = value;
            RefreshLayout();
        }
    }

    /// <summary>副标题文本。</summary>
    public string Subtitle
    {
        get => _subtitleTextBlock.Text;
        set
        {
            _subtitleTextBlock.Text = value;
            RefreshLayout();
        }
    }

    /// <summary>标题垂直对齐方式。</summary>
    public VerticalAlignment TitleVerticalAlignment
    {
        get => _titleTextBlock.VerticalAlignment;
        set => _titleTextBlock.VerticalAlignment = value;
    }

    /// <summary>副标题垂直对齐方式。</summary>
    public VerticalAlignment SubtitleVerticalAlignment
    {
        get => _subtitleTextBlock.VerticalAlignment;
        set => _subtitleTextBlock.VerticalAlignment = value;
    }

    /// <summary>警告文本，仅在文本非空时显示。</summary>
    public string Warning
    {
        get => _warningTextBlock.Text;
        set
        {
            _warningTextBlock.Text = value;
            RefreshLayout();
        }
    }

    /// <summary>副标题的悬浮提示；为 <c>null</c> 时禁用提示。</summary>
    public string? SubtitleToolTip
    {
        get => _subtitleTextBlock.ToolTip as string;
        set
        {
            _subtitleTextBlock.ToolTip = value;
            ToolTipService.SetIsEnabled(_subtitleTextBlock, value is not null);
            RefreshLayout();
        }
    }

    /// <summary>放置在右侧的附件元素（跨越两行显示）。</summary>
    public UIElement? Accessory
    {
        get => _accessory;
        set
        {
            // 先移除旧附件，避免视觉树中残留
            if (_accessory is not null)
                _grid.Children.Remove(_accessory);

            _accessory = value;

            if (_accessory is not null)
            {
                // 附件固定在第 2 列并跨两行
                Grid.SetColumn(_accessory, 1);
                Grid.SetRow(_accessory, 0);
                Grid.SetRowSpan(_accessory, 2);

                _grid.Children.Add(_accessory);
            }

            RefreshLayout();
        }
    }

    /// <summary>初始化时构建固定布局与文本样式订阅。</summary>
    /// <param name="e">事件参数。</param>
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        // 标题位于第一行，副标题/警告堆叠在第二行
        Grid.SetColumn(_titleTextBlock, 0);
        Grid.SetColumn(_stackPanel, 0);

        Grid.SetRow(_titleTextBlock, 0);
        Grid.SetRow(_stackPanel, 1);

        _stackPanel.Children.Add(_subtitleTextBlock);
        _stackPanel.Children.Add(_warningTextBlock);

        _grid.Children.Add(_titleTextBlock);
        _grid.Children.Add(_stackPanel);

        Content = _grid;

        UpdateTextStyle();
        // 可用状态变化时同步更新文字颜色
        IsEnabledChanged += (_, _) => UpdateTextStyle();
    }

    /// <summary>创建自定义自动化 Peer，使标题可作为无障碍名称暴露。</summary>
    /// <returns>自动化 Peer。</returns>
    protected override AutomationPeer OnCreateAutomationPeer() => new CardHeaderControlAutomationPeer(this);

    // 无副标题且无警告时，让标题占据两行以保持垂直居中
    private void RefreshLayout()
    {
        if (string.IsNullOrWhiteSpace(Subtitle) && string.IsNullOrWhiteSpace(Warning))
            Grid.SetRowSpan(_titleTextBlock, 2);
        else
            Grid.SetRowSpan(_titleTextBlock, 1);

        _subtitleTextBlock.Visibility = string.IsNullOrWhiteSpace(Subtitle) ? Visibility.Collapsed : Visibility.Visible;
        _warningTextBlock.Visibility = string.IsNullOrWhiteSpace(Warning) ? Visibility.Collapsed : Visibility.Visible;
    }

    // 根据可用状态切换主题画刷资源，保证禁用态视觉正确
    private void UpdateTextStyle()
    {
        if (IsEnabled)
        {
            _titleTextBlock.SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");
            _subtitleTextBlock.SetResourceReference(ForegroundProperty, "TextFillColorSecondaryBrush");
            _warningTextBlock.SetResourceReference(ForegroundProperty, "SystemFillColorCautionBrush");
        }
        else
        {
            _titleTextBlock.SetResourceReference(ForegroundProperty, "TextFillColorDisabledBrush");
            _subtitleTextBlock.SetResourceReference(ForegroundProperty, "TextFillColorDisabledBrush");
            _warningTextBlock.SetResourceReference(ForegroundProperty, "TextFillColorDisabledBrush");
        }
    }

    // 自定义自动化 Peer：将标题（及副标题）作为控件的无障碍名称
    private class CardHeaderControlAutomationPeer(CardHeaderControl owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(CardHeaderControl);

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Pane;

        public override object? GetPattern(PatternInterface patternInterface)
        {
            if (patternInterface == PatternInterface.ItemContainer)
                return this;

            return base.GetPattern(patternInterface);
        }

        protected override string GetNameCore()
        {
            var result = base.GetNameCore() ?? string.Empty;

            if (result == string.Empty)
                result = AutomationProperties.GetName(owner);

            // 回退为标题文本，并拼接副标题以提供更完整的读屏信息
            if (result == string.Empty && !string.IsNullOrWhiteSpace(owner._titleTextBlock.Text))
            {
                result = owner._titleTextBlock.Text;

                if (!string.IsNullOrWhiteSpace(owner._subtitleTextBlock.Text))
                    result += $", {owner._subtitleTextBlock.Text}";
            }

            return result;
        }
    }
}

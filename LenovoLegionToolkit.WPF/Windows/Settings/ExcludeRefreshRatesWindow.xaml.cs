using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Utils;

namespace LenovoLegionToolkit.WPF.Windows.Settings;

/// <summary>
/// 排除刷新率窗口，用于勾选要忽略的刷新率，避免自动切换时误用。
/// </summary>
public partial class ExcludeRefreshRatesWindow
{
    private readonly RefreshRateFeature _feature = IoCContainer.Resolve<RefreshRateFeature>();
    private readonly ApplicationSettings _settings = IoCContainer.Resolve<ApplicationSettings>();

    /// <summary>
    /// 初始化窗口并订阅可见性变化。
    /// </summary>
    public ExcludeRefreshRatesWindow()
    {
        InitializeComponent();

        IsVisibleChanged += PickProcessesWindow_IsVisibleChanged;
    }

    private async void PickProcessesWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            await RefreshAsync();
    }

    /// <summary>
    /// 加载设备支持的刷新率与已排除项，并生成可勾选列表。
    /// </summary>
    private async Task RefreshAsync()
    {
        _loader.IsLoading = true;

        var loadingTask = Task.Delay(500);

        var refreshRates = await _feature.GetAllStatesAsync();
        var excluded = _settings.Store.ExcludedRefreshRates;

        if (refreshRates.IsEmpty())
        {
            await Task.Delay(500);

            // 未检测到刷新率时询问用户是重试还是关闭窗口
            var result = await MessageBoxHelper.ShowAsync(this,
                Resource.ExcludeRefreshRatesWindow_NoRefreshRatesFound_Title,
                Resource.ExcludeRefreshRatesWindow_NoRefreshRatesFound_Message,
                Resource.TryAgain,
                Resource.Cancel);

            if (result)
                await RefreshAsync();
            else
                Close();

            return;
        }

        _list.Items.Clear();

        var items = refreshRates
            .Union(excluded)
            .Distinct()
            .OrderBy(rr => rr.Frequency);

        foreach (var refreshRate in items)
        {
            var item = new ListItem(refreshRate)
            {
                IsChecked = !excluded.Contains(refreshRate)
            };
            _list.Items.Add(item);
        }

        await loadingTask;

        _loader.IsLoading = false;
    }

    /// <summary>
    /// 将未勾选的刷新率写入排除列表并持久化。
    /// </summary>
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var excludedRefreshRates = _list.Items.OfType<ListItem>()
            .Where(li => !li.IsChecked)
            .Select(li => li.RefreshRate);

        _settings.Store.ExcludedRefreshRates.Clear();
        _settings.Store.ExcludedRefreshRates.AddRange(excludedRefreshRates);
        _settings.SynchronizeStore();

        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 刷新率列表中的单项控件，左侧为勾选框、右侧为刷新率显示名。
    /// </summary>
    private class ListItem : UserControl
    {
        private readonly Grid _grid = new()
        {
            Margin = new(8, 4, 0, 16),
            ColumnDefinitions =
            {
                new() { Width = new(32, GridUnitType.Pixel) },
                new() { Width = new(1, GridUnitType.Star) },
            },
        };

        private readonly CheckBox _checkBox = new();

        private readonly TextBlock _nameTextBox = new()
        {
            FlowDirection = FlowDirection.LeftToRight,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };

        /// <summary>当前列表项对应的刷新率。</summary>
        public RefreshRate RefreshRate { get; }

        /// <summary>获取或设置是否保留该刷新率（勾选表示不排除）。</summary>
        public bool IsChecked
        {
            get => _checkBox.IsChecked ?? false;
            set => _checkBox.IsChecked = value;
        }

        /// <summary>
        /// 初始化列表项。
        /// </summary>
        /// <param name="refreshRate">关联的刷新率。</param>
        public ListItem(RefreshRate refreshRate)
        {
            RefreshRate = refreshRate;

            InitializeComponent();
        }

        /// <summary>
        /// 组装列表项布局，并为勾选框设置辅助技术的关联标签。
        /// </summary>
        private void InitializeComponent()
        {
            _nameTextBox.Text = RefreshRate.DisplayName;

            Grid.SetColumn(_checkBox, 0);
            Grid.SetColumn(_nameTextBox, 1);

            _grid.Children.Add(_checkBox);
            _grid.Children.Add(_nameTextBox);

            AutomationProperties.SetLabeledBy(_checkBox, _nameTextBox);

            Content = _grid;
        }
    }
}

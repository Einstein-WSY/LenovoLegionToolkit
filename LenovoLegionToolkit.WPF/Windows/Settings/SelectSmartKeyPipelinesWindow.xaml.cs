using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation;
using LenovoLegionToolkit.Lib.Automation.Pipeline;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.WPF.Resources;

namespace LenovoLegionToolkit.WPF.Windows.Settings;

/// <summary>
/// 智能键动作选择窗口，为单击或双击智能键绑定自动化管道。
/// </summary>
public partial class SelectSmartKeyPipelinesWindow
{
    private readonly AutomationProcessor _automationProcessor = IoCContainer.Resolve<AutomationProcessor>();
    private readonly ApplicationSettings _settings = IoCContainer.Resolve<ApplicationSettings>();

    private readonly bool _isDoublePress;

    /// <summary>
    /// 读取或写入当前（单击/双击）对应的主动作 ID；为 <see langword="null"/> 表示交由本应用处理。
    /// </summary>
    private Guid? SettingsStoreGuid
    {
        get => _isDoublePress ? _settings.Store.SmartKeyDoublePressActionId : _settings.Store.SmartKeySinglePressActionId;
        set
        {
            if (_isDoublePress)
                _settings.Store.SmartKeyDoublePressActionId = value;
            else
                _settings.Store.SmartKeySinglePressActionId = value;
        }
    }

    /// <summary>
    /// 获取当前（单击/双击）对应的动作管道列表。
    /// </summary>
    private List<Guid> SettingsStoreList => _isDoublePress ? _settings.Store.SmartKeyDoublePressActionList : _settings.Store.SmartKeySinglePressActionList;

    /// <summary>
    /// 初始化窗口。
    /// </summary>
    /// <param name="isDoublePress">为 <see langword="true"/> 时配置双击动作，否则配置单击动作。</param>
    public SelectSmartKeyPipelinesWindow(bool isDoublePress = false)
    {
        _isDoublePress = isDoublePress;

        InitializeComponent();

        Title = _title.Text = isDoublePress
            ? Resource.SettingsPage_SmartKeyDoublePressAction_Title
            : Resource.SettingsPage_SmartKeySinglePressAction_Title;

        IsVisibleChanged += SelectSmartKeyPipelinesWindow_IsVisibleChanged;
    }

    private async void SelectSmartKeyPipelinesWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
            await RefreshAsync();
    }

    /// <summary>
    /// 加载所有未绑定触发器的自动化管道，并回填已选状态。
    /// </summary>
    private async Task RefreshAsync()
    {
        _loader.IsLoading = true;

        _showThisAppToggle.IsChecked = SettingsStoreGuid is null;

        var allPipelines = await _automationProcessor.GetPipelinesAsync();
        var pipelines = allPipelines.Where(p => p.Trigger is null).OrderBy(p => p.Name).ToArray();

        _list.Items.Clear();

        if (pipelines.IsEmpty())
            _list.Items.Add(Resource.SelectSmartKeyPipelinesWindow_List_Empty);

        foreach (var pipeline in pipelines)
        {
            var item = new ListItem(pipeline)
            {
                IsChecked = SettingsStoreList.Exists(x => x == pipeline.Id) || pipeline.Id == SettingsStoreGuid
            };
            _list.Items.Add(item);
        }

        EnableListIfPossible();

        _loader.IsLoading = false;
    }

    private void ShowThisAppToggle_Click(object sender, RoutedEventArgs e) => EnableListIfPossible();

    /// <summary>
    /// 选择"由本应用处理"时禁用管道列表，避免两项配置同时生效。
    /// </summary>
    private void EnableListIfPossible() => _list.IsEnabled = !(_showThisAppToggle.IsChecked ?? false);

    /// <summary>
    /// 保存勾选的管道；若选择了"由本应用处理"则清空自定义动作。
    /// </summary>
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedPipelines = _list.Items.OfType<ListItem>()
            .Where(li => li.IsChecked)
            .Select(li => li.Pipeline.Id)
            .ToArray();

        SettingsStoreList.Clear();

        if (_showThisAppToggle.IsChecked ?? false)
            SettingsStoreGuid = null;
        else
        {
            SettingsStoreList.AddRange(selectedPipelines);
            SettingsStoreGuid = SettingsStoreList.FirstOrDefault();
        }

        _settings.SynchronizeStore();

        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// 管道列表中的单项控件，展示管道名称并提供勾选状态。
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
            TextAlignment = TextAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
        };

        /// <summary>当前列表项对应的自动化管道。</summary>
        public AutomationPipeline Pipeline { get; }

        /// <summary>获取或设置该管道是否被选中。</summary>
        public bool IsChecked
        {
            get => _checkBox.IsChecked ?? false;
            set => _checkBox.IsChecked = value;
        }

        /// <summary>
        /// 初始化列表项。
        /// </summary>
        /// <param name="pipeline">关联的自动化管道。</param>
        public ListItem(AutomationPipeline pipeline)
        {
            Pipeline = pipeline;

            InitializeComponent();
        }

        /// <summary>
        /// 组装列表项布局，并为勾选框设置辅助技术的关联标签。
        /// </summary>
        private void InitializeComponent()
        {
            _nameTextBox.Text = Pipeline.Name;

            Grid.SetColumn(_checkBox, 0);
            Grid.SetColumn(_nameTextBox, 1);

            _grid.Children.Add(_checkBox);
            _grid.Children.Add(_nameTextBox);

            AutomationProperties.SetLabeledBy(_checkBox, _nameTextBox);

            Content = _grid;
        }
    }
}

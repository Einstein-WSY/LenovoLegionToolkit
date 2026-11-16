using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Controls.Custom;
using LenovoLegionToolkit.WPF.Extensions;
using Wpf.Ui.Common;

namespace LenovoLegionToolkit.WPF.Controls;

/// <summary>
/// 带下拉框的功能卡片基类：以卡片形式展示标题、副标题，并通过 <see cref="ComboBox"/> 切换功能状态。
/// </summary>
/// <typeparam name="T">功能状态枚举类型。</typeparam>
public abstract class AbstractComboBoxFeatureCardControl<T> : AbstractRefreshingControl where T : struct
{
    /// <summary>当前卡片绑定的功能实例。</summary>
    protected readonly IFeature<T> Feature = IoCContainer.Resolve<IFeature<T>>();

    private readonly CardControl _cardControl = new();

    private readonly CardHeaderControl _cardHeaderControl = new();

    private readonly ComboBox _comboBox = new();

    /// <summary>卡片图标。</summary>
    protected SymbolRegular Icon
    {
        get => _cardControl.Icon;
        set => _cardControl.Icon = value;
    }

    /// <summary>卡片标题；同时同步到下拉框的无障碍名称。</summary>
    protected string Title
    {
        get => _cardHeaderControl.Title;
        set
        {
            _cardHeaderControl.Title = value;
            AutomationProperties.SetName(_comboBox, value);
        }
    }

    /// <summary>卡片副标题。</summary>
    protected string Subtitle
    {
        get => _cardHeaderControl.Subtitle;
        set => _cardHeaderControl.Subtitle = value;
    }

    /// <summary>卡片警告文本。</summary>
    protected string Warning
    {
        get => _cardHeaderControl.Warning;
        set => _cardHeaderControl.Warning = value;
    }

    protected AbstractComboBoxFeatureCardControl() => InitializeComponent();

    // 以代码方式构建视觉树：卡片 + 头部 + 下拉框
    private void InitializeComponent()
    {
        _comboBox.SelectionChanged += ComboBox_SelectionChanged;
        _comboBox.MinWidth = 165;
        // 刷新完成前先隐藏，避免显示空数据
        _comboBox.Visibility = Visibility.Hidden;
        _comboBox.Margin = new(8, 0, 0, 0);

        _cardHeaderControl.Accessory = GetAccessory(_comboBox);
        _cardControl.Header = _cardHeaderControl;
        _cardControl.Margin = new(0, 0, 0, 8);

        Content = _cardControl;
    }

    private async void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        await OnStateChangeAsync(_comboBox, Feature, e.GetNewValue<T>(), e.GetOldValue<T>());
    }

    /// <summary>尝试获取当前选中项。</summary>
    /// <param name="value">选中项的值。</param>
    /// <returns>成功获取返回 <c>true</c>。</returns>
    protected bool TryGetSelectedItem(out T value) => _comboBox.TryGetSelectedItem(out value);

    /// <summary>下拉框中的选项数量。</summary>
    protected int ItemsCount => _comboBox.Items.Count;

    /// <summary>获取放置在卡片右侧的附件元素，默认返回下拉框本身。</summary>
    /// <param name="comboBox">下拉框。</param>
    /// <returns>附件元素。</returns>
    protected virtual FrameworkElement GetAccessory(ComboBox comboBox) => comboBox;

    /// <summary>获取下拉项显示名称，优先使用 <see cref="IDisplayName"/> 或枚举的显示名特性。</summary>
    /// <param name="value">枚举值。</param>
    /// <returns>显示名称。</returns>
    protected virtual string ComboBoxItemDisplayName(T value) => value switch
    {
        IDisplayName dn => dn.DisplayName,
        Enum e => e.GetDisplayName(),
        _ => value.ToString() ?? throw new InvalidOperationException("Unsupported type")
    };

    /// <summary>刷新下拉框的选项与当前选中值。</summary>
    protected override async Task OnRefreshAsync()
    {
        if (!await Feature.IsSupportedAsync())
            throw new NotSupportedException();

        var items = await Feature.GetAllStatesAsync();
        var selectedItem = await Feature.GetStateAsync();

        _comboBox.SetItems(items, selectedItem, ComboBoxItemDisplayName);
        _comboBox.IsEnabled = items.Length != 0;
        _comboBox.Visibility = Visibility.Visible;
    }

    /// <summary>加载完成后订阅功能状态变更消息，在可见时自动刷新。</summary>
    protected override void OnFinishedLoading()
    {
        MessagingCenter.Subscribe<FeatureStateMessage<T>>(this, () => Dispatcher.InvokeTask(async () =>
        {
            if (!IsVisible)
                return;

            await RefreshAsync();
        }));
    }

    /// <summary>
    /// 处理下拉框选择变化：将新值写入功能，并对异常/延迟做统一处理。
    /// </summary>
    /// <param name="comboBox">触发变化的下拉框。</param>
    /// <param name="feature">目标功能。</param>
    /// <param name="newValue">新值。</param>
    /// <param name="oldValue">旧值。</param>
    protected virtual async Task OnStateChangeAsync(ComboBox comboBox, IFeature<T> feature, T? newValue, T? oldValue)
    {
        var exceptionOccurred = false;

        try
        {
            // 刷新期间或初始化为空时忽略用户操作
            if (IsRefreshing)
                return;

            _comboBox.IsEnabled = false;

            if (oldValue is null)
                return;

            if (!comboBox.TryGetSelectedItem(out T selectedState))
                return;

            var currentState = await feature.GetStateAsync();

            if (selectedState.Equals(currentState))
                return;

            await feature.SetStateAsync(selectedState);
        }
        catch (Exception ex)
        {
            exceptionOccurred = true;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to change state. [feature={GetType().Name}]", ex);

            OnStateChangeException(ex);
        }
        finally
        {
            // 部分硬件需要等待一段时间才能再次接受指令
            var delay = AdditionalStateChangeDelay(oldValue, newValue);
            if (delay > TimeSpan.Zero)
                await Task.Delay(delay);

            _comboBox.IsEnabled = true;
        }

        // 发生异常时重新读取硬件状态，恢复界面显示
        if (exceptionOccurred)
            await RefreshAsync();
    }

    /// <summary>状态变更发生异常时的扩展点。</summary>
    /// <param name="exception">异常。</param>
    protected virtual void OnStateChangeException(Exception exception) { }

    /// <summary>状态变更后额外等待的时间，供派生类按需延长。</summary>
    /// <param name="oldValue">旧值。</param>
    /// <param name="newValue">新值。</param>
    /// <returns>额外延迟。</returns>
    protected virtual TimeSpan AdditionalStateChangeDelay(T? oldValue, T? newValue) => TimeSpan.Zero;
}

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Features;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;
using Wpf.Ui.Common;
using Wpf.Ui.Controls;
using CardControl = LenovoLegionToolkit.WPF.Controls.Custom.CardControl;

namespace LenovoLegionToolkit.WPF.Controls;

/// <summary>
/// 带开关的功能卡片基类：通过 <see cref="ToggleSwitch"/> 在“开/关”两种状态间切换功能。
/// </summary>
/// <typeparam name="T">功能状态枚举类型。</typeparam>
public abstract class AbstractToggleFeatureCardControl<T> : AbstractRefreshingControl where T : struct
{
    /// <summary>当前卡片绑定的功能实例。</summary>
    protected readonly IFeature<T> Feature = IoCContainer.Resolve<IFeature<T>>();

    private readonly CardControl _cardControl = new();

    private readonly CardHeaderControl _cardHeaderControl = new();

    private readonly ToggleSwitch _toggle = new();

    // 防止 Loaded 重复触发时多次订阅消息
    private bool _subscribed;

    /// <summary>卡片图标。</summary>
    protected SymbolRegular Icon
    {
        get => _cardControl.Icon;
        set => _cardControl.Icon = value;
    }

    /// <summary>卡片标题；同时同步到开关的无障碍名称。</summary>
    protected string Title
    {
        get => _cardHeaderControl.Title;
        set
        {
            _cardHeaderControl.Title = value;
            AutomationProperties.SetName(_toggle, value);
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

    /// <summary>开关是否可用。</summary>
    public bool IsToggleEnabled
    {
        get => _toggle.IsEnabled;
        set => _toggle.IsEnabled = value;
    }

    /// <summary>开关打开时对应的功能状态。</summary>
    protected abstract T OnState { get; }

    /// <summary>开关关闭时对应的功能状态。</summary>
    protected abstract T OffState { get; }

    /// <summary>状态切换后额外等待的时间。</summary>
    protected virtual TimeSpan AdditionalStateChangeDelay => TimeSpan.Zero;

    protected AbstractToggleFeatureCardControl()
    {
        InitializeComponent();
    }

    // 以代码方式构建视觉树：卡片 + 头部 + 开关
    private void InitializeComponent()
    {
        _toggle.Click += Toggle_Click;
        // 刷新完成前先隐藏，避免显示错误状态
        _toggle.Visibility = Visibility.Hidden;
        _toggle.Margin = new(8, 0, 0, 0);

        _cardHeaderControl.Accessory = _toggle;
        _cardControl.Header = _cardHeaderControl;
        _cardControl.Margin = new(0, 0, 0, 8);

        Content = _cardControl;
    }

    private async void Toggle_Click(object sender, RoutedEventArgs e) => await OnStateChange(_toggle, Feature);

    /// <summary>根据功能当前状态同步开关的勾选状态。</summary>
    protected override async Task OnRefreshAsync()
    {
        if (!await Feature.IsSupportedAsync())
            throw new NotSupportedException();

        _toggle.IsChecked = OnState.Equals(await Feature.GetStateAsync());
        _toggle.Visibility = Visibility.Visible;
    }

    /// <summary>加载完成后订阅功能状态变更消息，在可见时自动刷新。</summary>
    protected override void OnFinishedLoading()
    {
        if (_subscribed)
            return;

        _subscribed = true;

        MessagingCenter.Subscribe<FeatureStateMessage<T>>(this, () => Dispatcher.InvokeTask(async () =>
        {
            if (!IsVisible)
                return;

            await RefreshAsync();
        }));
    }

    /// <summary>
    /// 处理开关点击：按勾选状态设置对应功能值，并在异常时回滚界面。
    /// </summary>
    /// <param name="toggle">触发事件的开关。</param>
    /// <param name="feature">目标功能。</param>
    protected virtual async Task OnStateChange(ToggleSwitch toggle, IFeature<T> feature)
    {
        var exceptionOccurred = false;

        try
        {
            // 刷新中或状态未知时忽略用户操作
            if (IsRefreshing || toggle.IsChecked is null)
                return;

            _toggle.IsEnabled = false;

            var state = toggle.IsChecked.Value ? OnState : OffState;
            if (state.Equals(await feature.GetStateAsync()))
                return;

            await feature.SetStateAsync(state);
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
            if (AdditionalStateChangeDelay > TimeSpan.Zero)
                await Task.Delay(AdditionalStateChangeDelay);

            _toggle.IsEnabled = true;
        }

        if (exceptionOccurred)
            await RefreshAsync();
    }

    /// <summary>状态变更发生异常时的扩展点。</summary>
    /// <param name="exception">异常。</param>
    protected virtual void OnStateChangeException(Exception exception) { }
}

using System.Windows.Media;

namespace LenovoLegionToolkit.WPF.Controls.KeyboardBacklight.Spectrum.Device;

/// <summary>
/// Spectrum 设备视图中的单个按键/灯区热点：显示当前的选中状态与颜色，并携带其 HID 按键码。
/// </summary>
public partial class SpectrumZoneControl
{
    /// <summary>该热点对应的 HID 按键码。</summary>
    private ushort _keyCode;

    /// <summary>
    /// 热点对应的 HID 按键码；调试构建下会同步显示到提示气泡，便于核对该按键映射。
    /// </summary>
    public ushort KeyCode
    {
        get => _keyCode;
        set
        {
            _keyCode = value;

#if DEBUG
            _button.ToolTip = $"0x{value:X2}";
#endif
        }
    }

    /// <summary>
    /// 热点当前显示的颜色；为空时清除底色。
    /// </summary>
    public Color? Color
    {
        get => (_background.Background as SolidColorBrush)?.Color;
        set
        {
            if (!value.HasValue)
                _background.Background = null;
            else if (_background.Background is SolidColorBrush brush)
                // 复用已有画刷，减少每次取色的对象分配。
                brush.Color = value.Value;
            else
                _background.Background = new SolidColorBrush(value.Value);
        }
    }

    /// <summary>
    /// 热点是否被选中（选中后按键在设备图上高亮）。
    /// </summary>
    public bool? IsChecked
    {
        get => _button.IsChecked;
        set => _button.IsChecked = value;
    }

    /// <summary>
    /// 初始化控件并加载对应的 XAML 视图。
    /// </summary>
    public SpectrumZoneControl()
    {
        InitializeComponent();
    }
}

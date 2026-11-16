using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Listeners;

/// <summary>
/// 系统主题监听器：监听注册表中的深色模式与强调色变化（由 <see cref="SystemTheme"/> 提供注册表监视），
/// 变化时触发 <see cref="Changed"/>，供界面即时切换主题色。
/// </summary>
public class SystemThemeListener : IListener<EventArgs>
{
    /// <summary>
    /// 系统主题变化事件。深色模式切换或强调色实际变化时触发。
    /// </summary>
    public event EventHandler<EventArgs>? Changed;

    private IDisposable? _darkModeListener;
    private IDisposable? _colorizationColorListener;

    private RGBColor? _currentRegColor;

    private bool _started;

    /// <inheritdoc/>
    public Task StartAsync()
    {
        if (_started)
            return Task.CompletedTask;

        // 两个监听器分别监视注册表中深色模式与应用强调色两个键，Dispose 时会解除注册表通知
        _darkModeListener = SystemTheme.GetDarkModeListener(OnDarkModeChanged);
        _colorizationColorListener = SystemTheme.GetColorizationColorListener(OnColorizationColorChanged);

        _started = true;

        return Task.CompletedTask;
    }

    private void OnDarkModeChanged()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnColorizationColorChanged()
    {
        try
        {
            var color = SystemTheme.GetColorizationColor();

            // Ignore alpha channel transition events
            // 系统在淡入淡出动画时会多次写入同一颜色（仅透明度不同），按 RGB 去重避免重复刷新界面
            if (color.Equals(_currentRegColor))
                return;

            _currentRegColor = color;

            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to notify on accent color change.", ex);
        }
    }

    /// <inheritdoc/>
    public Task StopAsync()
    {
        // 释放注册表监视句柄，解除注册表变更通知，防止停止后仍触发事件
        _darkModeListener?.Dispose();
        _colorizationColorListener?.Dispose();

        _started = false;

        return Task.CompletedTask;
    }
}

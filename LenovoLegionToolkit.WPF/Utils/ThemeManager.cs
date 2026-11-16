using System;
using System.Windows;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Listeners;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Extensions;

namespace LenovoLegionToolkit.WPF.Utils;

/// <summary>
/// 主题管理器，负责应用明暗主题与强调色，并监听系统主题变化自动刷新。
/// </summary>
public class ThemeManager
{
    // 无法读取系统/自定义颜色时使用的默认强调色
    private static readonly RGBColor DefaultAccentColor = new(255, 33, 33);

    private readonly ApplicationSettings _settings;
    private readonly SystemThemeListener _listener;

    /// <summary>主题与强调色应用完成后触发。</summary>
    public event EventHandler? ThemeApplied;

    /// <summary>
    /// 构造主题管理器并订阅系统主题变更事件。
    /// </summary>
    /// <param name="systemThemeListener">系统主题监听器。</param>
    /// <param name="settings">应用设置。</param>
    public ThemeManager(SystemThemeListener systemThemeListener, ApplicationSettings settings)
    {
        _listener = systemThemeListener;
        _settings = settings;

        // 系统主题变化时切回 UI 线程重新应用主题
        _listener.Changed += (_, _) => Application.Current.Dispatcher.Invoke(Apply);
    }

    /// <summary>应用当前设置的主题与强调色。</summary>
    public void Apply()
    {
        SetTheme();
        SetColor();

        ThemeApplied?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 获取当前应使用的强调色：自定义、跟随系统或默认色。
    /// </summary>
    /// <returns>强调色。</returns>
    public RGBColor GetAccentColor()
    {
        switch (_settings.Store.AccentColorSource)
        {
            case AccentColorSource.Custom:
                return _settings.Store.AccentColor ?? DefaultAccentColor;
            case AccentColorSource.System:
                try
                {
                    return SystemTheme.GetAccentColor();
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Couldn't check system accent color; using default.", ex);

                    return DefaultAccentColor;
                }
            default:
                return DefaultAccentColor;
        }
    }

    // 根据设置判断是否应使用深色主题，跟随系统时读取系统状态
    private bool IsDarkMode()
    {
        var theme = _settings.Store.Theme;

        switch (theme)
        {
            case Theme.Dark:
                return true;
            case Theme.Light:
                return false;
            case Theme.System:
                try
                {
                    return SystemTheme.IsDarkMode();
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Couldn't check system theme; assuming Dark Mode.", ex);

                    return true;
                }
            default:
                return true;
        }
    }

    // 应用主题，使用 Mica 背景并跳过窗口背景重绘
    private void SetTheme()
    {
        var theme = IsDarkMode() ? Wpf.Ui.Appearance.ThemeType.Dark : Wpf.Ui.Appearance.ThemeType.Light;
        Wpf.Ui.Appearance.Theme.Apply(theme, Wpf.Ui.Appearance.BackgroundType.Mica, false);
    }

    // 将强调色统一应用到主色/次色/三级色
    private void SetColor()
    {
        var accentColor = GetAccentColor().ToColor();
        Wpf.Ui.Appearance.Accent.Apply(systemAccent: accentColor,
            primaryAccent: accentColor,
            secondaryAccent: accentColor,
            tertiaryAccent: accentColor);
    }
}

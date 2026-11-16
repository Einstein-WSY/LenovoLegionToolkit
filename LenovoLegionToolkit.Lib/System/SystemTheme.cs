using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LenovoLegionToolkit.Lib.System;

/// <summary>
/// Windows 系统主题（深色/浅色、强调色）读取与监听辅助类。
/// </summary>
public static partial class SystemTheme
{
    private const string REGISTRY_HIVE = "HKEY_CURRENT_USER";

    // 个性化设置下的注册表路径：AppsUseLightTheme 决定应用使用浅色(1)还是深色(0)
    private const string PERSONALIZE_REGISTRY_PATH = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string APPS_USE_LIGHT_THEME_REGISTRY_KEY = "AppsUseLightTheme";

    // DWM 注册表路径：ColorizationColor 保存窗口标题栏的着色（ARGB）
    private const string DWM_REGISTRY_PATH = @"Software\Microsoft\Windows\DWM";
    private const string DWM_COLORIZATION_COLOR_REGISTRY_KEY = "ColorizationColor";

    /// <summary>
    /// 判断系统当前是否使用深色模式。
    /// </summary>
    /// <returns>深色模式返回 true。</returns>
    /// <exception cref="InvalidOperationException">注册表键不存在或读取失败时抛出。</exception>
    public static bool IsDarkMode()
    {
        var registryValue = Registry.GetValue(REGISTRY_HIVE, PERSONALIZE_REGISTRY_PATH, APPS_USE_LIGHT_THEME_REGISTRY_KEY, -1);
        // 用 -1 作为"读取失败"的哨兵值，区别于合法的 0/1
        if (registryValue == -1)
            throw new InvalidOperationException($"Couldn't read the {APPS_USE_LIGHT_THEME_REGISTRY_KEY} setting");

        return registryValue == 0;
    }

    /// <summary>
    /// 获取窗口标题栏的着色颜色。
    /// </summary>
    /// <returns>标题栏颜色。</returns>
    /// <exception cref="InvalidOperationException">注册表键不存在或读取失败时抛出。</exception>
    public static RGBColor GetColorizationColor()
    {
        var registryValue = Registry.GetValue(REGISTRY_HIVE, DWM_REGISTRY_PATH, DWM_COLORIZATION_COLOR_REGISTRY_KEY, -1);
        if (registryValue == -1)
            throw new InvalidOperationException($"Couldn't read the {DWM_COLORIZATION_COLOR_REGISTRY_KEY} setting");

        // DWM 以 0xAARRGGBB 的 DWORD 存储；小端字节序下 bytes[0..3] = BB,GG,RR,AA
        var bytes = BitConverter.GetBytes(registryValue);
        return new(bytes[2], bytes[1], bytes[0]);
    }

    /// <summary>
    /// 获取系统强调色（主题强调色）。
    /// 深色模式下需取"浅一档"的系统色，浅色模式下取"深一档"，以保证在对应背景上的对比度。
    /// </summary>
    /// <returns>强调色。</returns>
    public static RGBColor GetAccentColor()
    {
        var colorName = IsDarkMode() ? "SystemAccentLight2" : "SystemAccentDark1";
        return GetUxThemeImmersiveColor(colorName);
    }

    /// <summary>
    /// 通过 uxtheme.dll 的未文档化接口查询 Windows 沉浸式（Immersive）颜色值。
    /// </summary>
    /// <param name="name">颜色名称（不含 "Immersive" 前缀）。</param>
    /// <returns>对应的 RGB 颜色。</returns>
    /// <exception cref="Win32Exception">颜色名称无法解析时抛出。</exception>
    private static RGBColor GetUxThemeImmersiveColor(string name)
    {
        var colorType = GetImmersiveColorTypeFromName("Immersive" + name);

        // 0xFFFFFFFF 是接口约定表示"未找到该颜色名"的错误返回值
        if (colorType == 0xFFFFFFFF)
            throw new Win32Exception($"Couldn't get color \"{name}\"");

        // 先取当前用户使用的颜色集（注册表偏好），再按颜色集 + 颜色类型取具体色值
        var activeColorSet = GetImmersiveUserColorSetPreference(false, false);
        var nativeColor = GetImmersiveColorFromColorSetEx(activeColorSet, colorType, false, 0);

        // 返回值为 0xAABBGGRR（低字节为 R），因此低 8 位是红、次 8 位是绿、第 3 个字节是蓝
        var r = (byte)((0x000000FF & nativeColor) >> 0);
        var g = (byte)((0x0000FF00 & nativeColor) >> 8);
        var b = (byte)((0x00FF0000 & nativeColor) >> 16);

        return new(r, g, b);
    }

    /// <summary>
    /// 监听深色模式变化。
    /// </summary>
    /// <param name="callback">主题变化时触发的回调。</param>
    /// <returns>可释放对象，释放后停止监听。</returns>
    internal static IDisposable GetDarkModeListener(Action callback)
    {
        return Registry.ObserveValue(REGISTRY_HIVE, PERSONALIZE_REGISTRY_PATH, APPS_USE_LIGHT_THEME_REGISTRY_KEY, callback);
    }

    /// <summary>
    /// 监听标题栏着色颜色变化。
    /// </summary>
    /// <param name="callback">颜色变化时触发的回调。</param>
    /// <returns>可释放对象，释放后停止监听。</returns>
    internal static IDisposable GetColorizationColorListener(Action callback)
    {
        return Registry.ObserveValue(REGISTRY_HIVE, DWM_REGISTRY_PATH, DWM_COLORIZATION_COLOR_REGISTRY_KEY, callback);
    }

    #region Native

    // ReSharper disable StringLiteralTypo

    // 以下三个 uxtheme.dll 导出函数未提供公开符号名，只能按序号（Ordinal）导入：
    // "#95A" GetImmersiveColorFromColorSetEx / "#96W" GetImmersiveColorTypeFromName / "#98A" GetImmersiveUserColorSetPreference
    // 序号导入依赖系统版本，属未文档化 API，故用 partial + LibraryImport 源生成声明
    [LibraryImport("uxtheme.dll", EntryPoint = "#95A")]
    private static partial uint GetImmersiveColorFromColorSetEx(uint immersiveColorSet, uint immersiveColorType, [MarshalAs(UnmanagedType.Bool)] bool ignoreHighContrast, uint highContrastCacheMode);

    [LibraryImport("uxtheme.dll", EntryPoint = "#96W", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint GetImmersiveColorTypeFromName(string name);

    [LibraryImport("uxtheme.dll", EntryPoint = "#98A")]
    private static partial uint GetImmersiveUserColorSetPreference([MarshalAs(UnmanagedType.Bool)] bool forceCheckRegistry, [MarshalAs(UnmanagedType.Bool)] bool skipCheckOnFail);

    // ReSharper restore StringLiteralTypo

    #endregion
}

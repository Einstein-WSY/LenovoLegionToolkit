using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Humanizer;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.Resources;
using LenovoLegionToolkit.WPF.Windows.Utils;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace LenovoLegionToolkit.WPF.Utils;

/// <summary>
/// 本地化辅助类，负责读取/保存界面语言、切换 Culture，以及提供日期格式与文本方向等本地化信息。
/// </summary>
public static class LocalizationHelper
{
    // 语言代码保存文件路径（AppData/lang）
    private static readonly string LanguagePath = Path.Combine(Folders.AppData, "lang");

    private static readonly CultureInfo DefaultLanguage = new("en");

    /// <summary>应用支持的所有语言。</summary>
    public static readonly CultureInfo[] Languages = [
        DefaultLanguage,
        new("ar"),
        new("bg"),
        new("cs"),
        new("de"),
        new("el"),
        new("es"),
        new("fr"),
        new("hu"),
        new("it"),
        new("ja"),
        new("lv"),
        new("nl-nl"),
        new("pl"),
        new("pt"),
        new("pt-br"),
        new("ro"),
        new("ru"),
        new("sk"),
        new("tr"),
        new("uk"),
        new("vi"),
        new("zh-hans"),
        new("zh-hant"),
        new("uz-latn-uz") // HACK: Karakalpak is not a recognized culture by msbuild, so we use this one as workaround instead.
    ];

    /// <summary>当前语言对应的文本流向（从右到左或从左到右）。</summary>
    public static FlowDirection Direction => Resource.Culture?.TextInfo.IsRightToLeft ?? false
        ? FlowDirection.RightToLeft
        : FlowDirection.LeftToRight;

    // 短日期格式缓存，避免重复查询系统区域设置
    private static string? _dateFormat;

    /// <summary>系统短日期格式，读取失败时回退为 dd/M/yyyy。</summary>
    public static string ShortDateFormat
    {
        get
        {
            if (_dateFormat is not null)
                return _dateFormat;

            _dateFormat = GetSystemShortDateFormat() ?? "dd/M/yyyy";
            return _dateFormat;
        }
    }

    /// <summary>
    /// 获取语言的原生显示名称（标题式大小写），并强制从左到右显示。
    /// </summary>
    /// <param name="culture">语言。</param>
    /// <returns>显示名称。</returns>
    public static string LanguageDisplayName(CultureInfo culture)
    {
        var name = culture.NativeName.Transform(culture, To.TitleCase);

        // uz-latn-uz 实际用于代表卡拉卡尔帕克语，显示为友好名称
        if (culture.IetfLanguageTag.Equals("uz-latn-uz", StringComparison.InvariantCultureIgnoreCase))
            name = "Karakalpak";

        return ForceLeftToRight(name);
    }

    /// <summary>
    /// 在从右到左语言环境下，通过不可见方向控制符强制文本从左到右显示。
    /// </summary>
    /// <param name="str">原始文本。</param>
    /// <returns>处理后的文本。</returns>
    public static string ForceLeftToRight(string str)
    {
        if (Resource.Culture?.TextInfo.IsRightToLeft ?? false)
            return "\u200e" + str + "\u200e";
        return str;
    }

    /// <summary>
    /// 设置界面语言；首次启动（interactive）且无已保存语言时弹出语言选择窗口。
    /// </summary>
    /// <param name="interactive">是否允许交互式选择语言。</param>
    public static async Task SetLanguageAsync(bool interactive = false)
    {
        CultureInfo? cultureInfo = null;

        if (interactive && await GetLanguageFromFile() is null)
        {
            var window = new LanguageSelectorWindow(Languages, DefaultLanguage);
            window.Show();
            cultureInfo = await window.ShouldContinue;
            if (cultureInfo is not null)
                await SaveLanguageToFileAsync(cultureInfo);
        }

        cultureInfo ??= await GetLanguageAsync();

        SetLanguageInternal(cultureInfo);
    }

    /// <summary>
    /// 保存并应用指定语言。
    /// </summary>
    /// <param name="cultureInfo">目标语言。</param>
    public static async Task SetLanguageAsync(CultureInfo cultureInfo)
    {
        await SaveLanguageToFileAsync(cultureInfo);
        SetLanguageInternal(cultureInfo);
    }

    /// <summary>
    /// 读取已保存的语言；不存在或无效时回退到默认语言并写回。
    /// </summary>
    /// <returns>当前语言。</returns>
    public static async Task<CultureInfo> GetLanguageAsync()
    {
        var cultureInfo = await GetLanguageFromFile();
        if (cultureInfo is null)
        {
            cultureInfo = DefaultLanguage;
            await SaveLanguageToFileAsync(cultureInfo);
        }
        return cultureInfo;
    }

    // 从文件读取语言代码，并校验其属于支持列表
    private static async Task<CultureInfo?> GetLanguageFromFile()
    {
        try
        {
            var name = await File.ReadAllTextAsync(LanguagePath);
            var cultureInfo = new CultureInfo(name);
            if (!Languages.Contains(cultureInfo))
                throw new InvalidOperationException("Unknown language");
            return cultureInfo;
        }
        catch
        {
            return null;
        }
    }

    private static Task SaveLanguageToFileAsync(CultureInfo cultureInfo) => File.WriteAllTextAsync(LanguagePath, cultureInfo.Name);

    // 应用 Culture：数值/日期格式固定为 en，界面文本使用目标语言
    private static void SetLanguageInternal(CultureInfo cultureInfo)
    {
        Thread.CurrentThread.CurrentCulture = new CultureInfo("en");
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en");

        Thread.CurrentThread.CurrentUICulture = cultureInfo;
        CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

        // 三个资源程序集都需要同步 Culture，才能正确切换本地化字符串
        Resource.Culture = cultureInfo;
        Lib.Resources.Resource.Culture = cultureInfo;
        Lib.Automation.Resources.Resource.Culture = cultureInfo;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Applied culture: {cultureInfo.Name}");
    }

    // 通过 Win32 GetLocaleInfoEx 读取系统短日期格式
    private static unsafe string? GetSystemShortDateFormat()
    {
        var ptr = IntPtr.Zero;
        try
        {
            // 先查询所需缓冲区长度，再分配非托管内存读取内容
            var length = PInvoke.GetLocaleInfoEx(null, PInvoke.LOCALE_SSHORTDATE, null, 0);
            if (length == 0)
                return null;

            ptr = Marshal.AllocHGlobal(sizeof(char) * length);
            var charPtr = new PWSTR((char*)ptr.ToPointer());

            length = PInvoke.GetLocaleInfoEx(null, PInvoke.LOCALE_SSHORTDATE, charPtr, length);
            return length == 0 ? null : charPtr.ToString();
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }
}

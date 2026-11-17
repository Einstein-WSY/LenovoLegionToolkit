using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Settings;

/// <summary>
/// 应用主设置：持久化到 settings.json，涵盖外观、通知、电源模式映射、智能按键等全局配置。
/// </summary>
public class ApplicationSettings : AbstractSettings<ApplicationSettings.ApplicationSettingsStore>
{
    /// <summary>
    /// 各类通知的开关集合。
    /// </summary>
    public class Notifications
    {
        /// <summary>
        /// 是否提示 CapsLock/NumLock 状态，默认关闭。
        /// </summary>
        public bool CapsNumLock { get; set; }

        /// <summary>
        /// 是否提示 Fn 锁状态，默认关闭。
        /// </summary>
        public bool FnLock { get; set; }

        /// <summary>
        /// 是否提示触控板锁定状态，默认开启。
        /// </summary>
        public bool TouchpadLock { get; set; } = true;

        /// <summary>
        /// 是否提示键盘背光变化，默认开启。
        /// </summary>
        public bool KeyboardBacklight { get; set; } = true;

        /// <summary>
        /// 是否提示摄像头锁定状态，默认开启。
        /// </summary>
        public bool CameraLock { get; set; } = true;

        /// <summary>
        /// 是否提示麦克风状态，默认开启。
        /// </summary>
        public bool Microphone { get; set; } = true;

        /// <summary>
        /// 是否提示性能模式切换，默认关闭。
        /// </summary>
        public bool PowerMode { get; set; }

        /// <summary>
        /// 是否提示刷新率切换，默认开启。
        /// </summary>
        public bool RefreshRate { get; set; } = true;

        /// <summary>
        /// 是否提示电源适配器插拔，默认关闭。
        /// </summary>
        public bool ACAdapter { get; set; }

        /// <summary>
        /// 是否提示智能按键触发，默认关闭。
        /// </summary>
        public bool SmartKey { get; set; }

        /// <summary>
        /// 是否提示自动化规则执行，默认开启。
        /// </summary>
        public bool AutomationNotification { get; set; } = true;
    }

    /// <summary>
    /// settings.json 的实际数据结构。新增字段时会自动以属性初始值作为默认值，实现向后兼容。
    /// </summary>
    public class ApplicationSettingsStore
    {
        /// <summary>
        /// 界面主题（跟随系统/浅色/深色）。
        /// </summary>
        public Theme Theme { get; set; }

        /// <summary>
        /// 自定义强调色；为空时使用系统强调色。
        /// </summary>
        public RGBColor? AccentColor { get; set; }

        /// <summary>
        /// 强调色来源（系统或自定义）。
        /// </summary>
        public AccentColorSource AccentColorSource { get; set; }

        /// <summary>
        /// 性能模式与 Windows 电源模式/电源计划的映射方式，默认映射 Windows 电源模式。
        /// </summary>
        public PowerModeMappingMode PowerModeMappingMode { get; set; } = PowerModeMappingMode.WindowsPowerMode;

        /// <summary>
        /// 各性能模式对应的电源计划 GUID（映射方式为电源计划时使用）。
        /// </summary>
        public Dictionary<PowerModeState, Guid> PowerPlans { get; set; } = [];

        /// <summary>
        /// 各性能模式对应的 Windows 电源模式（映射方式为电源模式时使用）。
        /// </summary>
        public Dictionary<PowerModeState, WindowsPowerMode> PowerModes { get; set; } = [];

        /// <summary>
        /// 关闭主窗口时是否最小化到托盘，默认开启。
        /// </summary>
        public bool MinimizeToTray { get; set; } = true;

        /// <summary>
        /// 是否在关闭窗口时直接最小化而非退出，默认关闭。
        /// </summary>
        public bool MinimizeOnClose { get; set; }

        /// <summary>
        /// 上次窗口尺寸，用于恢复窗口大小。
        /// </summary>
        public WindowSize? WindowSize { get; set; }

        /// <summary>
        /// 是否完全禁用所有通知，默认关闭。
        /// </summary>
        public bool DontShowNotifications { get; set; }

        /// <summary>
        /// 通知显示位置，默认底部居中。
        /// </summary>
        public NotificationPosition NotificationPosition { get; set; } = NotificationPosition.BottomCenter;

        /// <summary>
        /// 通知显示时长，默认普通。
        /// </summary>
        public NotificationDuration NotificationDuration { get; set; } = NotificationDuration.Normal;

        /// <summary>
        /// 通知是否置于所有窗口最上层，默认关闭。
        /// </summary>
        public bool NotificationAlwaysOnTop { get; set; }

        /// <summary>
        /// 是否在所有显示器上显示通知，默认关闭。
        /// </summary>
        public bool NotificationOnAllScreens { get; set; }

        /// <summary>
        /// 各类通知的开关集合。
        /// </summary>
        public Notifications Notifications { get; set; } = new();

        /// <summary>
        /// 温度显示单位（摄氏度/华氏度）。
        /// </summary>
        public TemperatureUnit TemperatureUnit { get; set; }

        /// <summary>
        /// Fn+R 循环切换刷新率时排除的刷新率列表。
        /// </summary>
        public List<RefreshRate> ExcludedRefreshRates { get; set; } = [];

        /// <summary>
        /// 智能按键单次按下所绑定动作的 ID。
        /// </summary>
        public Guid? SmartKeySinglePressActionId { get; set; }

        /// <summary>
        /// 智能按键双击所绑定动作的 ID。
        /// </summary>
        public Guid? SmartKeyDoublePressActionId { get; set; }

        /// <summary>
        /// 智能按键单次按下可选择的动作列表。
        /// </summary>
        public List<Guid> SmartKeySinglePressActionList { get; set; } = [];

        /// <summary>
        /// 智能按键双击可选择的动作列表。
        /// </summary>
        public List<Guid> SmartKeyDoublePressActionList { get; set; } = [];

        /// <summary>
        /// 是否把当前亮度同步到所有电源计划，默认关闭。
        /// </summary>
        public bool SynchronizeBrightnessToAllPowerPlans { get; set; }

        /// <summary>
        /// 智能 Fn 锁生效的修饰键组合。
        /// </summary>
        public ModifierKey SmartFnLockFlags { get; set; }

        /// <summary>
        /// 是否在重启时重置电池"自上次充电以来"计时器，默认关闭。
        /// </summary>
        public bool ResetBatteryOnSinceTimerOnReboot { get; set; }
    }

    /// <summary>
    /// 初始化应用设置，并注册旧版本电源计划 ID 的兼容转换器。
    /// </summary>
    public ApplicationSettings() : base("settings.json")
    {
        // 先注册自定义转换器，使其优先于默认的 Guid 转换逻辑处理旧格式字符串
        JsonSerializerSettings.Converters.Add(new LegacyPowerPlanInstanceIdToGuidConverter());
    }
}

/// <summary>
/// 兼容 2.12.0 之前的电源计划 ID 格式：旧版本把计划保存为 "Microsoft:PowerPlan\{GUID}" 形式的字符串，
/// 新版本统一使用纯 GUID。该转换器在读取时剥离前后缀，写出时仍走默认 GUID 序列化。
/// </summary>
internal class LegacyPowerPlanInstanceIdToGuidConverter : JsonConverter // Introduced in 2.12.0
{
    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override bool CanConvert(Type objectType) => objectType == typeof(Guid);

    /// <inheritdoc/>
    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer) => throw new InvalidOperationException();

    /// <inheritdoc/>
    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        var value = reader.Value?.ToString() ?? string.Empty;

        const string prefix = "Microsoft:PowerPlan\\{";
        const string suffix = "}";

        // 仅当字符串同时包含旧前缀和结尾花括号时才做剥离，避免误伤普通 GUID
        var prefixIndex = value.Contains(prefix, StringComparison.InvariantCulture);
        var suffixIndex = value.IndexOf(suffix, StringComparison.InvariantCulture);

        if (prefixIndex && suffixIndex > 0)
        {
            value = value[..suffixIndex];
            value = value[prefix.Length..];
        }

        return Guid.Parse(value);
    }
}

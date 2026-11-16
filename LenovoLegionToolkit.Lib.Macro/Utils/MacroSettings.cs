using System.Collections.Generic;
using LenovoLegionToolkit.Lib.Settings;

namespace LenovoLegionToolkit.Lib.Macro.Utils;

/// <summary>
/// 宏功能的设置容器，持久化到 <c>macro.json</c>。
/// </summary>
public class MacroSettings() : AbstractSettings<MacroSettings.MacroSettingsStore>("macro.json")
{
    /// <summary>
    /// 宏设置的实际存储内容。
    /// </summary>
    public class MacroSettingsStore
    {
        /// <summary>宏功能是否启用。</summary>
        public bool IsEnabled { get; set; }

        /// <summary>已保存的宏序列，键为触发该宏的输入标识。</summary>
        public Dictionary<MacroIdentifier, MacroSequence> Sequences { get; set; } = [];
    }

    /// <summary>
    /// 配置文件不存在时使用的默认设置。
    /// </summary>
    protected override MacroSettingsStore Default => new();
}

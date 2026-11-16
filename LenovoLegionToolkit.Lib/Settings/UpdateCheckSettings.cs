using System;

namespace LenovoLegionToolkit.Lib.Settings;

/// <summary>
/// 更新检查设置：持久化到 update_check.json，记录上次检查时间与检查频率。
/// </summary>
public class UpdateCheckSettings() : AbstractSettings<UpdateCheckSettings.UpdateCheckSettingsStore>("update_check.json")
{
    /// <summary>
    /// update_check.json 的数据结构。
    /// </summary>
    public class UpdateCheckSettingsStore
    {
        /// <summary>
        /// 上次检查更新的时间；从未检查过时为 null。
        /// </summary>
        public DateTime? LastUpdateCheckDateTime { get; set; }

        /// <summary>
        /// 更新检查频率。
        /// </summary>
        public UpdateCheckFrequency UpdateCheckFrequency { get; set; }
    }

    /// <summary>
    /// 默认设置：从未检查过，检查频率为每天一次。
    /// </summary>
    protected override UpdateCheckSettingsStore Default => new()
    {
        LastUpdateCheckDateTime = null,
        UpdateCheckFrequency = UpdateCheckFrequency.PerDay
    };
}

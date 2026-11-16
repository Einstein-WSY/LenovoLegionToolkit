using static LenovoLegionToolkit.Lib.Settings.GPUOverclockSettings;

namespace LenovoLegionToolkit.Lib.Settings;

/// <summary>
/// GPU 超频设置：持久化到 gpu_oc.json，记录是否启用超频及具体 Offset 参数。
/// </summary>
public class GPUOverclockSettings() : AbstractSettings<GPUOverclockSettingsStore>("gpu_oc.json")
{
    /// <summary>
    /// gpu_oc.json 的数据结构。
    /// </summary>
    public class GPUOverclockSettingsStore
    {
        /// <summary>
        /// 是否启用 GPU 超频。
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// 超频参数；未配置时为全零值（等同不超频）。
        /// </summary>
        public GPUOverclockInfo Info { get; set; } = GPUOverclockInfo.Zero;
    }
}

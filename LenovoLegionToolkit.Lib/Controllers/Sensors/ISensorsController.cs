using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Controllers.Sensors;

/// <summary>
/// 传感器控制器接口：不同机型/固件版本的实现方式不同（见 V1/V2/V3）。
/// </summary>
public interface ISensorsController
{
    /// <summary>
    /// 检测当前机型是否支持该实现。
    /// </summary>
    /// <returns>支持时为 true。</returns>
    Task<bool> IsSupportedAsync();

    /// <summary>
    /// 初始化控制器（建立 WMI 会话、读取静态上限等），应在首次读取数据前调用。
    /// </summary>
    Task PrepareAsync();

    /// <summary>
    /// 读取 CPU/GPU 的实时传感器数据。
    /// </summary>
    /// <returns>传感器数据快照。</returns>
    Task<SensorsData> GetDataAsync();

    /// <summary>
    /// 读取 CPU 与 GPU 风扇的实时转速。
    /// </summary>
    /// <returns>CPU/GPU 风扇转速（RPM）。</returns>
    Task<(int cpuFanSpeed, int gpuFanSpeed)> GetFanSpeedsAsync();
}

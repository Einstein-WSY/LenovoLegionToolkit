using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// 特性（Feature）通用接口。所有硬件能力开关（如 Fn 锁、USB Always-On 等）均实现此接口，
/// <typeparamref name="T"/> 为该特性支持的状态枚举类型。
/// </summary>
/// <typeparam name="T">特性状态枚举类型，必须是值类型（struct）。</typeparam>
public interface IFeature<T> where T : struct
{
    /// <summary>异步检测当前机器是否支持该特性。</summary>
    /// <returns>支持返回 <c>true</c>，否则返回 <c>false</c>。</returns>
    Task<bool> IsSupportedAsync();

    /// <summary>获取该特性所有可用的状态列表。</summary>
    /// <returns>状态枚举值数组。</returns>
    Task<T[]> GetAllStatesAsync();

    /// <summary>读取当前生效的状态。</summary>
    /// <returns>当前状态枚举值。</returns>
    /// <exception cref="Exception">底层 WMI/UEFI 读取失败或返回未定义值时抛出。</exception>
    Task<T> GetStateAsync();

    /// <summary>设置特性的新状态。</summary>
    /// <param name="state">要应用的目标状态。</param>
    /// <exception cref="Exception">底层写入失败时抛出。</exception>
    Task SetStateAsync(T state);
}

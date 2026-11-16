using System.Collections.Generic;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.WPF.CLI.Features;

/// <summary>
/// CLI 功能注册接口，定义统一的名称、支持性检查与值读写操作。
/// </summary>
public interface IFeatureRegistration
{
    /// <summary>功能名称（CLI 参数中使用的标识）。</summary>
    string Name { get; }
    /// <summary>判断功能是否受当前设备支持。</summary>
    /// <returns>受支持返回 <c>true</c>。</returns>
    Task<bool> IsSupportedAsync();
    /// <summary>获取功能的所有可选值。</summary>
    /// <returns>可选值字符串集合。</returns>
    Task<IEnumerable<string>> GetValuesAsync();
    /// <summary>获取功能当前值。</summary>
    /// <returns>当前值字符串。</returns>
    Task<string> GetValueAsync();
    /// <summary>设置功能值。</summary>
    /// <param name="value">目标值字符串。</param>
    Task SetValueAsync(string value);
}

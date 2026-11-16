using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供 <see cref="ManagementObjectSearcher"/> 的扩展方法。
/// </summary>
public static class ManagementObjectSearcherExtensions
{
    /// <summary>
    /// 以异步方式执行 WMI 查询并返回结果集合。
    /// <para>
    /// WMI 查询是同步阻塞的，这里通过 <see cref="Task.Run(Action)"/> 放到线程池执行，避免阻塞 UI 线程。
    /// </para>
    /// </summary>
    /// <param name="mos">WMI 查询器。</param>
    /// <returns>查询结果的异步序列。</returns>
    public static Task<IEnumerable<ManagementBaseObject>> GetAsync(this ManagementObjectSearcher mos) => Task.Run(() => mos.Get().Cast<ManagementBaseObject>());
}

using System;
using System.Diagnostics;

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// 对 <see cref="PerformanceCounter"/> 的容错封装：读取失败或计数器不可用时返回 0，避免影响调用方。
/// </summary>
/// <param name="categoryName">性能计数器类别名称。</param>
/// <param name="counterName">性能计数器名称。</param>
/// <param name="instanceName">性能计数器实例名称。</param>
public class SafePerformanceCounter(string categoryName, string counterName, string instanceName)
{
    private PerformanceCounter? _performanceCounter;

    /// <summary>
    /// 读取计数器的下一个采样值；发生异常或计数器未就绪时返回 0。
    /// </summary>
    /// <returns>当前采样值（浮点数）。</returns>
    public float NextValue()
    {
        try
        {
            TryCreateIfNeeded();
            return _performanceCounter?.NextValue() ?? 0f;
        }
        catch
        {
            return 0f;
        }
    }

    /// <summary>
    /// 重置内部计数器实例；下次读取时会重新创建底层 <see cref="PerformanceCounter"/>。
    /// </summary>
    public void Reset()
    {
        _performanceCounter = null;
        _ = NextValue();
    }

    /// <summary>
    /// 首次使用时惰性创建底层计数器；创建失败时保持为 null 并记录日志。
    /// </summary>
    private void TryCreateIfNeeded()
    {
        if (_performanceCounter is not null)
            return;

        try
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Creating performance counter. [categoryName={categoryName}, counterName={counterName}, instanceName={instanceName}]");

            _performanceCounter = new(categoryName, counterName, instanceName);
        }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Failed to create performance counter. [categoryName={categoryName}, counterName={counterName}, instanceName={instanceName}]", ex);

            _performanceCounter = null;
        }
    }
}

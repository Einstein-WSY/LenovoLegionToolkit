using System;

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// 带锁保护的线程安全计数器，计数下限为 0。
/// </summary>
public class ThreadSafeCounter
{
    private readonly object _lock = new();

    private int _counter;

    /// <summary>
    /// 将计数减一（不会低于 0）。
    /// </summary>
    /// <returns>若调用前计数已小于 1 则返回 true，否则返回 false。</returns>
    public bool Decrement()
    {
        lock (_lock)
        {
            var value = _counter < 1;
            _counter = Math.Max(0, _counter - 1);
            return value;
        }
    }

    /// <summary>
    /// 将计数加一。
    /// </summary>
    public void Increment()
    {
        lock (_lock)
        {
            _counter++;
        }
    }
}

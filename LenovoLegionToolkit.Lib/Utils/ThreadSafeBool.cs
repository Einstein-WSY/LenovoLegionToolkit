using System.Threading;

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// 基于 <see cref="Interlocked"/> 实现的线程安全布尔值。
/// </summary>
public class ThreadSafeBool
{
    // 以 0/1 的 int 存储布尔状态，保证读写具备原子性与内存可见性。
    private int _threadSafeBoolBackValue;

    /// <summary>
    /// 获取或设置布尔值，读写均为原子操作。
    /// </summary>
    public bool Value
    {
        get => Interlocked.CompareExchange(ref _threadSafeBoolBackValue, 1, 1) == 1;
        set
        {
            if (value)
                Interlocked.CompareExchange(ref _threadSafeBoolBackValue, 1, 0);
            else
                Interlocked.CompareExchange(ref _threadSafeBoolBackValue, 0, 1);
        }
    }
}

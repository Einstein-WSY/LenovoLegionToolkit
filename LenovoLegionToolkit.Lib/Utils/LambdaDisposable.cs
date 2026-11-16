using System;

namespace LenovoLegionToolkit.Lib.Utils;

/// <summary>
/// 将一段同步清理逻辑包装为 <see cref="IDisposable"/>，便于在 using 语句中托管资源。
/// </summary>
/// <param name="action">释放时执行的清理委托。</param>
public class LambdaDisposable(Action action) : IDisposable
{
    /// <summary>
    /// 释放资源，执行构造时传入的清理委托。
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
        action();
    }
}

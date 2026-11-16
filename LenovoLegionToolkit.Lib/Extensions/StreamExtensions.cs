using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Extensions;

// ReSharper disable LocalizableElement

/// <summary>
/// 提供 <see cref="Stream"/> 的扩展方法。
/// </summary>
public static class StreamExtensions
{
    /// <summary>
    /// 将源流内容拷贝到目标流，并在每个缓冲区写完后报告已拷贝的总字节数。
    /// <para>
    /// 相比框架自带的 <c>CopyToAsync</c>，此方法额外支持 long 粒度的累计进度回调，
    /// 适合大文件下载等需要展示精确进度的场景。
    /// </para>
    /// </summary>
    /// <param name="source">可读的源流。</param>
    /// <param name="destination">可写的目标流。</param>
    /// <param name="bufferSize">每次读写使用的缓冲区大小（字节）。</param>
    /// <param name="progress">可选的进度回调，参数为累计已拷贝的字节数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>拷贝完成的任务。</returns>
    /// <exception cref="ArgumentException">源流不可读或目标流不可写时抛出。</exception>
    public static async Task CopyToAsync(this Stream source, Stream destination, int bufferSize, IProgress<long>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!source.CanRead)
            throw new ArgumentException("Has to be readable", nameof(source));
        if (!destination.CanWrite)
            throw new ArgumentException("Has to be writable", nameof(destination));

        var buffer = new byte[bufferSize];
        long totalBytesRead = 0;
        int bytesRead;

        while ((bytesRead = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
            totalBytesRead += bytesRead;
            progress?.Report(totalBytesRead);
        }
    }
}

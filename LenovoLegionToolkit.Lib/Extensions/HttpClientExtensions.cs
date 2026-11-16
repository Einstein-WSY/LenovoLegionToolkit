using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// 提供 <see cref="HttpClient"/> 的扩展方法，如带进度回调的下载。
/// </summary>
public static class HttpClientExtensions
{
    /// <summary>
    /// 将指定 URI 的内容下载到目标流，并通过 <paramref name="progress"/> 报告进度。
    /// <para>
    /// 使用 <see cref="HttpCompletionOption.ResponseHeadersRead"/> 在收到响应头后即开始读取流，
    /// 避免大文件被整体缓冲到内存。进度为 0~1 的浮点比例；无进度回调或服务端未返回
    /// Content-Length 时退化为普通拷贝。
    /// </para>
    /// </summary>
    /// <param name="client">HTTP 客户端。</param>
    /// <param name="requestUri">下载地址。</param>
    /// <param name="destination">目标流。</param>
    /// <param name="progress">可选的进度回调，参数为 0~1 的完成比例。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>下载完成的任务。</returns>
    public static async Task DownloadAsync(this HttpClient client, string requestUri, Stream destination, IProgress<float>? progress = null, CancellationToken cancellationToken = default)
    {
        using var response = await client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        var contentLength = response.Content.Headers.ContentLength;

        await using var download = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        if (progress is null || !contentLength.HasValue)
        {
            await download.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
            return;
        }

        progress.Report(0);
        // 将"已拷贝的字节总数"换算为相对比例后转发给外层进度回调
        var relativeProgress = new Progress<long>(totalBytes => progress.Report((float)totalBytes / contentLength.Value));
        await download.CopyToAsync(destination, 81920, relativeProgress, cancellationToken).ConfigureAwait(false);
        progress.Report(1);
    }
}

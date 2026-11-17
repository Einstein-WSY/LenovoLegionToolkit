using System;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.CLI.Lib.Extensions;

/// <summary>
/// 命名管道读写扩展：以 JSON 文本形式在管道中传递对象。
/// 依赖管道处于消息传输模式（<see cref="PipeTransmissionMode.Message"/>），即一次写入对应一条完整消息。
/// </summary>
public static class PipeStreamExtensions
{
    private static readonly Encoding Encoding = Encoding.UTF8;

    /// <summary>
    /// 把对象序列化为 JSON 并写入管道。
    /// </summary>
    /// <typeparam name="T">待写入对象的类型。</typeparam>
    /// <param name="stream">目标管道流。</param>
    /// <param name="obj">待写入的对象。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>写入完成的异步任务。</returns>
    /// <exception cref="InvalidOperationException">管道未处于消息传输模式。</exception>
    public static async Task WriteObjectAsync<T>(this PipeStream stream, T obj, CancellationToken token = default)
    {
        // 消息模式保证接收端能以 IsMessageComplete 判断一条消息的结束
        if (stream.ReadMode != PipeTransmissionMode.Message)
            throw new InvalidOperationException("ReadMode is not PipeTransmissionMode.Message");

        var str = JsonConvert.SerializeObject(obj);
        var bytes = Encoding.GetBytes(str);
        await stream.WriteAsync(bytes, token).ConfigureAwait(false);
    }

    /// <summary>
    /// 从管道读取一条完整消息并反序列化为对象。
    /// </summary>
    /// <typeparam name="T">目标对象类型。</typeparam>
    /// <param name="stream">源管道流。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>反序列化得到的对象；消息内容为空时可能返回 <see langword="null"/>。</returns>
    /// <exception cref="InvalidOperationException">管道未处于消息传输模式。</exception>
    public static async Task<T?> ReadObjectAsync<T>(this PipeStream stream, CancellationToken token = default)
    {
        if (stream.ReadMode != PipeTransmissionMode.Message)
            throw new InvalidOperationException("ReadMode is not PipeTransmissionMode.Message");

        var buffer = new byte[1024];
        var builder = new StringBuilder();

        // 一条消息可能跨多次读取，需循环累加直到 IsMessageComplete 为 true。
        // 只解码实际读取的字节数：解码整个缓冲区会把尾部残留的 \0 混入消息。
        do
        {
            var bytesRead = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
            builder.Append(Encoding.GetString(buffer, 0, bytesRead));
        } while (!stream.IsMessageComplete);

        return JsonConvert.DeserializeObject<T>(builder.ToString());
    }
}

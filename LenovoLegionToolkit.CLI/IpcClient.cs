using System;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.CLI.Lib;
using LenovoLegionToolkit.CLI.Lib.Extensions;

namespace LenovoLegionToolkit.CLI;

/// <summary>
/// CLI 的 IPC 客户端，通过命名管道把请求发送给正在运行的 Lenovo Legion Toolkit 主程序。
/// 所有操作都要求主程序已启用 CLI 功能。
/// </summary>
public static class IpcClient
{
    /// <summary>
    /// 获取所有可用的快捷操作名称列表。
    /// </summary>
    /// <returns>主程序返回的快捷操作列表文本。</returns>
    /// <exception cref="IpcException">响应缺失或操作失败。</exception>
    public static async Task<string> ListQuickActionsAsync()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.ListQuickActions
        };

        return await SendRequestAsync(req).ConfigureAwait(false)
               ?? throw new IpcException("Missing return message");
    }

    /// <summary>
    /// 执行指定名称的快捷操作。
    /// </summary>
    /// <param name="name">快捷操作名称。</param>
    /// <returns>请求完成的异步任务。</returns>
    public static Task RunQuickActionAsync(string name)
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.QuickAction,
            Name = name
        };

        return SendRequestAsync(req);
    }

    /// <summary>
    /// 获取所有可用功能的列表。
    /// </summary>
    /// <returns>主程序返回的功能列表文本。</returns>
    /// <exception cref="IpcException">响应缺失或操作失败。</exception>
    public static async Task<string> ListFeaturesAsync()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.ListFeatures,
        };

        return await SendRequestAsync(req).ConfigureAwait(false)
               ?? throw new IpcException("Missing return message");
    }

    /// <summary>
    /// 获取指定功能所有可选的值。
    /// </summary>
    /// <param name="name">功能名称。</param>
    /// <returns>主程序返回的可选值列表文本。</returns>
    /// <exception cref="IpcException">响应缺失或操作失败。</exception>
    public static async Task<string> ListFeatureValuesAsync(string name)
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.ListFeatureValues,
            Name = name,
        };

        return await SendRequestAsync(req).ConfigureAwait(false)
               ?? throw new IpcException("Missing return message");
    }

    /// <summary>
    /// 设置指定功能的值。
    /// </summary>
    /// <param name="name">功能名称。</param>
    /// <param name="value">要设置的值。</param>
    /// <returns>请求完成的异步任务。</returns>
    public static Task SetFeatureValueAsync(string name, string value)
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.SetFeatureValue,
            Name = name,
            Value = value
        };

        return SendRequestAsync(req);
    }

    /// <summary>
    /// 获取指定功能当前的值。
    /// </summary>
    /// <param name="name">功能名称。</param>
    /// <returns>功能当前值的文本表示。</returns>
    /// <exception cref="IpcException">响应缺失或操作失败。</exception>
    public static async Task<string> GetFeatureValueAsync(string name)
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.GetFeatureValue,
            Name = name
        };

        return await SendRequestAsync(req).ConfigureAwait(false)
               ?? throw new IpcException("Missing return message");
    }

    /// <summary>
    /// 获取当前 Spectrum 灯效配置。
    /// </summary>
    /// <returns>配置值的文本表示。</returns>
    /// <exception cref="IpcException">响应缺失或操作失败。</exception>
    public static async Task<string> GetSpectrumProfileAsync()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.GetSpectrumProfile
        };

        return await SendRequestAsync(req).ConfigureAwait(false)
               ?? throw new IpcException("Missing return message");
    }

    /// <summary>
    /// 设置 Spectrum 灯效配置。
    /// </summary>
    /// <param name="value">配置值（以字符串形式传输，由主程序解析）。</param>
    /// <returns>请求完成的异步任务。</returns>
    public static Task SetSpectrumProfileAsync(string value)
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.SetSpectrumProfile,
            Value = value
        };

        return SendRequestAsync(req);
    }

    /// <summary>
    /// 获取当前 Spectrum 亮度。
    /// </summary>
    /// <returns>亮度值的文本表示。</returns>
    /// <exception cref="IpcException">响应缺失或操作失败。</exception>
    public static async Task<string> GetSpectrumBrightnessAsync()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.GetSpectrumBrightness
        };

        return await SendRequestAsync(req).ConfigureAwait(false)
               ?? throw new IpcException("Missing return message");
    }

    /// <summary>
    /// 设置 Spectrum 亮度。
    /// </summary>
    /// <param name="value">亮度值（以字符串形式传输）。</param>
    /// <returns>请求完成的异步任务。</returns>
    public static Task SetSpectrumBrightnessAsync(string value)
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.SetSpectrumBrightness,
            Value = value
        };

        return SendRequestAsync(req);
    }

    /// <summary>
    /// 获取当前 RGB 灯效预设。
    /// </summary>
    /// <returns>预设值的文本表示。</returns>
    /// <exception cref="IpcException">响应缺失或操作失败。</exception>
    public static async Task<string> GetRGBPresetAsync()
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.GetRGBPreset
        };

        return await SendRequestAsync(req).ConfigureAwait(false)
               ?? throw new IpcException("Missing return message");
    }

    /// <summary>
    /// 设置 RGB 灯效预设。
    /// </summary>
    /// <param name="value">预设值（以字符串形式传输）。</param>
    /// <returns>请求完成的异步任务。</returns>
    public static Task SetRGBPresetAsync(string value)
    {
        var req = new IpcRequest
        {
            Operation = IpcRequest.OperationType.SetRGBPreset,
            Value = value
        };

        return SendRequestAsync(req);
    }

    /// <summary>
    /// 建立管道连接、发送一条请求并读取响应。
    /// </summary>
    /// <param name="req">待发送的请求。</param>
    /// <returns>响应中的消息内容；查询类操作返回所需的文本。</returns>
    /// <exception cref="IpcException">服务端返回失败或响应缺失。</exception>
    /// <exception cref="IpcConnectException">无法连接到主程序。</exception>
    private static async Task<string?> SendRequestAsync(IpcRequest req)
    {
        await using var pipe = new NamedPipeClientStream(Constants.PIPE_NAME);

        await ConnectAsync(pipe).ConfigureAwait(false);

        await pipe.WriteObjectAsync(req).ConfigureAwait(false);
        var res = await pipe.ReadObjectAsync<IpcResponse>().ConfigureAwait(false);

        if (res is null || !res.Success)
            throw new IpcException(res?.Message ?? "Unknown failure");

        return res.Message;
    }

    /// <summary>
    /// 连接命名管道，最多尝试 4 次（初始 1 次 + 3 次重试），每次超时 500 毫秒。
    /// </summary>
    /// <param name="pipe">待连接的管道客户端。</param>
    /// <returns>连接成功的异步任务。</returns>
    /// <exception cref="IpcConnectException">所有尝试均超时。</exception>
    private static async Task ConnectAsync(NamedPipeClientStream pipe)
    {
        var retries = 3;

        while (retries >= 0)
        {
            try
            {
                await pipe.ConnectAsync(TimeSpan.FromMilliseconds(500), CancellationToken.None).ConfigureAwait(false);
                // 必须使用消息模式，读写扩展依赖 IsMessageComplete 判断消息边界
                pipe.ReadMode = PipeTransmissionMode.Message;
                return;
            }
            catch (TimeoutException) { }

            retries--;
        }

        throw new IpcConnectException();
    }
}

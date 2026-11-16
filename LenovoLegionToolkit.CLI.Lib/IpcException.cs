using System;

namespace LenovoLegionToolkit.CLI.Lib;

/// <summary>
/// 与主程序通信失败或请求执行失败时抛出的异常。
/// </summary>
/// <param name="name">异常信息，通常为服务端返回的错误描述。</param>
public class IpcException(string? name) : Exception(name);

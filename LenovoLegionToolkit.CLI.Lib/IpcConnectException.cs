using System;

namespace LenovoLegionToolkit.CLI.Lib;

/// <summary>
/// 无法连接到主程序命名管道时抛出的异常，通常表示主程序未运行或未启用 CLI 功能。
/// </summary>
public class IpcConnectException : Exception;

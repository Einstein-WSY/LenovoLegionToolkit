namespace LenovoLegionToolkit.CLI.Lib;

/// <summary>
/// CLI 与主程序通信时使用的常量。
/// </summary>
public static class Constants
{
    /// <summary>命名管道的名称，主程序在启用 CLI 功能后监听同名管道。</summary>
    public const string PIPE_NAME = "LenovoLegionToolkit-IPC-0";
}

namespace LenovoLegionToolkit.CLI.Lib;

/// <summary>
/// 主程序返回给 CLI 的响应。
/// </summary>
public class IpcResponse
{
    /// <summary>请求是否执行成功。</summary>
    public bool Success { get; init; }

    /// <summary>执行结果或错误信息；查询类操作的返回值也放在此字段中。</summary>
    public string? Message { get; init; }
}

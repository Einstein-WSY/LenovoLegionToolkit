namespace LenovoLegionToolkit.Lib.Automation;

/// <summary>
/// 流水线单次执行期间的上下文对象，用于在同一条流水线的各个步骤之间传递运行期数据。
/// </summary>
public class AutomationContext
{
    /// <summary>
    /// 上一步骤产生的输出文本，供后续步骤引用（例如命令输出、错误信息）。
    /// </summary>
    public string? LastRunOutput { get; set; }
}

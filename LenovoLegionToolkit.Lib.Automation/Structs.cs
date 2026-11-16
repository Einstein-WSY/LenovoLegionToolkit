using LenovoLegionToolkit.Lib.Automation.Resources;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation;

// JsonConstructor 特性标注在主构造函数上，使 Newtonsoft.Json 在反序列化时能够据此重建该只读结构体。
/// <summary>
/// 表示“延时若干秒”的配置值，用于延时自动化步骤，并负责生成界面显示文本。
/// </summary>
/// <param name="delaySeconds">延时的秒数。</param>
[method: JsonConstructor]
public readonly struct Delay(int delaySeconds) : IDisplayName
{
    /// <summary>延时的秒数。</summary>
    public int DelaySeconds { get; } = delaySeconds;

    /// <summary>
    /// 本地化的显示文本；英文等语言存在单复数差异，因此按秒数是否为 1 选择不同的资源字符串。
    /// </summary>
    public string DisplayName => string.Format(DelaySeconds == 1 ? Resource.Delay_Second : Resource.Delay_Second_Many, DelaySeconds);
}

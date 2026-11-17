using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Resources;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：关注的进程开始运行后执行。
/// 触发时机为进程启动事件，且该进程命中配置列表、同时当前确有列表内的进程在运行。
/// </summary>
/// <param name="processes">关注的进程集合；为 null 时视为空列表（永不匹配）。</param>
[method: JsonConstructor]
public class ProcessesAreRunningAutomationPipelineTrigger(ProcessInfo[]? processes) : IProcessesAutomationPipelineTrigger
{
    /// <summary>界面显示名称：当进程运行时。</summary>
    [JsonIgnore]
    public string DisplayName => Resource.ProcessesAreRunningAutomationPipelineTrigger_DisplayName;

    /// <summary>关注的进程集合。</summary>
    public ProcessInfo[] Processes { get; } = processes ?? [];

    /// <summary>
    /// 判断事件是否触发：只处理“进程启动”事件，且进程需命中关注列表，
    /// 随后再确认列表中存在正在运行的进程实例。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>关注的进程正在运行时返回 true。</returns>
    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        if (automationEvent is not ProcessAutomationEvent { Type: ProcessEventInfoType.Started } e)
            return Task.FromResult(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Checking for {e.ProcessInfo.Name}... [processes={string.Join(",", Processes.Select(p => p.Name))}]");

        // 进程可能因重启导致实例变化，因此除对象相等外还按名称匹配一次。
        if (!Processes.Contains(e.ProcessInfo) && !Processes.Select(p => p.Name).Contains(e.ProcessInfo.Name))
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Process name {e.ProcessInfo.Name} not in the list.");

            return Task.FromResult(false);
        }

        // 不仅要看事件里的进程，还要确认关注列表中确实存在运行实例，避免进程刚启动就退出的误报。
        var result = Processes.SelectMany(p => Process.GetProcessesByName(p.Name)).Any();

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Process name {e.ProcessInfo.Name} found in process list: {result}.");

        return Task.FromResult(result);
    }

    /// <summary>
    /// 查询当前状态：关注列表中任一进程存在运行实例即满足。
    /// </summary>
    /// <returns>存在运行中的关注进程时返回 true。</returns>
    public Task<bool> IsMatchingState()
    {
        var result = Processes.SelectMany(p => Process.GetProcessesByName(p.Name)).Any();
        return Task.FromResult(result);
    }

    /// <summary>向执行环境写入进程已运行及进程列表。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment)
    {
        environment.ProcessesStarted = true;
        environment.Processes = Processes;
    }

    /// <summary>创建深拷贝，保留进程列表。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new ProcessesAreRunningAutomationPipelineTrigger(Processes);

    /// <summary>以新的进程集合创建副本（供界面编辑后重建触发器）。</summary>
    /// <param name="processes">新的进程集合。</param>
    /// <returns>新的触发器实例。</returns>
    public IProcessesAutomationPipelineTrigger DeepCopy(ProcessInfo[] processes) => new ProcessesAreRunningAutomationPipelineTrigger(processes);

    /// <summary>按进程集合序列比较是否等价。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>类型相同且进程集合顺序一致时返回 true。</returns>
    public override bool Equals(object? obj)
    {
        return obj is ProcessesAreRunningAutomationPipelineTrigger t && Processes.SequenceEqual(t.Processes);
    }

    /// <summary>逐个累加进程信息生成哈希值。</summary>
    /// <returns>哈希值。</returns>
    public override int GetHashCode()
    {
        var hc = new HashCode();
        Processes.ForEach(p => hc.Add(p));
        return hc.ToHashCode();
    }

    /// <summary>输出便于日志排查的文本表示。</summary>
    /// <returns>包含进程集合的字符串。</returns>
    public override string ToString() => $"{nameof(Processes)}: {string.Join(", ", Processes)}";
}

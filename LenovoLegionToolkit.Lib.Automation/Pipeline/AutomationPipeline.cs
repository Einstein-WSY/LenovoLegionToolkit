using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;
using LenovoLegionToolkit.Lib.Automation.Steps;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline;

/// <summary>
/// 自动化流水线：由一个可选的触发器与一组按顺序执行的步骤组成。
/// 触发器为空表示只能手动运行；触发后按步骤顺序依次执行，步骤失败会被收集而不是立即中断。
/// </summary>
public class AutomationPipeline
{
    /// <summary>
    /// 流水线唯一标识。除区分实例外，也被“快捷操作”步骤用于引用目标流水线，因此需保持稳定。
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>界面显示的图标名称。</summary>
    public string? IconName { get; set; }

    /// <summary>流水线名称，仅用于展示与日志。</summary>
    public string? Name { get; set; }

    /// <summary>触发条件；为 null 表示该流水线只能手动执行。</summary>
    public IAutomationPipelineTrigger? Trigger { get; set; }

    /// <summary>按顺序执行的步骤集合。</summary>
    public List<IAutomationStep> Steps { get; init; } = [];

    /// <summary>
    /// 是否为独占流水线。独占流水线执行完成后，本轮事件不再继续执行后续流水线。
    /// </summary>
    public bool IsExclusive { get; init; } = true;

    /// <summary>
    /// 展开后的全部叶子触发器：复合触发器会被拆开为其中的各个子触发器，
    /// 便于统一做事件匹配与监听器需求统计。
    /// </summary>
    [JsonIgnore]
    public IEnumerable<IAutomationPipelineTrigger> AllTriggers
    {
        get
        {
            if (Trigger is null)
                yield break;

            foreach (var trigger in EnumerateTriggers(Trigger, [Trigger]))
                yield return trigger;
        }
    }

    /// <summary>
    /// 递归展开触发器：普通触发器返回自身；复合触发器则递归展开其全部子触发器，
    /// 并用引用相等集合防止循环引用导致无限递归。
    /// </summary>
    /// <param name="trigger">待展开的触发器。</param>
    /// <param name="visited">已访问触发器集合（按引用比较）。</param>
    /// <returns>展开后的叶子触发器序列。</returns>
    private static IEnumerable<IAutomationPipelineTrigger> EnumerateTriggers(IAutomationPipelineTrigger trigger, HashSet<object> visited)
    {
        if (trigger is not ICompositeAutomationPipelineTrigger compositeTrigger)
        {
            yield return trigger;
            yield break;
        }

        foreach (var innerTrigger in compositeTrigger.Triggers)
        {
            if (!visited.Add(innerTrigger))
                continue;

            foreach (var leafTrigger in EnumerateTriggers(innerTrigger, visited))
                yield return leafTrigger;
        }
    }

    /// <summary>创建空流水线，供序列化使用。</summary>
    public AutomationPipeline() { }

    /// <summary>创建无触发器（仅手动执行）的命名流水线。</summary>
    /// <param name="name">流水线名称。</param>
    public AutomationPipeline(string name) => Name = name;

    /// <summary>创建由指定触发器自动触发的流水线。</summary>
    /// <param name="trigger">触发条件。</param>
    public AutomationPipeline(IAutomationPipelineTrigger trigger) => Trigger = trigger;

    /// <summary>
    /// 执行流水线中的全部步骤。
    /// </summary>
    /// <param name="otherPipelines">其余流水线，用于展开“快捷操作”步骤所引用的流水线。</param>
    /// <param name="token">取消令牌，取消后不再开始新的步骤。</param>
    /// <returns>表示异步执行的任务。</returns>
    /// <exception cref="AggregateException">存在失败的步骤时，在全部步骤执行结束后统一抛出。</exception>
    internal async Task RunAsync(List<AutomationPipeline> otherPipelines, CancellationToken token = default)
    {
        if (token.IsCancellationRequested)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Pipeline interrupted.");
            return;
        }

        // 每次执行都新建上下文与环境对象，避免多次运行或不同流水线之间串用状态。
        var context = new AutomationContext();
        var environment = new AutomationEnvironment();
        var stepExceptions = new List<Exception>();

        // 先把触发器所代表的状态写入环境变量，后续步骤（如脚本）可直接读取。
        AllTriggers.ForEach(t => t.UpdateEnvironment(environment));

        foreach (var step in GetAllSteps(otherPipelines))
        {
            if (token.IsCancellationRequested)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Pipeline interrupted.");
                break;
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Running step... [type={step.GetType().Name}]");

            try
            {
                await step.RunAsync(context, environment, token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 取消属于正常中断流程，不属于步骤失败，直接向上抛出。
                if (ex is OperationCanceledException && token.IsCancellationRequested)
                    throw;

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Step run failed. [name={step.GetType().Name}]", ex);

                // 单个步骤失败不中断整条流水线，先收集异常，全部步骤结束后再统一抛出。
                stepExceptions.Add(ex);
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Step completed successfully. [type={step.GetType().Name}]");
        }

        if (stepExceptions.Count != 0)
            throw new AggregateException(stepExceptions);
    }

    /// <summary>
    /// 展开执行序列：遇到“快捷操作”步骤时，将其引用的无触发器流水线的步骤就地插入。
    /// </summary>
    /// <param name="pipelines">可被引用的流水线集合。</param>
    /// <param name="visited">已访问流水线 Id 集合；首次调用为 null 时以当前流水线初始化，用于防止引用环导致无限递归。</param>
    /// <returns>按执行顺序展开后的步骤序列。</returns>
    private IEnumerable<IAutomationStep> GetAllSteps(List<AutomationPipeline> pipelines, HashSet<Guid>? visited = null)
    {
        visited ??= [Id];

        foreach (var step in Steps)
        {
            if (step is QuickActionAutomationStep qas)
            {
                // 只允许引用自身以外、且不带触发器的流水线，防止把自动规则误当作子流程执行。
                var matchingPipeline = pipelines.FirstOrDefault(p => p.Id != Id && p.Id == qas.PipelineId && p.AllTriggers.IsEmpty());
                if (matchingPipeline is null)
                    continue;

                // 已在本次展开链中访问过的流水线不再递归，避免引用环造成 StackOverflow。
                if (visited.Add(matchingPipeline.Id))
                {
                    foreach (var matchingPipelineStep in matchingPipeline.GetAllSteps(pipelines, visited))
                        yield return matchingPipelineStep;
                }
            }

            yield return step;
        }
    }

    /// <summary>
    /// 创建流水线的深拷贝，触发器与每个步骤都会被复制，避免共享可变状态。
    /// </summary>
    /// <returns>新的流水线实例，Id 与原实例保持一致。</returns>
    public AutomationPipeline DeepCopy() => new()
    {
        Id = Id,
        IconName = IconName,
        Name = Name,
        Trigger = Trigger?.DeepCopy(),
        Steps = Steps.Select(s => s.DeepCopy()).ToList(),
        IsExclusive = IsExclusive,
    };
}

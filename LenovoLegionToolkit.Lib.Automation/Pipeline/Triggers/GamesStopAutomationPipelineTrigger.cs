using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.AutoListeners;
using LenovoLegionToolkit.Lib.Automation.Resources;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：所有游戏停止运行后执行。
/// 触发时机为游戏自动监听器报告“游戏运行中”状态由有到无。
/// </summary>
public class GamesStopAutomationPipelineTrigger : IGameAutomationPipelineTrigger
{
    /// <summary>界面显示名称：当游戏停止时。</summary>
    [JsonIgnore]
    public string DisplayName => Resource.GamesStopAutomationPipelineTrigger_DisplayName;

    /// <summary>
    /// 判断事件是否触发：事件必须是“游戏已停止”的状态变化事件。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>游戏全部停止时返回 true。</returns>
    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        var result = automationEvent is GameAutomationEvent { Running: false };
        return Task.FromResult(result);
    }

    /// <summary>
    /// 查询当前状态：游戏监听器当前未检测到任何运行中的游戏。
    /// </summary>
    /// <returns>当前没有游戏运行时返回 true。</returns>
    public Task<bool> IsMatchingState()
    {
        var listener = IoCContainer.Resolve<GameAutoListener>();
        var result = !listener.AreGamesRunning();
        return Task.FromResult(result);
    }

    /// <summary>向执行环境写入“无游戏运行”。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment) => environment.GameRunning = false;

    /// <summary>创建深拷贝。该触发器无可变配置。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new GamesStopAutomationPipelineTrigger();
}

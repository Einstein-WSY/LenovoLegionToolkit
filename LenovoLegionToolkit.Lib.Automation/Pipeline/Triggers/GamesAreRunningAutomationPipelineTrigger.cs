using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.AutoListeners;
using LenovoLegionToolkit.Lib.Automation.Resources;
using Newtonsoft.Json;

namespace LenovoLegionToolkit.Lib.Automation.Pipeline.Triggers;

/// <summary>
/// 触发器：有游戏开始运行时执行。
/// 触发时机为游戏自动监听器报告“游戏运行中”状态由无到有。
/// </summary>
public class GamesAreRunningAutomationPipelineTrigger : IGameAutomationPipelineTrigger
{
    /// <summary>界面显示名称：当游戏运行时。</summary>
    [JsonIgnore]
    public string DisplayName => Resource.GamesAreRunningAutomationPipelineTrigger_DisplayName;

    /// <summary>
    /// 判断事件是否触发：事件必须是“游戏运行中”的状态变化事件。
    /// </summary>
    /// <param name="automationEvent">待判断的事件。</param>
    /// <returns>有游戏开始运行时返回 true。</returns>
    public Task<bool> IsMatchingEvent(IAutomationEvent automationEvent)
    {
        var result = automationEvent is GameAutomationEvent { Running: true };
        return Task.FromResult(result);
    }

    /// <summary>
    /// 查询当前状态：直接询问游戏监听器是否检测到正在运行的游戏。
    /// </summary>
    /// <returns>当前有游戏运行时返回 true。</returns>
    public Task<bool> IsMatchingState()
    {
        var listener = IoCContainer.Resolve<GameAutoListener>();
        var result = listener.AreGamesRunning();
        return Task.FromResult(result);
    }

    /// <summary>向执行环境写入“游戏正在运行”。</summary>
    /// <param name="environment">流水线执行环境。</param>
    public void UpdateEnvironment(AutomationEnvironment environment) => environment.GameRunning = true;

    /// <summary>创建深拷贝。该触发器无可变配置。</summary>
    /// <returns>新的触发器实例。</returns>
    public IAutomationPipelineTrigger DeepCopy() => new GamesAreRunningAutomationPipelineTrigger();
}

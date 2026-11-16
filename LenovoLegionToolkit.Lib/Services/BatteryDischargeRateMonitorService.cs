using System;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.Services;

/// <summary>
/// 电池放电速率监控服务：周期性采集并更新电池最小/最大放电速率等统计值，
/// 为界面展示放电功率曲线提供数据。通过启停接口按需运行，避免不必要的轮询。
/// </summary>
public class BatteryDischargeRateMonitorService
{
    private CancellationTokenSource? _cts;
    private Task? _refreshTask;

    /// <summary>
    /// 重启监控服务：先停止已有实例，若已有循环则直接返回，否则新建取消令牌并启动后台监控循环。
    /// </summary>
    public async Task StartStopIfNeededAsync()
    {
        await StopAsync().ConfigureAwait(false);

        if (_refreshTask != null)
            return;

        if (_cts is not null)
            await _cts.CancelAsync().ConfigureAwait(false);

        _cts = new CancellationTokenSource();

        var token = _cts.Token;

        _refreshTask = Task.Run(async () =>
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Battery monitoring service started...");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    Battery.SetMinMaxDischargeRate();

                    // 3 秒轮询间隔：放电速率变化平缓，过低频率足够且开销很小
                    await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
                }
                // 取消导致的异常属于正常退出路径，忽略即可
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    // 单次采集失败不终止循环，等待下一轮重试
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Battery monitoring service failed.", ex);
                }
            }

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Battery monitoring service stopped.");
        }, token);
    }

    /// <summary>
    /// 停止监控：取消令牌并等待后台循环真正退出，确保资源释放且可安全重启。
    /// </summary>
    public async Task StopAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopping...");

        if (_cts is not null)
            await _cts.CancelAsync().ConfigureAwait(false);

        _cts = null;

        if (_refreshTask is not null)
            await _refreshTask.ConfigureAwait(false);

        _refreshTask = null;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopped.");
    }
}

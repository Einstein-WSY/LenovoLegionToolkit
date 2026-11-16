using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.CLI.Lib;
using LenovoLegionToolkit.CLI.Lib.Extensions;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Automation;
using LenovoLegionToolkit.Lib.Controllers;
using LenovoLegionToolkit.Lib.Messaging;
using LenovoLegionToolkit.Lib.Messaging.Messages;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.Utils;
using LenovoLegionToolkit.WPF.CLI.Features;

namespace LenovoLegionToolkit.WPF.CLI;

/// <summary>
/// 基于命名管道的 IPC 服务端，接收 CLI 客户端请求并执行对应的快捷操作或功能读写。
/// </summary>
public class IpcServer(
    AutomationProcessor automationProcessor,
    SpectrumKeyboardBacklightController spectrumKeyboardBacklightController,
    RGBKeyboardBacklightController rgbKeyboardBacklightController,
    IntegrationsSettings settings
    )
{
    private CancellationTokenSource _cancellationTokenSource = new();
    private Task _handler = Task.CompletedTask;

    /// <summary>
    /// 按当前设置启动或停止 IPC 服务：先停止旧实例，若设置中启用了 CLI 则重新启动监听。
    /// </summary>
    public async Task StartStopIfNeededAsync()
    {
        await StopAsync().ConfigureAwait(false);

        if (!settings.Store.CLI)
            return;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Starting...");

        _cancellationTokenSource = new();

        var token = _cancellationTokenSource.Token;
        _handler = Task.Run(() => Handler(token), token);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Started");
    }

    /// <summary>
    /// 停止 IPC 服务：取消令牌并等待处理循环结束。
    /// </summary>
    public async Task StopAsync()
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopping...");

        // 取消监听并等待处理任务退出，确保端口被释放
        await _cancellationTokenSource.CancelAsync();
        await _handler;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Stopped");
    }

    private async Task Handler(CancellationToken token)
    {
        try
        {
            // 允许已认证用户读写管道，避免未授权进程调用
            var identity = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
            var security = new PipeSecurity();
            security.AddAccessRule(new(identity, PipeAccessRights.ReadWrite, AccessControlType.Allow));

            await using var pipe = NamedPipeServerStreamAcl.Create(LenovoLegionToolkit.CLI.Lib.Constants.PIPE_NAME,
                PipeDirection.InOut,
                1,
                PipeTransmissionMode.Message,
                PipeOptions.None,
                0,
                0,
                security);

            while (!token.IsCancellationRequested)
            {
                await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Connection received.");

                try
                {
                    // 读取请求对象并分发处理，成功或失败都写回 IpcResponse
                    var req = await pipe.ReadObjectAsync<IpcRequest>(token).ConfigureAwait(false);

                    if (req?.Operation is null)
                        throw new IpcException("Failed to deserialize request");

                    var res = await HandleRequest(req).ConfigureAwait(false);
                    await pipe.WriteObjectAsync(res, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // 业务异常转换为失败响应返回客户端，而非中断服务
                    var res = new IpcResponse { Success = false, Message = ex.Message };
                    await pipe.WriteObjectAsync(res, token).ConfigureAwait(false);
                }
                finally
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Disconnecting...");

                    pipe.Disconnect();
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Unknown failure.", ex);
        }
    }

    private async Task<IpcResponse> HandleRequest(IpcRequest req)
    {
        string? message;

        // 根据操作类型分发到对应处理方法
        switch (req.Operation)
        {
            case IpcRequest.OperationType.ListQuickActions:
                message = await ListQuickActionsAsync().ConfigureAwait(false);
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.QuickAction when req is { Name: not null }:
                await RunQuickActionAsync(req.Name).ConfigureAwait(false);
                return new IpcResponse { Success = true };
            case IpcRequest.OperationType.ListFeatures:
                message = await ListFeaturesAsync();
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.ListFeatureValues when req is { Name: not null }:
                message = await ListFeatureValuesAsync(req.Name);
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.GetFeatureValue when req is { Name: not null }:
                message = await GetFeatureValueAsync(req.Name);
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.SetFeatureValue when req is { Name: not null, Value: not null }:
                await SetFeatureValueAsync(req.Name, req.Value).ConfigureAwait(false);
                return new IpcResponse { Success = true };
            case IpcRequest.OperationType.GetSpectrumProfile:
                message = await GetSpectrumProfileAsync();
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.SetSpectrumProfile when req is { Value: not null }:
                await SetSpectrumProfileAsync(req.Value);
                return new IpcResponse { Success = true };
            case IpcRequest.OperationType.GetSpectrumBrightness:
                message = await GetSpectrumBrightnessAsync();
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.SetSpectrumBrightness when req is { Value: not null }:
                await SetSpectrumBrightnessAsync(req.Value);
                return new IpcResponse { Success = true };
            case IpcRequest.OperationType.GetRGBPreset:
                message = await GetRGBPresetAsync();
                return new IpcResponse { Success = true, Message = message };
            case IpcRequest.OperationType.SetRGBPreset when req is { Value: not null }:
                await SetRGBPresetAsync(req.Value);
                return new IpcResponse { Success = true };
            default:
                throw new IpcException("Invalid request");
        }
    }

    // 列出所有无触发器的自动化流程（即“快捷操作”）名称
    private async Task<string> ListQuickActionsAsync()
    {
        var pipelines = await automationProcessor.GetPipelinesAsync().ConfigureAwait(false);
        var quickActions = pipelines
            .Where(p => p.Trigger is null)
            .Select(p => p.Name);

        return string.Join('\n', quickActions);
    }

    // 按名称立即执行指定的快捷操作
    private async Task RunQuickActionAsync(string name)
    {
        var pipelines = await automationProcessor.GetPipelinesAsync().ConfigureAwait(false);
        var quickAction = pipelines
                              .Where(p => p.Trigger is null)
                              .FirstOrDefault(p => p.Name == name)
                          ?? throw new InvalidOperationException($"Quick Action \"{name}\" not found");

        await automationProcessor.RunNowAsync(quickAction.Id);
    }

    // 列出当前设备支持的功能名称
    private static async Task<string?> ListFeaturesAsync()
    {
        var features = new List<string>();

        foreach (var feature in FeatureRegistry.All)
        {
            if (await feature.IsSupportedAsync().ConfigureAwait(false))
                features.Add(feature.Name);
        }

        return string.Join('\n', features);
    }

    // 列出指定功能的所有可选值
    private static async Task<string?> ListFeatureValuesAsync(string name)
    {
        var feature = FeatureRegistry.All.FirstOrDefault(f => f.Name == name)
                      ?? throw new IpcException("Invalid feature");
        var values = await feature.GetValuesAsync().ConfigureAwait(false);
        return string.Join('\n', values);
    }

    // 读取指定功能的当前值
    private static async Task<string> GetFeatureValueAsync(string name)
    {
        var feature = FeatureRegistry.All.FirstOrDefault(f => f.Name == name)
                      ?? throw new IpcException("Invalid feature");
        return await feature.GetValueAsync().ConfigureAwait(false);
    }

    // 设置指定功能的值
    private static async Task SetFeatureValueAsync(string name, string value)
    {
        var feature = FeatureRegistry.All.FirstOrDefault(f => f.Name == name)
                      ?? throw new IpcException("Invalid feature");
        await feature.SetValueAsync(value).ConfigureAwait(false);
    }

    // 读取 Spectrum 键盘灯效配置文件
    private async Task<string> GetSpectrumProfileAsync()
    {
        if (!await spectrumKeyboardBacklightController.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("Spectrum is not supported");

        var profile = await spectrumKeyboardBacklightController.GetProfileAsync().ConfigureAwait(false);
        return $"{profile}";
    }

    // 设置 Spectrum 灯效配置，并广播变更消息以刷新界面
    private async Task SetSpectrumProfileAsync(string value)
    {
        if (!await spectrumKeyboardBacklightController.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("Spectrum is not supported");

        await spectrumKeyboardBacklightController.SetProfileAsync(Convert.ToInt32(value)).ConfigureAwait(false);

        MessagingCenter.Publish(new SpectrumBacklightChangedMessage());
    }

    // 读取 Spectrum 键盘亮度
    private async Task<string> GetSpectrumBrightnessAsync()
    {
        if (!await spectrumKeyboardBacklightController.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("Spectrum is not supported");

        var profile = await spectrumKeyboardBacklightController.GetBrightnessAsync().ConfigureAwait(false);
        return $"{profile}";
    }

    // 设置 Spectrum 键盘亮度，并广播变更消息
    private async Task SetSpectrumBrightnessAsync(string value)
    {
        if (!await spectrumKeyboardBacklightController.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("Spectrum is not supported");

        await spectrumKeyboardBacklightController.SetBrightnessAsync(Convert.ToInt32(value)).ConfigureAwait(false);

        MessagingCenter.Publish(new SpectrumBacklightChangedMessage());
    }

    // 读取 RGB 键盘当前预设编号（对外从 1 开始）
    private async Task<string> GetRGBPresetAsync()
    {
        if (!await rgbKeyboardBacklightController.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("RGB is not supported");

        var state = await rgbKeyboardBacklightController.GetStateAsync().ConfigureAwait(false);
        return $"{(int)state.SelectedPreset + 1}";
    }

    // 设置 RGB 键盘预设（入参从 1 开始，内部转换为枚举），并广播变更消息
    private async Task SetRGBPresetAsync(string value)
    {
        if (!await rgbKeyboardBacklightController.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("RGB is not supported");

        var preset = (RGBKeyboardBacklightPreset)(Convert.ToInt32(value) - 1);

        if (!Enum.IsDefined(preset))
            throw new InvalidOperationException("Invalid preset");

        await rgbKeyboardBacklightController.SetLightControlOwnerAsync(true).ConfigureAwait(false);
        await rgbKeyboardBacklightController.SetPresetAsync(preset).ConfigureAwait(false);

        MessagingCenter.Publish(new RGBKeyboardBacklightChangedMessage());
    }
}

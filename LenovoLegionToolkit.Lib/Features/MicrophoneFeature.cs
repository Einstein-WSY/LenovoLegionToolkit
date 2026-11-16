using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using NAudio.CoreAudioApi;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// 麦克风静音特性。
/// 并非通过硬件接口，而是借助 NAudio (Windows Core Audio / WASAPI) 统一设置所有活动采集设备的静音状态。
/// </summary>
public class MicrophoneFeature : IFeature<MicrophoneState>
{
    private readonly MMDeviceEnumerator _enumerator = new();

    // 枚举所有处于活动状态的录音（Capture）端点，并取其音量控制接口
    private IEnumerable<AudioEndpointVolume> AudioEndpointVolumes => _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).Select(d => d.AudioEndpointVolume);

    /// <inheritdoc/>
    public Task<bool> IsSupportedAsync()
    {
        try
        {
            // 存在任意活动录音设备即认为支持
            var isSupported = AudioEndpointVolumes.Any();
            return Task.FromResult(isSupported);
        }
        catch
        {
            // 音频服务异常或没有录音设备时视为不支持
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public Task<MicrophoneState[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<MicrophoneState>());

    /// <inheritdoc/>
    public Task<MicrophoneState> GetStateAsync()
    {
        // 只有当所有录音设备都被静音时才认为麦克风处于关闭状态
        var mute = AudioEndpointVolumes.Aggregate(true, (current, v) => current && v.Mute);
        var result = mute ? MicrophoneState.Off : MicrophoneState.On;
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task SetStateAsync(MicrophoneState state)
    {
        // 对所有录音设备统一应用静音/取消静音
        var mute = MicrophoneState.Off == state;
        AudioEndpointVolumes.ForEach(v => v.Mute = mute);
        return Task.CompletedTask;
    }
}

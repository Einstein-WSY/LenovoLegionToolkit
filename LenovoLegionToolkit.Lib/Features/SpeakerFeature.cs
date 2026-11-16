using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using NAudio.CoreAudioApi;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// 扬声器静音特性。
/// 通过 NAudio (Windows Core Audio / WASAPI) 统一设置所有活动播放（Render）设备的静音状态。
/// </summary>
public class SpeakerFeature : IFeature<SpeakerState>
{
    private readonly MMDeviceEnumerator _enumerator = new();

    // 枚举所有处于活动状态的播放（Render）端点及其音量控制接口
    private IEnumerable<AudioEndpointVolume> AudioEndpointVolumes => _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).Select(d => d.AudioEndpointVolume);

    /// <inheritdoc/>
    public Task<bool> IsSupportedAsync()
    {
        try
        {
            // 存在任意活动播放设备即认为支持
            var isSupported = AudioEndpointVolumes.Any();
            return Task.FromResult(isSupported);
        }
        catch
        {
            // 音频服务异常或无播放设备时视为不支持
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public Task<SpeakerState[]> GetAllStatesAsync() => Task.FromResult(Enum.GetValues<SpeakerState>());

    /// <inheritdoc/>
    public Task<SpeakerState> GetStateAsync()
    {
        // 仅当所有播放设备均静音时，才认为扬声器处于静音状态
        var mute = AudioEndpointVolumes.Aggregate(true, (current, v) => current && v.Mute);
        var result = mute ? SpeakerState.Mute : SpeakerState.Unmute;
        return Task.FromResult(result);
    }

    /// <inheritdoc/>
    public Task SetStateAsync(SpeakerState state)
    {
        // 对所有播放设备统一应用静音/取消静音
        var mute = SpeakerState.Mute == state;
        AudioEndpointVolumes.ForEach(v => v.Mute = mute);
        return Task.CompletedTask;
    }
}

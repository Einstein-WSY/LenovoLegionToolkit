// #define MOCK_RGB

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Settings;
using LenovoLegionToolkit.Lib.SoftwareDisabler;
using LenovoLegionToolkit.Lib.System;
using LenovoLegionToolkit.Lib.System.Management;
using LenovoLegionToolkit.Lib.Utils;
using Microsoft.Win32.SafeHandles;
using NeoSmart.AsyncLock;
using Windows.Win32;

namespace LenovoLegionToolkit.Lib.Controllers
{
    /// <summary>
    /// 四分区 RGB 键盘控制器：通过 HID Feature Report 下发灯效状态。
    /// 预设存储在设置中，所有设备 I/O 由同一把异步锁串行化，避免报文交错。
    /// </summary>
    public class RGBKeyboardBacklightController(RGBKeyboardSettings settings, VantageDisabler vantageDisabler)
    {
        private static readonly AsyncLock IoLock = new(); // 串行化所有 HID 写入

        private SafeFileHandle? _deviceHandle; // 延迟打开的 HID 设备句柄

        // 设备句柄惰性获取；ForceDisable 用于临时禁用灯光控制（如交由其他程序接管）
        private SafeFileHandle? DeviceHandle
        {
            get
            {
                if (ForceDisable)
                    return null;

                _deviceHandle ??= Devices.GetRGBKeyboard();
                return _deviceHandle;
            }
        }

        /// <summary>
        /// 强制禁用本控制器（返回 null 句柄，所有操作视为不支持）。
        /// </summary>
        public bool ForceDisable { get; set; }

        /// <summary>
        /// 判断本机是否存在可用的 RGB 键盘设备。
        /// </summary>
        public Task<bool> IsSupportedAsync()
        {
#if MOCK_RGB
            return Task.FromResult(true);
#else
            return Task.FromResult(DeviceHandle is not null);
#endif
        }

        /// <summary>
        /// 夺取/交还灯光控制权（通过 WMI 的 LightControlOwner）。
        /// </summary>
        /// <param name="enable">true 表示由本程序控制灯光。</param>
        /// <param name="restorePreset">夺取控制权后是否立即恢复当前预设。</param>
        public async Task SetLightControlOwnerAsync(bool enable, bool restorePreset = false)
        {
            using (await IoLock.LockAsync().ConfigureAwait(false))
            {
                try
                {
#if !MOCK_RGB
                    _ = DeviceHandle ?? throw new InvalidOperationException("RGB Keyboard unsupported");
#endif

                    await ThrowIfVantageEnabled().ConfigureAwait(false);

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Taking ownership...");

#if !MOCK_RGB
                    await WMI.LenovoGameZoneData.SetLightControlOwnerAsync(enable ? 1 : 0).ConfigureAwait(false);
#endif

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Ownership set to {enable}, restoring profile...");

                    if (restorePreset)
                    {
                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Restoring preset...");

                        await SetCurrentPresetAsync().ConfigureAwait(false);

                        if (Log.Instance.IsTraceEnabled)
                            Log.Instance.Trace($"Restored preset");
                    }
                }
                catch (Exception ex)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Can't take ownership.", ex);

                    throw;
                }
            }
        }

        /// <summary>
        /// 读取当前灯效状态（来自持久化设置，不查询设备）。
        /// </summary>
        public async Task<RGBKeyboardBacklightState> GetStateAsync()
        {
            using (await IoLock.LockAsync().ConfigureAwait(false))
            {
#if !MOCK_RGB
                _ = DeviceHandle ?? throw new InvalidOperationException("RGB Keyboard unsupported");
#endif

                await ThrowIfVantageEnabled().ConfigureAwait(false);

                return settings.Store.State;
            }
        }

        /// <summary>
        /// 保存并应用完整灯效状态。
        /// </summary>
        /// <param name="state">包含选中预设与全部预设定义的灯效状态。</param>
        public async Task SetStateAsync(RGBKeyboardBacklightState state)
        {
            using (await IoLock.LockAsync().ConfigureAwait(false))
            {
#if !MOCK_RGB
                _ = DeviceHandle ?? throw new InvalidOperationException("RGB Keyboard unsupported");
#endif

                await ThrowIfVantageEnabled().ConfigureAwait(false);

                settings.Store.State = state;
                settings.SynchronizeStore();

                var selectedPreset = state.SelectedPreset;

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Selected preset: {selectedPreset}");

                LENOVO_RGB_KEYBOARD_STATE str;
                if (selectedPreset == RGBKeyboardBacklightPreset.Off)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Creating off state.");

                    str = CreateOffState();
                }
                else
                {
                    var presetDescription = state.Presets.GetValueOrDefault(selectedPreset, RGBKeyboardBacklightBacklightPresetDescription.Default);

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Creating state: {presetDescription}");

                    str = Convert(presetDescription);
                }

                await SendToDevice(str).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 切换到指定预设并持久化。
        /// </summary>
        /// <param name="preset">目标预设。</param>
        public async Task SetPresetAsync(RGBKeyboardBacklightPreset preset)
        {
            using (await IoLock.LockAsync().ConfigureAwait(false))
            {
#if !MOCK_RGB
                _ = DeviceHandle ?? throw new InvalidOperationException("RGB Keyboard unsupported");
#endif

                await ThrowIfVantageEnabled().ConfigureAwait(false);

                var state = settings.Store.State;
                var presets = state.Presets;

                settings.Store.State = new(preset, presets);
                settings.SynchronizeStore();

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Preset is {preset}.");

                LENOVO_RGB_KEYBOARD_STATE str;
                if (preset == RGBKeyboardBacklightPreset.Off)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Creating off state.");

                    str = CreateOffState();
                }
                else
                {
                    var presetDescription = state.Presets.GetValueOrDefault(preset, RGBKeyboardBacklightBacklightPresetDescription.Default);

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Creating state: {presetDescription}");

                    str = Convert(presetDescription);
                }

                await SendToDevice(str).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 切换到下一个预设（循环：Off → One → ... → Four → Off）。
        /// </summary>
        /// <returns>切换后的预设。</returns>
        public async Task<RGBKeyboardBacklightPreset> SetNextPresetAsync()
        {
            using (await IoLock.LockAsync().ConfigureAwait(false))
            {
#if !MOCK_RGB
                _ = DeviceHandle ?? throw new InvalidOperationException("RGB Keyboard unsupported");
#endif

                await ThrowIfVantageEnabled().ConfigureAwait(false);

                var state = settings.Store.State;

                var newPreset = state.SelectedPreset.Next();
                var presets = state.Presets;

                settings.Store.State = new(newPreset, presets);
                settings.SynchronizeStore();

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"New preset is {newPreset}.");

                LENOVO_RGB_KEYBOARD_STATE str;
                if (newPreset == RGBKeyboardBacklightPreset.Off)
                {
                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Creating off state.");

                    str = CreateOffState();
                }
                else
                {
                    var presetDescription = state.Presets.GetValueOrDefault(newPreset, RGBKeyboardBacklightBacklightPresetDescription.Default);

                    if (Log.Instance.IsTraceEnabled)
                        Log.Instance.Trace($"Creating state: {presetDescription}");

                    str = Convert(presetDescription);
                }

                await SendToDevice(str).ConfigureAwait(false);

                return newPreset;
            }
        }

        // 按设置中当前选中的预设重新下发（常用于重新取得控制权后恢复外观）
        private async Task SetCurrentPresetAsync()
        {
#if !MOCK_RGB
            _ = DeviceHandle ?? throw new InvalidOperationException("RGB Keyboard unsupported");
#endif

            await ThrowIfVantageEnabled().ConfigureAwait(false);

            var state = settings.Store.State;

            var preset = state.SelectedPreset;

            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Current preset is {preset}.");

            LENOVO_RGB_KEYBOARD_STATE str;
            if (preset == RGBKeyboardBacklightPreset.Off)
            {
                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Creating off state.");

                str = CreateOffState();
            }
            else
            {
                var presetDescription = state.Presets.GetValueOrDefault(preset, RGBKeyboardBacklightBacklightPresetDescription.Default);

                if (Log.Instance.IsTraceEnabled)
                    Log.Instance.Trace($"Creating state: {presetDescription}");

                str = Convert(presetDescription);
            }

            await SendToDevice(str).ConfigureAwait(false);
        }

        // Vantage 运行时会与 HID 写入冲突，因此直接抛异常由上层展示错误
        private async Task ThrowIfVantageEnabled()
        {
            var vantageStatus = await vantageDisabler.GetStatusAsync().ConfigureAwait(false);
            if (vantageStatus == SoftwareStatus.Enabled)
                throw new InvalidOperationException("Can't manage RGB keyboard with Vantage enabled");
        }

        // 通过 HID Feature Report 下发状态：结构体需先封送到非托管内存
        private unsafe Task SendToDevice(LENOVO_RGB_KEYBOARD_STATE str) => Task.Run(() =>
        {
#if !MOCK_RGB
            var handle = DeviceHandle ?? throw new InvalidOperationException("RGB Keyboard unsupported");

            var ptr = IntPtr.Zero;
            try
            {
                var size = Marshal.SizeOf<LENOVO_RGB_KEYBOARD_STATE>();
                ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(str, ptr, false);

                if (!PInvoke.HidD_SetFeature(handle, ptr.ToPointer(), (uint)size))
                    PInvokeExtensions.ThrowIfWin32Error("HidD_SetFeature");
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
#endif
        });

        // 关闭状态：亮度 0、灯效 0，颜色全零
        private static LENOVO_RGB_KEYBOARD_STATE CreateOffState()
        {
            return new()
            {
                Header = [0xCC, 0x16], // 固件约定的固定报文头
                Unused = new byte[13],
                Padding = 0,
                Effect = 0,
                WaveLTR = 0,
                WaveRTL = 0,
                Brightness = 0,
                Zone1Rgb = new byte[3],
                Zone2Rgb = new byte[3],
                Zone3Rgb = new byte[3],
                Zone4Rgb = new byte[3],
            };
        }

        // 把预设描述转换为设备报文：灯效/亮度的枚举值需映射到固件使用的数字编码
        private static LENOVO_RGB_KEYBOARD_STATE Convert(RGBKeyboardBacklightBacklightPresetDescription preset)
        {
            var result = new LENOVO_RGB_KEYBOARD_STATE
            {
                Header = [0xCC, 0x16], // 固件约定的固定报文头
                Unused = new byte[13],
                Padding = 0x0,
                // 动态灯效会忽略分区颜色，先用白色占位
                Zone1Rgb = [0xFF, 0xFF, 0xFF],
                Zone2Rgb = [0xFF, 0xFF, 0xFF],
                Zone3Rgb = [0xFF, 0xFF, 0xFF],
                Zone4Rgb = [0xFF, 0xFF, 0xFF],
                // 固件灯效编码：1=常亮 3=呼吸 4=波浪 6=平滑
                Effect = preset.Effect switch
                {
                    RGBKeyboardBacklightEffect.Static => 1,
                    RGBKeyboardBacklightEffect.Breath => 3,
                    RGBKeyboardBacklightEffect.WaveRTL => 4,
                    RGBKeyboardBacklightEffect.WaveLTR => 4,
                    RGBKeyboardBacklightEffect.Smooth => 6,
                    _ => 0
                },
                WaveRTL = (byte)(preset.Effect == RGBKeyboardBacklightEffect.WaveRTL ? 1 : 0), // 波浪方向标志位
                WaveLTR = (byte)(preset.Effect == RGBKeyboardBacklightEffect.WaveLTR ? 1 : 0), // 波浪方向标志位
                Brightness = preset.Brightness switch
                {
                    RGBKeyboardBacklightBrightness.Low => 1,
                    RGBKeyboardBacklightBrightness.High => 2,
                    _ => 0
                }
            };


            // 常亮灯效没有速度概念，仅动态灯效写入速度
            if (preset.Effect != RGBKeyboardBacklightEffect.Static)
            {
                result.Speed = preset.Speed switch
                {
                    RGBKeyboardBacklightSpeed.Slowest => 1,
                    RGBKeyboardBacklightSpeed.Slow => 2,
                    RGBKeyboardBacklightSpeed.Fast => 3,
                    RGBKeyboardBacklightSpeed.Fastest => 4,
                    _ => 0
                };
            }

            // 只有常亮与呼吸灯效使用自定义分区颜色，其他灯效由固件自行决定颜色
            if (preset.Effect is RGBKeyboardBacklightEffect.Static or RGBKeyboardBacklightEffect.Breath)
            {
                result.Zone1Rgb = [preset.Zone1.R, preset.Zone1.G, preset.Zone1.B];
                result.Zone2Rgb = [preset.Zone2.R, preset.Zone2.G, preset.Zone2.B];
                result.Zone3Rgb = [preset.Zone3.R, preset.Zone3.G, preset.Zone3.B];
                result.Zone4Rgb = [preset.Zone4.R, preset.Zone4.G, preset.Zone4.B];
            }

            return result;
        }
    }
}

using System;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.System;

namespace LenovoLegionToolkit.Lib.Features;

/// <summary>
/// 电池充电模式特性（普通 / 快充 / 养护模式）。
/// 通过能量管理驱动 IOCTL_ENERGY_BATTERY_CHARGE_MODE 设置硬件充电模式，
/// 同时把状态同步写入注册表，以便与联想 Vantage 等服务保持一致。
/// </summary>
public class BatteryFeature() : AbstractDriverFeature<BatteryState>(Drivers.GetEnergy, Drivers.IOCTL_ENERGY_BATTERY_CHARGE_MODE)
{
    // 下列注册表项由联想 Vantage 写入，用于持久化用户选择的充电模式
    private const string BATTERY_CHARGE_MODE_HIVE = "HKEY_CURRENT_USER";
    private const string BATTERY_CHARGE_MODE_PATH = "Software\\Lenovo\\VantageService\\AddinData\\IdeaNotebookAddin";
    private const string BATTERY_CHARGE_MODE_KEY = "BatteryChargeMode";
    private const string BATTERY_CHARGE_MODE_NORMAL = "Normal";
    private const string BATTERY_CHARGE_MODE_RAPID_CHARGE = "Quick";
    private const string BATTERY_CHARGE_MODE_CONSERVATION = "Storage";

    // 查询当前充电模式时使用的 inBuffer 标记值
    protected override uint GetInBufferValue() => 0xFF;

    // 状态 -> 驱动指令序列。某些模式切换需要先退出旧模式再进入新模式，
    // 因此会根据 LastState 决定是否需要额外的过渡指令。
    protected override Task<uint[]> ToInternalAsync(BatteryState state)
    {
        var result = state switch
        {
            BatteryState.Conservation => LastState == BatteryState.RapidCharge ? new uint[] { 0x8, 0x3 } : [0x3],
            BatteryState.Normal => LastState == BatteryState.Conservation ? [0x5] : [0x8],
            BatteryState.RapidCharge => LastState == BatteryState.Conservation ? [0x5, 0x7] : [0x7],
            _ => throw new InvalidOperationException("Invalid state")
        };
        return Task.FromResult(result);
    }

    // 驱动返回值按字节序反转后按位解析
    protected override Task<BatteryState> FromInternalAsync(uint state)
    {
        state = state.ReverseEndianness();

        if (state.GetNthBit(17)) // Is charging?
            // bit26 为 1 表示当前处于快充模式，否则为普通模式
            return Task.FromResult(state.GetNthBit(26) ? BatteryState.RapidCharge : BatteryState.Normal);

        // bit29 为 1 表示养护（Storage）模式，即充电阈值限制在 55%~60%
        if (state.GetNthBit(29))
            return Task.FromResult(BatteryState.Conservation);

        throw new InvalidOperationException($"Unknown battery state: {state} [bits={Convert.ToString(state, 2)}]");
    }

    /// <inheritdoc/>
    public override async Task SetStateAsync(BatteryState state)
    {
        await base.SetStateAsync(state).ConfigureAwait(false);
        // 硬件设置成功后同步注册表，供 Vantage 及本程序启动时校验
        SetStateInRegistry(state);
    }

    /// <summary>
    /// 校验并修正电池模式：若注册表中记录的模式与硬件当前模式不一致（例如 Vantage 修改过或固件重置），
    /// 则按注册表记录重新应用一次，保证两者一致。
    /// </summary>
    public async Task EnsureCorrectBatteryModeIsSetAsync()
    {
        var state = GetStateFromRegistry();

        if (!state.HasValue)
            return;

        if (await GetStateAsync().ConfigureAwait(false) == state.Value)
            return;

        await SetStateAsync(state.Value).ConfigureAwait(false);
    }

    // 从注册表读取字符串形式的充电模式并映射为枚举；未设置或无法识别时返回 null
    private static BatteryState? GetStateFromRegistry()
    {
        var batteryModeString = Registry.GetValue(BATTERY_CHARGE_MODE_HIVE, BATTERY_CHARGE_MODE_PATH, BATTERY_CHARGE_MODE_KEY, string.Empty);
        return batteryModeString switch
        {
            BATTERY_CHARGE_MODE_NORMAL => BatteryState.Normal,
            BATTERY_CHARGE_MODE_RAPID_CHARGE => BatteryState.RapidCharge,
            BATTERY_CHARGE_MODE_CONSERVATION => BatteryState.Conservation,
            _ => null
        };
    }

    // 把枚举映射为注册表中的字符串并写入
    private static void SetStateInRegistry(BatteryState state)
    {
        var batteryModeString = state switch
        {
            BatteryState.Normal => BATTERY_CHARGE_MODE_NORMAL,
            BatteryState.RapidCharge => BATTERY_CHARGE_MODE_RAPID_CHARGE,
            BatteryState.Conservation => BATTERY_CHARGE_MODE_CONSERVATION,
            _ => null
        };

        if (batteryModeString is null)
            return;

        Registry.SetValue(BATTERY_CHARGE_MODE_HIVE, BATTERY_CHARGE_MODE_PATH, BATTERY_CHARGE_MODE_KEY, batteryModeString);
    }
}

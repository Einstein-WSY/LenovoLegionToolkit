using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib;
using LenovoLegionToolkit.Lib.Features;

namespace LenovoLegionToolkit.WPF.CLI.Features;

/// <summary>
/// 泛型功能注册项，将某个 <see cref="IFeature{T}"/> 适配为 CLI 可调用的名称与字符串值接口。
/// </summary>
/// <typeparam name="T">功能状态枚举类型。</typeparam>
/// <param name="name">功能在 CLI 中使用的名称。</param>
/// <param name="toStringConverter">自定义枚举到字符串的转换器（可选）。</param>
/// <param name="fromStringConverter">自定义字符串到枚举的转换器（可选）。</param>
public class FeatureRegistration<T>(string name, Func<T, string>? toStringConverter = null, Func<string, T>? fromStringConverter = null)
    : IFeatureRegistration where T : struct
{
    /// <summary>功能名称。</summary>
    public string Name { get; } = name;

    // 延迟从 IoC 容器解析，避免注册时立即创建功能实例
    private readonly Func<IFeature<T>> _feature = IoCContainer.Resolve<IFeature<T>>;

    /// <summary>判断功能在当前设备上是否受支持。</summary>
    /// <returns>受支持返回 <c>true</c>。</returns>
    public Task<bool> IsSupportedAsync() => _feature().IsSupportedAsync();

    /// <summary>
    /// 获取该功能所有可选值（转换为小写字符串）。
    /// </summary>
    /// <returns>可选值列表。</returns>
    public async Task<IEnumerable<string>> GetValuesAsync()
    {
        var feature = _feature();

        if (!await feature.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("Feature is not supported");

        var states = await feature.GetAllStatesAsync().ConfigureAwait(false);
        // 优先使用自定义转换器，否则回退为枚举名的小写形式
        return states.Select(s => toStringConverter?.Invoke(s) ?? s.ToString()?.ToLowerInvariant()).OfType<string>();
    }

    /// <summary>
    /// 获取功能当前值。
    /// </summary>
    /// <returns>当前值的字符串表示。</returns>
    public async Task<string> GetValueAsync()
    {
        var feature = _feature();

        if (!await feature.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("Feature is not supported");

        var state = await feature.GetStateAsync().ConfigureAwait(false);

        string result;

        if (toStringConverter is not null)
        {
            result = toStringConverter(state);
        }
        else
        {
            result = state.ToString()?.ToLowerInvariant() ?? throw new InvalidOperationException("Null return value");
        }

        return result;
    }

    /// <summary>
    /// 解析字符串并设置功能值，值不在可选范围内时抛出异常。
    /// </summary>
    /// <param name="value">目标值的字符串表示。</param>
    public async Task SetValueAsync(string value)
    {
        var feature = _feature();

        if (!await feature.IsSupportedAsync().ConfigureAwait(false))
            throw new InvalidOperationException("Feature is not supported");

        var states = await feature.GetAllStatesAsync().ConfigureAwait(false);

        T state;

        if (fromStringConverter is not null)
        {
            state = fromStringConverter(value);
        }
        else
        {
            state = Enum.TryParse<T>(value, true, out var s)
                ? s
                : throw new InvalidOperationException("State is not supported");
        }

        // 校验解析结果确实属于该功能支持的状态集合
        if (!states.Contains(state))
            throw new InvalidOperationException("State is not supported");

        await feature.SetStateAsync(state).ConfigureAwait(false);
    }
}

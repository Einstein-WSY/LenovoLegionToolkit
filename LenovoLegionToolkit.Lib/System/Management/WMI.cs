using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.System.Management;

/// <summary>
/// WMI 访问基础设施（部分类）。
/// 统一封装 WMI 命名空间（scope）选择、查询执行、方法调用与事件订阅，
/// 并通过 <see cref="WMIPropertyValueFormatter"/> 自动转义查询字符串中的反斜杠。
/// </summary>
public static partial class WMI
{
    /// <summary>
    /// WMI 命名空间作用域缓存。
    /// 【性能优化】每次 new ManagementObjectSearcher(string scope, ...) 都会重新解析命名空间路径并建立连接，
    /// 而本项目在启动、状态浮窗、传感器轮询等路径上会高频执行 WMI 查询，重复建连开销明显。
    /// 这里按命名空间复用已连接的 <see cref="ManagementScope"/>，避免重复建连。
    /// </summary>
    private static readonly ConcurrentDictionary<string, ManagementScope> ScopeCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 获取（必要时创建并连接）指定命名空间的 WMI 作用域。
    /// </summary>
    /// <param name="path">WMI 命名空间路径，如 root\WMI。</param>
    /// <returns>已连接的作用域实例（按命名空间复用）。</returns>
    private static ManagementScope GetScope(string path)
    {
        var scope = ScopeCache.GetOrAdd(path, static p => new ManagementScope(p));

        // IsConnected 为 false 时才建连；Connect() 幂等，并发调用安全。
        if (!scope.IsConnected)
            scope.Connect();

        return scope;
    }

    /// <summary>
    /// 判断指定的 WMI 实例是否存在。
    /// </summary>
    /// <param name="scope">WMI 命名空间，如 root\WMI。</param>
    /// <param name="query">WQL 查询语句。</param>
    /// <returns>存在返回 true；查询异常或没有结果时返回 false。</returns>
    private static async Task<bool> ExistsAsync(string scope, FormattableString query)
    {
        try
        {
            var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);
            // 【性能优化】复用已连接的作用域，并显式释放查询对象（底层持有 COM 资源）
            using var mos = new ManagementObjectSearcher(GetScope(scope), new ObjectQuery(queryFormatted));
            var managementObjects = await mos.GetAsync().ConfigureAwait(false);
            return managementObjects.Any();
        }
        catch
        {
            // 该 WMI 类在机型上不存在时查询会抛异常，此处即视为"不支持"
            return false;
        }
    }

    /// <summary>
    /// 订阅 WMI 事件。
    /// </summary>
    /// <param name="scope">WMI 命名空间。</param>
    /// <param name="query">WQL 事件查询语句（通常包含 WITHIN 子句指定轮询间隔）。</param>
    /// <param name="handler">事件到达时的回调，参数为事件的属性集合。</param>
    /// <returns>可释放对象，释放时停止并销毁事件监视器。</returns>
    private static LambdaDisposable Listen(string scope, FormattableString query, Action<PropertyDataCollection> handler)
    {
        var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);
        var watcher = new ManagementEventWatcher(scope, queryFormatted);
        watcher.EventArrived += (_, e) => handler(e.NewEvent.Properties);
        watcher.Start();

        return new LambdaDisposable(() =>
        {
            watcher.Stop();
            watcher.Dispose();
        });
    }

    /// <summary>
    /// 执行 WQL 查询并把每条结果的属性集合转换为目标类型。
    /// </summary>
    /// <typeparam name="T">转换后的结果类型。</typeparam>
    /// <param name="scope">WMI 命名空间。</param>
    /// <param name="query">WQL 查询语句。</param>
    /// <param name="converter">把属性集合转换为 T 的委托。</param>
    /// <returns>转换后的结果序列。</returns>
    /// <exception cref="ManagementException">WMI 查询失败时抛出（附带 scope 与 query 便于定位）。</exception>
    private static async Task<IEnumerable<T>> ReadAsync<T>(string scope, FormattableString query, Func<PropertyDataCollection, T> converter)
    {
        try
        {
            var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);
            // 【性能优化】复用已连接的作用域，并显式释放查询对象
            using var mos = new ManagementObjectSearcher(GetScope(scope), new ObjectQuery(queryFormatted));
            var managementObjects = await mos.GetAsync().ConfigureAwait(false);
            var result = managementObjects.Select(mo => mo.Properties).Select(converter);
            return result;
        }
        catch (ManagementException ex)
        {
            throw new ManagementException($"Read failed: {ex.Message} [scope={scope}, query={query}]", ex);
        }
    }

    /// <summary>
    /// 调用 WMI 实例方法（无返回值）。
    /// </summary>
    /// <param name="scope">WMI 命名空间。</param>
    /// <param name="query">用于定位实例的 WQL 查询。</param>
    /// <param name="methodName">方法名。</param>
    /// <param name="methodParams">入参键值对（键为 WMI 方法参数名）。</param>
    /// <exception cref="InvalidOperationException">查询未返回任何实例时抛出。</exception>
    /// <exception cref="ManagementException">WMI 调用失败时抛出。</exception>
    private static async Task CallAsync(string scope, FormattableString query, string methodName, Dictionary<string, object> methodParams)
    {
        try
        {
            var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);
            // 【性能优化】复用已连接的作用域，并显式释放查询对象
            using var mos = new ManagementObjectSearcher(GetScope(scope), new ObjectQuery(queryFormatted));
            var managementObjects = await mos.GetAsync().ConfigureAwait(false);
            var managementObject = managementObjects.FirstOrDefault() ?? throw new InvalidOperationException("No results in query");

            var mo = (ManagementObject)managementObject;
            // 先取方法签名以构造参数对象，避免参数名/类型不匹配
            var methodParamsObject = mo.GetMethodParameters(methodName);
            foreach (var pair in methodParams)
                methodParamsObject[pair.Key] = pair.Value;

            mo.InvokeMethod(methodName, methodParamsObject, new InvokeMethodOptions());
        }
        catch (ManagementException ex)
        {
            throw new ManagementException($"Call failed: {ex.Message} [scope={scope}, query={query}, methodName={methodName}]", ex);
        }
    }

    /// <summary>
    /// 调用 WMI 实例方法并把返回的属性集合转换为目标类型。
    /// </summary>
    /// <typeparam name="T">返回值类型。</typeparam>
    /// <param name="scope">WMI 命名空间。</param>
    /// <param name="query">用于定位实例的 WQL 查询。</param>
    /// <param name="methodName">方法名。</param>
    /// <param name="methodParams">入参键值对。</param>
    /// <param name="converter">把方法返回的属性集合转换为 T 的委托。</param>
    /// <returns>转换后的方法返回值。</returns>
    /// <exception cref="InvalidOperationException">查询未返回任何实例时抛出。</exception>
    /// <exception cref="ManagementException">WMI 调用失败时抛出。</exception>
    private static async Task<T> CallAsync<T>(string scope, FormattableString query, string methodName, Dictionary<string, object> methodParams, Func<PropertyDataCollection, T> converter)
    {
        try
        {
            var queryFormatted = query.ToString(WMIPropertyValueFormatter.Instance);

            // 【性能优化】复用已连接的作用域，并显式释放查询对象
            using var mos = new ManagementObjectSearcher(GetScope(scope), new ObjectQuery(queryFormatted));
            var managementObjects = await mos.GetAsync().ConfigureAwait(false);
            var managementObject = managementObjects.FirstOrDefault() ?? throw new InvalidOperationException("No results in query");

            var mo = (ManagementObject)managementObject;
            var methodParamsObject = mo.GetMethodParameters(methodName);
            foreach (var pair in methodParams)
                methodParamsObject[pair.Key] = pair.Value;

            var resultProperties = mo.InvokeMethod(methodName, methodParamsObject, new InvokeMethodOptions());
            var result = converter(resultProperties.Properties);
            return result;
        }
        catch (ManagementException ex)
        {
            throw new ManagementException($"Call failed: {ex.Message}. [scope={scope}, query={query}, methodName={methodName}]", ex);
        }
    }

    /// <summary>
    /// 把插值字符串中的参数格式化为可安全嵌入 WQL 的字面量。
    /// WQL 中反斜杠是转义符（如命名空间 root\WMI），因此必须转义为双反斜杠。
    /// </summary>
    private class WMIPropertyValueFormatter : IFormatProvider, ICustomFormatter
    {
        /// <summary>单例实例。</summary>
        public static readonly WMIPropertyValueFormatter Instance = new();

        private WMIPropertyValueFormatter() { }

        /// <summary>
        /// 返回自定义格式化器本身。
        /// </summary>
        /// <param name="formatType">请求的格式化类型。</param>
        /// <returns>当请求 ICustomFormatter 时返回 this。</returns>
        /// <exception cref="InvalidOperationException">请求了不支持的格式化类型时抛出。</exception>
        public object GetFormat(Type? formatType)
        {
            if (formatType == typeof(ICustomFormatter))
                return this;

            throw new InvalidOperationException("Invalid type of formatted");
        }

        /// <summary>
        /// 格式化单个参数值：转义反斜杠，null 转为空字符串。
        /// </summary>
        /// <param name="format">格式字符串（此处未使用）。</param>
        /// <param name="arg">待格式化的参数。</param>
        /// <param name="formatProvider">格式提供程序（此处未使用）。</param>
        /// <returns>转义后的字符串。</returns>
        public string Format(string? format, object? arg, IFormatProvider? formatProvider)
        {
            var stringArg = arg?.ToString()?.Replace("\\", "\\\\");
            return stringArg ?? string.Empty;
        }
    }
}

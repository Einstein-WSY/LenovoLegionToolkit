using System;
using Autofac;

namespace LenovoLegionToolkit.Lib;

/// <summary>
/// 全局 Autofac 依赖注入容器的静态入口。
/// 应用启动时调用 <see cref="Initialize"/> 注册模块，之后各处通过 <see cref="Resolve{T}"/> 获取服务实例。
/// </summary>
public static class IoCContainer
{
    private static readonly object Lock = new(); // 保护 _container 初始化与解析的线程锁

    private static IContainer? _container; // 全局唯一的容器实例；null 表示尚未初始化

    /// <summary>
    /// 初始化全局容器，依次注册传入的模块。
    /// </summary>
    /// <param name="modules">要注册的 Autofac 模块（如 <see cref="IoCModule"/>）。</param>
    /// <exception cref="InvalidOperationException">容器已初始化时抛出，防止重复构建。</exception>
    public static void Initialize(params Module[] modules)
    {
        lock (Lock)
        {
            if (_container is not null)
                throw new InvalidOperationException("IoCContainer already initialized");

            var cb = new ContainerBuilder();

            foreach (var module in modules)
                cb.RegisterModule(module);

            _container = cb.Build();
        }
    }

    /// <summary>
    /// 解析指定类型的服务实例（不存在时由 Autofac 抛异常）。
    /// </summary>
    /// <typeparam name="T">要解析的服务类型。</typeparam>
    /// <returns>服务实例。</returns>
    /// <exception cref="InvalidOperationException">容器尚未初始化时抛出。</exception>
    public static T Resolve<T>() where T : notnull
    {
        lock (Lock)
        {
            if (_container is null)
                throw new InvalidOperationException($"IoCContainer must be initialized first [type={nameof(T)}]");
            return _container.Resolve<T>();
        }
    }

    /// <summary>
    /// 尝试解析指定类型的服务实例；未注册时返回 null 而不抛异常。
    /// </summary>
    /// <typeparam name="T">要解析的服务类型。</typeparam>
    /// <returns>服务实例；未注册时为 null。</returns>
    /// <exception cref="InvalidOperationException">容器尚未初始化时抛出。</exception>
    public static T? TryResolve<T>() where T : class
    {
        lock (Lock)
        {
            if (_container is null)
                throw new InvalidOperationException($"IoCContainer must be initialized first [type={nameof(T)}]");
            _ = _container.TryResolve(out T? value);
            return value;
        }
    }

}

using Autofac;
using Autofac.Builder;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// Autofac 容器构建器的扩展方法，简化类型注册流程。
/// </summary>
public static class ContainerBuilderExtensions
{
    /// <summary>
    /// 将指定类型注册为单例服务。
    /// <para>
    /// 默认同时注册为"自身类型 + 全部已实现接口"，这样既可注入具体类型，也可注入任意接口；
    /// 若只想按自身类型注入（避免接口注册带来的多实例歧义），可传入 <paramref name="selfOnly"/>。
    /// </para>
    /// </summary>
    /// <typeparam name="T">要注册的具体类型。</typeparam>
    /// <param name="cb">Autofac 容器构建器。</param>
    /// <param name="selfOnly">为 <c>true</c> 时仅按自身类型注册，不同时注册其实现的接口。</param>
    /// <returns>注册构建器，可继续链式配置。</returns>
    public static IRegistrationBuilder<T, ConcreteReflectionActivatorData, SingleRegistrationStyle> Register<T>(this ContainerBuilder cb, bool selfOnly = false) where T : notnull
    {
        var registration = cb.RegisterType<T>().AsSelf();
        if (!selfOnly)
            registration = registration.AsImplementedInterfaces();
        return registration.SingleInstance();
    }
}

using System;
using Autofac;
using Autofac.Builder;
using LenovoLegionToolkit.Lib.Listeners;

namespace LenovoLegionToolkit.Lib.Extensions;

/// <summary>
/// Autofac 注册构建器的扩展方法。
/// </summary>
public static class RegistrationBuilderExtensions
{
    /// <summary>
    /// 将监听器注册为"激活时自动启动"。
    /// <para>
    /// 在实例被容器创建（OnActivating）时调用其 <c>StartAsync</c>，实现监听器随容器启停而自动运行；
    /// <c>AsValueTask()</c> 把返回的 <see cref="Task"/> 适配为 Autofac 回调要求的 <see cref="ValueTask"/>。
    /// </para>
    /// </summary>
    /// <typeparam name="T">监听器所监听的事件参数类型。</typeparam>
    /// <param name="registration">监听器注册构建器。</param>
    public static void AutoActivateListener<T>(this IRegistrationBuilder<IListener<T>, ConcreteReflectionActivatorData, SingleRegistrationStyle> registration) where T : EventArgs
    {
        registration.OnActivating(e => e.Instance.StartAsync().AsValueTask()).AutoActivate();
    }
}

using Autofac;
using LenovoLegionToolkit.Lib.Automation.Utils;
using LenovoLegionToolkit.Lib.Extensions;

namespace LenovoLegionToolkit.Lib.Automation;

/// <summary>
/// 自动化模块的 Autofac 注册入口，向容器注册设置与流水线处理器（均为单例）。
/// </summary>
public class IoCModule : Module
{
    /// <summary>
    /// 注册本模块所需的依赖。
    /// </summary>
    /// <param name="builder">容器构建器。</param>
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register<AutomationSettings>();
        builder.Register<AutomationProcessor>();
    }
}

using Autofac;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.Lib.Macro.Utils;

namespace LenovoLegionToolkit.Lib.Macro;

/// <summary>
/// 宏子系统的 Autofac 依赖注入模块，注册宏设置与宏控制器。
/// </summary>
public class IoCModule : Module
{
    /// <summary>
    /// 向容器注册宏相关的服务。
    /// </summary>
    /// <param name="builder">Autofac 容器构建器。</param>
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register<MacroSettings>();
        builder.Register<MacroController>();
    }
}

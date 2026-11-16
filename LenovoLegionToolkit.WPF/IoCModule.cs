using Autofac;
using LenovoLegionToolkit.Lib.Extensions;
using LenovoLegionToolkit.WPF.CLI;
using LenovoLegionToolkit.WPF.Settings;
using LenovoLegionToolkit.WPF.Utils;

namespace LenovoLegionToolkit.WPF;

/// <summary>
/// Autofac 依赖注入模块，注册 WPF 层所需的单例服务。
/// </summary>
public class IoCModule : Module
{
    /// <summary>
    /// 向容器注册各类服务；带 <c>AutoActivate</c> 的服务会在容器构建后立即实例化。
    /// </summary>
    /// <param name="builder">Autofac 容器构建器。</param>
    protected override void Load(ContainerBuilder builder)
    {
        // 主线程调度器，供后台线程切回 UI 线程使用
        builder.Register<MainThreadDispatcher>();

        // 屏幕取色（Spectrum）截图辅助
        builder.Register<SpectrumScreenCapture>();

        // 主题与通知管理器需要随应用启动自动激活
        builder.Register<ThemeManager>().AutoActivate();
        builder.Register<NotificationsManager>().AutoActivate();

        // 仪表盘设置
        builder.Register<DashboardSettings>();

        // 单实例进程间通信服务端
        builder.Register<IpcServer>();
    }
}

using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using LenovoLegionToolkit.CLI.Lib;

namespace LenovoLegionToolkit.CLI;

/// <summary>
/// CLI 入口：构建 System.CommandLine 命令树并把参数转发到 <see cref="IpcClient"/>。
/// 主程序需在后台运行并在设置中启用 CLI 功能，命令才能生效。
/// </summary>
public class Program
{
    /// <summary>
    /// 程序入口，解析并执行命令行参数。
    /// </summary>
    /// <param name="args">命令行参数。</param>
    /// <returns>
    /// 进程退出码：正常为 0；无法连接主程序为 -1；主程序返回失败为 -2；未预期异常为 -99。
    /// </returns>
    public static Task<int> Main(string[] args) => BuildCommandLine().InvokeAsync(args);

    /// <summary>
    /// 构建完整的命令树，包含 quickAction、feature、spectrum、rgb 四组命令。
    /// </summary>
    /// <returns>可执行解析的 <see cref="Parser"/>。</returns>
    private static Parser BuildCommandLine()
    {
        var root = new RootCommand("Utility that controls Lenovo Legion Toolkit from command line.\n\n" +
                                   "Lenovo Legion Toolkit must be running in the background and CLI setting must be " +
                                   "turned on for this utility to work.");

        var builder = new CommandLineBuilder(root)
            .UseDefaults()
            // 统一异常处理，把异常转换为可读的错误信息和退出码
            .UseExceptionHandler(OnException);

        root.AddCommand(BuildQuickActionsCommand());
        root.AddCommand(BuildFeatureCommand());
        root.AddCommand(BuildSpectrumCommand());
        root.AddCommand(BuildRGBCommand());

        return builder.Build();
    }

    /// <summary>
    /// 构建 <c>quickAction</c>（别名 <c>qa</c>）命令：
    /// <c>quickAction &lt;name&gt;</c> 执行指定快捷操作，<c>quickAction --list</c> 列出全部快捷操作。
    /// </summary>
    /// <returns>快捷操作命令。</returns>
    private static Command BuildQuickActionsCommand()
    {
        var nameArgument = new Argument<string>("name", "Name of the Quick Action") { Arity = ArgumentArity.ZeroOrOne };

        var listOption = new Option<bool>("--list", "List available Quick Actions") { Arity = ArgumentArity.ZeroOrOne };
        listOption.AddAlias("-l");

        var cmd = new Command("quickAction", "Run Quick Action");
        cmd.AddAlias("qa");
        cmd.AddArgument(nameArgument);
        cmd.AddOption(listOption);
        cmd.SetHandler(async (name, list) =>
        {
            if (list)
            {
                var result = await IpcClient.ListQuickActionsAsync();
                Console.WriteLine(result);
                return;
            }

            await IpcClient.RunQuickActionAsync(name);
        }, nameArgument, listOption);
        // 必须提供名称或 --list 之一，否则给出明确的使用提示
        cmd.AddValidator(result =>
        {
            if (result.FindResultFor(nameArgument) is not null)
                return;

            if (result.FindResultFor(listOption) is not null)
                return;

            result.ErrorMessage = $"{nameArgument.Name} or --{listOption.Name} should be specified";
        });

        return cmd;
    }

    /// <summary>
    /// 构建 <c>feature</c>（别名 <c>f</c>）命令组：
    /// 下设 <c>get</c>（读取功能值）、<c>set</c>（设置功能值）两个子命令，以及 <c>--list</c>（列出功能）。
    /// </summary>
    /// <returns>功能控制命令。</returns>
    private static Command BuildFeatureCommand()
    {
        var getCmd = BuildGetFeatureCommand();
        var setCmd = BuildSetFeatureCommand();

        var listOption = new Option<bool?>("--list", "List available features") { Arity = ArgumentArity.ZeroOrOne };
        listOption.AddAlias("-l");

        var cmd = new Command("feature", "Control features");
        cmd.AddAlias("f");
        cmd.AddCommand(getCmd);
        cmd.AddCommand(setCmd);
        cmd.AddOption(listOption);
        cmd.SetHandler(async list =>
        {
            // 只有显式传入 --list 且为真值时才输出列表
            if (!list.HasValue || !list.Value)
                return;

            var value = await IpcClient.ListFeaturesAsync();
            Console.WriteLine(value);
        }, listOption);
        // 必须指定 get、set 或 --list 之一
        cmd.AddValidator(result =>
        {
            if (result.FindResultFor(getCmd) is not null)
                return;

            if (result.FindResultFor(setCmd) is not null)
                return;

            if (result.FindResultFor(listOption) is not null)
                return;

            result.ErrorMessage = $"{getCmd.Name}, {setCmd.Name} or --{listOption.Name} should be specified";
        });

        return cmd;
    }

    /// <summary>
    /// 构建 <c>feature get</c>（别名 <c>g</c>）子命令：<c>feature get &lt;name&gt;</c> 输出功能当前值。
    /// </summary>
    /// <returns>读取功能值的命令。</returns>
    private static Command BuildGetFeatureCommand()
    {
        var nameArgument = new Argument<string>("name", "Name of the feature") { Arity = ArgumentArity.ExactlyOne };

        var cmd = new Command("get", "Get value of a feature");
        cmd.AddAlias("g");
        cmd.AddArgument(nameArgument);
        cmd.SetHandler(async name =>
        {
            var result = await IpcClient.GetFeatureValueAsync(name);
            Console.WriteLine(result);
        }, nameArgument);

        return cmd;
    }

    /// <summary>
    /// 构建 <c>feature set</c>（别名 <c>s</c>）子命令：
    /// <c>feature set &lt;name&gt; &lt;value&gt;</c> 设置功能值，<c>feature set &lt;name&gt; --list</c> 列出该功能可选值。
    /// </summary>
    /// <returns>设置功能值的命令。</returns>
    private static Command BuildSetFeatureCommand()
    {
        var nameArgument = new Argument<string>("name", "Name of the feature") { Arity = ArgumentArity.ExactlyOne };
        var valueArgument = new Argument<string>("value", "Value of the feature") { Arity = ArgumentArity.ZeroOrOne };

        var listOption = new Option<bool>("--list", "List available feature values") { Arity = ArgumentArity.ZeroOrOne };
        listOption.AddAlias("-l");

        var cmd = new Command("set", "Set value of a feature");
        cmd.AddAlias("s");
        cmd.AddArgument(nameArgument);
        cmd.AddArgument(valueArgument);
        cmd.AddOption(listOption);
        cmd.SetHandler(async (name, value, list) =>
        {
            if (list)
            {
                var result = await IpcClient.ListFeatureValuesAsync(name);
                Console.WriteLine(result);
                return;
            }

            await IpcClient.SetFeatureValueAsync(name, value);
        }, nameArgument, valueArgument, listOption);
        cmd.AddValidator(result =>
        {
            if (result.FindResultFor(nameArgument) is not null)
                return;

            if (result.FindResultFor(listOption) is not null)
                return;

            result.ErrorMessage = $"{nameArgument.Name} or --{listOption.Name} should be specified";
        });

        return cmd;
    }

    /// <summary>
    /// 构建 <c>spectrum</c>（别名 <c>s</c>）命令组，包含 profile 与 brightness 两个子命令组。
    /// </summary>
    /// <returns>Spectrum 灯效控制命令。</returns>
    private static Command BuildSpectrumCommand()
    {
        var profileCommand = BuildSpectrumProfileCommand();
        var brightnessCommand = BuildSpectrumBrightnessCommand();

        var cmd = new Command("spectrum", "Control Spectrum backlight");
        cmd.AddAlias("s");
        cmd.AddCommand(profileCommand);
        cmd.AddCommand(brightnessCommand);
        return cmd;
    }

    /// <summary>
    /// 构建 <c>spectrum profile</c>（别名 <c>p</c>）命令组，用于读取或设置灯效配置。
    /// </summary>
    /// <returns>Spectrum 配置命令。</returns>
    private static Command BuildSpectrumProfileCommand()
    {
        var getCmd = BuildGetSpectrumProfileCommand();
        var setCmd = BuildSetSpectrumProfileCommand();

        var cmd = new Command("profile", "Control Spectrum backlight profile");
        cmd.AddAlias("p");
        cmd.AddCommand(getCmd);
        cmd.AddCommand(setCmd);

        return cmd;
    }

    /// <summary>
    /// 构建 <c>spectrum profile get</c>（别名 <c>g</c>）子命令，输出当前灯效配置编号。
    /// </summary>
    /// <returns>读取 Spectrum 配置的命令。</returns>
    private static Command BuildGetSpectrumProfileCommand()
    {
        var cmd = new Command("get", "Get current Spectrum profile");
        cmd.AddAlias("g");
        cmd.SetHandler(async _ =>
        {
            var result = await IpcClient.GetSpectrumProfileAsync();
            Console.WriteLine(result);
        });

        return cmd;
    }

    /// <summary>
    /// 构建 <c>spectrum profile set</c>（别名 <c>s</c>）子命令：<c>spectrum profile set &lt;profile&gt;</c> 设置灯效配置。
    /// </summary>
    /// <returns>设置 Spectrum 配置的命令。</returns>
    private static Command BuildSetSpectrumProfileCommand()
    {
        var valueArgument = new Argument<int>("profile", "Profile to set") { Arity = ArgumentArity.ExactlyOne };

        var cmd = new Command("set", "Set current Spectrum profile");
        cmd.AddAlias("s");
        cmd.AddArgument(valueArgument);
        cmd.SetHandler(async value =>
        {
            await IpcClient.SetSpectrumProfileAsync($"{value}");
        }, valueArgument);

        return cmd;
    }

    /// <summary>
    /// 构建 <c>spectrum brightness</c>（别名 <c>b</c>）命令组，用于读取或设置亮度。
    /// </summary>
    /// <returns>Spectrum 亮度命令。</returns>
    private static Command BuildSpectrumBrightnessCommand()
    {
        var getCmd = BuildGetSpectrumBrightnessCommand();
        var setCmd = BuildSetSpectrumBrightnessCommand();

        var cmd = new Command("brightness", "Control Spectrum brightness");
        cmd.AddAlias("b");
        cmd.AddCommand(getCmd);
        cmd.AddCommand(setCmd);

        return cmd;
    }

    /// <summary>
    /// 构建 <c>spectrum brightness get</c>（别名 <c>g</c>）子命令，输出当前亮度。
    /// </summary>
    /// <returns>读取 Spectrum 亮度的命令。</returns>
    private static Command BuildGetSpectrumBrightnessCommand()
    {
        var cmd = new Command("get", "Get current Spectrum brightness");
        cmd.AddAlias("g");
        cmd.SetHandler(async _ =>
        {
            var result = await IpcClient.GetSpectrumBrightnessAsync();
            Console.WriteLine(result);
        });

        return cmd;
    }

    /// <summary>
    /// 构建 <c>spectrum brightness set</c>（别名 <c>s</c>）子命令：<c>spectrum brightness set &lt;brightness&gt;</c> 设置亮度。
    /// </summary>
    /// <returns>设置 Spectrum 亮度的命令。</returns>
    private static Command BuildSetSpectrumBrightnessCommand()
    {
        var valueArgument = new Argument<int>("brightness", "Brightness to set") { Arity = ArgumentArity.ExactlyOne };

        var cmd = new Command("set", "Set current Spectrum brightness");
        cmd.AddAlias("s");
        cmd.AddArgument(valueArgument);
        cmd.SetHandler(async value =>
        {
            await IpcClient.SetSpectrumBrightnessAsync($"{value}");
        }, valueArgument);

        return cmd;
    }

    /// <summary>
    /// 构建 <c>rgb</c>（别名 <c>r</c>）命令组，用于读取或设置 RGB 灯效预设。
    /// </summary>
    /// <returns>RGB 预设命令。</returns>
    private static Command BuildRGBCommand()
    {
        var getCmd = BuildGetRGBCommand();
        var setCmd = BuildSetRGBCommand();

        var cmd = new Command("rgb", "Control RGB backlight preset");
        cmd.AddAlias("r");
        cmd.AddCommand(getCmd);
        cmd.AddCommand(setCmd);

        return cmd;
    }

    /// <summary>
    /// 构建 <c>rgb get</c>（别名 <c>g</c>）子命令，输出当前 RGB 预设。
    /// </summary>
    /// <returns>读取 RGB 预设的命令。</returns>
    private static Command BuildGetRGBCommand()
    {
        var cmd = new Command("get", "Get current RGB preset");
        cmd.AddAlias("g");
        cmd.SetHandler(async _ =>
        {
            var result = await IpcClient.GetRGBPresetAsync();
            Console.WriteLine(result);
        });

        return cmd;
    }

    /// <summary>
    /// 构建 <c>rgb set</c>（别名 <c>s</c>）子命令：<c>rgb set &lt;preset&gt;</c> 设置 RGB 预设。
    /// </summary>
    /// <returns>设置 RGB 预设的命令。</returns>
    private static Command BuildSetRGBCommand()
    {
        var valueArgument = new Argument<int>("preset", "Preset to set") { Arity = ArgumentArity.ExactlyOne };

        var cmd = new Command("set", "Set current RGB preset");
        cmd.AddAlias("s");
        cmd.AddArgument(valueArgument);
        cmd.SetHandler(async value =>
        {
            await IpcClient.SetRGBPresetAsync($"{value}");
        }, valueArgument);

        return cmd;
    }

    /// <summary>
    /// 全局异常处理：把异常映射为面向用户的错误信息与进程退出码，并以红色输出到标准错误。
    /// </summary>
    /// <param name="ex">捕获到的异常。</param>
    /// <param name="context">命令调用上下文，用于设置退出码与输出。</param>
    private static void OnException(Exception ex, InvocationContext context)
    {
        var message = ex switch
        {
            // 连接失败时给出排查提示，而不是抛出原始异常文本
            IpcConnectException => "Failed to connect. " +
                                   "Make sure that Lenovo Legion Toolkit is running " +
                                   "in background and CLI is enabled in Settings.",
            IpcException => ex.Message,
            _ => ex.ToString()
        };
        // 退出码约定：-1 连接失败，-2 请求失败，-99 其它未预期异常
        var exitCode = ex switch
        {
            IpcConnectException => -1,
            IpcException => -2,
            _ => -99
        };

        // 输出被重定向（如管道）时不修改控制台颜色，避免污染捕获到的文本
        if (!Console.IsOutputRedirected)
        {
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Red;
        }

        context.Console.Error.WriteLine(message);
        context.ExitCode = exitCode;

        if (!Console.IsOutputRedirected)
            Console.ResetColor();
    }
}

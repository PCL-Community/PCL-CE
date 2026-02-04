using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using PCL.Core.App.Cli;

namespace PCL.Core.App;

[LifecycleService(LifecycleState.BeforeLoading, Priority = int.MaxValue)]
[LifecycleScope("startup", "基本信息", false)]
public sealed partial class StartupService
{
    private static Exception _GetUninitializedException() => new InvalidOperationException("Not initialized");

    /// <summary>
    /// 解析后的命令行模型实例
    /// </summary>
    /// <exception cref="Exception">尚未初始化完成</exception>
    public static CommandLine CommandLine
    {
        get => field ?? throw _GetUninitializedException();
        private set;
    } = null!;

    private static readonly ConcurrentDictionary<string, CommandArgument> _UnhandledCommandMap = [];

    /// <summary>
    /// 未处理的子命令
    /// </summary>
    public static ICollection<string> UnhandledCommands => _UnhandledCommandMap.Keys;

    /// <summary>
    /// 处理一个子命令
    /// </summary>
    /// <param name="command">子命令</param>
    /// <returns>参数</returns>
    /// <exception cref="KeyNotFoundException">指定子命令不存在</exception>
    public static CommandArgument HandleCommand(string command)
    {
        var exists = _UnhandledCommandMap.TryRemove(command, out var arg);
        return exists ? arg! : throw new KeyNotFoundException("Command not found");
    }

    [LifecycleStart]
    private static void _LogBasicInfo()
    {
        Context.Info($"程序路径: {Basics.ExecutablePath}");
        var argStr = new StringBuilder("命令行参数:");
        foreach (var x in Basics.FullCommandLineArguments) argStr.Append("\n - ").Append(x);
        Context.Info(argStr.ToString());
    }

    [LifecycleStart]
    private static void _ParseCommandLineArgs()
    {
        Context.Debug("正在解析命令行参数...");
        IEnumerable<SubcommandDefinition> subcommands = [
            ("update", [("execute"), ("success"), ("failed")]),
            ("activate"),
            ("memory")
        ];
        CommandLine = CommandLine.Parse(Basics.FullCommandLineArguments, subcommands);
    }
}

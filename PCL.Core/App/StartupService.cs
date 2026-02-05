using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using PCL.Core.App.Cli;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;

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

    private static readonly Dictionary<string, CommandLine> _UnhandledCommandMap = [];

    /// <summary>
    /// 未处理的子命令
    /// </summary>
    public static ICollection<string> UnhandledCommands => _UnhandledCommandMap.Keys;

    /// <summary>
    /// 处理一个子命令
    /// </summary>
    /// <param name="command">子命令</param>
    /// <param name="model">命令行模型</param>
    /// <returns>子命令是否存在</returns>
    /// <exception cref="KeyNotFoundException">指定子命令不存在</exception>
    public static bool TryHandleCommand(string command, [MaybeNullWhen(false)] out CommandLine model)
    {
        lock (_UnhandledCommandMap)
        {
            _UnhandledCommandMap.TryGetValue(command, out model);
            if (model == null) return false;
            // remove all related commands
            foreach (var x in _UnhandledCommandMap.Keys.ToList().Where(x => x.StartsWith(command)))
                _UnhandledCommandMap.Remove(x);
            return true;
        }
    }

    [LifecycleStart]
    private static void _LogBasicInfo()
    {
        var info = new StringBuilder();
        info.Append("\n版本: ").Append(Basics.Metadata.Version).Append(" (").Append(GetArchitectureName(RuntimeInformation.ProcessArchitecture)).Append(')');
        info.Append("\n路径: ").Append(Basics.ExecutablePath);
        info.Append("\n命令行参数:");
        if (Basics.CommandLineArguments.Length == 0) info.Append(" []");
        else foreach (var x in Basics.CommandLineArguments) info.Append("\n - ").Append(x);
        info.Append("\n系统版本: ").Append(Environment.OSVersion.Version).Append(" (").Append(GetArchitectureName(RuntimeInformation.OSArchitecture)).Append(')');
        var memory = KernelInterop.GetPhysicalMemoryBytes();
        const int memoryDiv = 1024 * 1024;
        info.Append("\n可用内存: ").Append(memory.Available / memoryDiv).Append('/').Append(memory.Total / memoryDiv).Append(" MB");
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var cp = Encoding.GetEncoding(0);
        info.Append("\n默认代码页: ").Append(cp.EncodingName).Append(" (").Append(cp.CodePage).Append(')');
        info.Append("\n管理员身份: ").Append(ProcessInterop.IsAdmin());
        info.Append("\n识别码: ").Append(Identify.LauncherId);
        Context.Info(info.ToString());
        return;
        string GetArchitectureName(Architecture arch) => arch switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "ARM64",
            _ => arch.ToString()
        };
    }

    [LifecycleStart]
    private static void _ParseCommandLineArgs()
    {
        IEnumerable<SubcommandDefinition> subcommands = [
            ("update", [("execute"), ("success"), ("failed")]),
            ("activate"),
            ("memory")
        ];
        Context.Debug("正在解析命令行参数...");
        var c = CommandLine.Parse(Basics.FullCommandLineArguments, subcommands);
        var prefix = new StringBuilder();
        while (true)
        {
            _UnhandledCommandMap[prefix.ToString()] = c;
            if (c.Subcommand == null) break;
            prefix.Append('.').Append(c.Subcommand.CommandText);
            c = c.Subcommand;
        }
        _UnhandledCommandMap.Remove("", out c!);
        CommandLine = c;
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PCL.Core.App.Cli;
using PCL.Core.App.IoC;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;

namespace PCL.Core.App.Essentials;

/// <summary>
/// 命令处理委托
/// </summary>
/// <param name="model">命令模型</param>
/// <param name="isCallback">
/// 指示该委托是否由已注册的回调触发，若是，则代表可能由 RPC
/// 或用户后续操作而非当前进程的命令行参数触发，应注意鉴权问题
/// </param>
public delegate void CommandHandler(CommandLine model, bool isCallback);

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
    private static readonly ConcurrentDictionary<string, CommandHandler> _HandleCallbackMap = [];

    /// <summary>
    /// 未处理的子命令
    /// </summary>
    public static IReadOnlyDictionary<string, CommandLine> UnhandledCommands => _UnhandledCommandMap.AsReadOnly();

    /// <summary>
    /// 处理一个子命令
    /// </summary>
    /// <param name="command">子命令</param>
    /// <param name="handler">用于处理命令的委托，传入 <see langword="null"/> 则触发已注册的处理回调</param>
    /// <param name="registerCallback">指定是否注册该委托为处理回调</param>
    /// <returns>是否执行成功，若子命令不存在或未注册任何处理回调则不成功</returns>
    public static bool TryHandleCommand(
        string command,
        CommandHandler? handler = null,
        bool registerCallback = false)
    {
        var isCallback = false;
        if (handler == null)
        {
            _HandleCallbackMap.TryGetValue(command, out handler);
            if (handler == null) return false;
            isCallback = true;
        }
        else if (registerCallback) _HandleCallbackMap.TryAdd(command, handler);
        lock (_UnhandledCommandMap)
        {
            _UnhandledCommandMap.TryGetValue(command, out var model);
            if (model == null) return false;
            // remove all related commands
            foreach (var x in _UnhandledCommandMap.Keys.Where(x => x.StartsWith(command)).ToList())
                _UnhandledCommandMap.Remove(x);
            // run handler
            try { handler(model, isCallback); }
            catch (Exception ex) { Context.Warn($"Exception thrown while handle command: {command}", ex); }
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
            ("activate", []),
            ("memory", []),
            ("promote", []),
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

    [RegisterRpc("cli")]
    public static RpcResponse OnRpcCommand(string? argument, string? content, bool indent)
    {
        if (content == null) return RpcResponse.Err("Must provide valid JSON model");
        try
        {
            var models = JsonSerializer.Deserialize<Dictionary<string, CommandLine>>(content);
            if (models == null) return RpcResponse.Err("Invalid JSON: empty/null content");
            Task.Run(() =>
            {
                foreach (var (command, model) in models)
                {
                    _UnhandledCommandMap[command] = model;
                    TryHandleCommand(command);
                }
            });
        }
        catch (JsonException ex)
        {
            return RpcResponse.Err($"Invalid JSON: {ex.Message}");
        }
        return RpcResponse.EmptySuccess;
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PCL.Core.App.Cli;
using PCL.Core.App.IoC;
using PCL.Core.IO.Net;
using PCL.Core.Platform;
using PCL.Core.Utils;
using PCL.Core.Utils.OS.DeviceInfo;
using PCL.Core.Utils.Secret;

namespace PCL.Core.App.Essentials;

/// <summary>命令处理委托</summary>
/// <param name="model">命令模型</param>
/// <param name="isCallback">指示该委托是否由已注册的回调触发，若是，则代表可能由 RPC 或用户后续操作而非当前进程的命令行参数触发，应注意鉴权问题</param>
public delegate void CommandHandler(CommandLine model, bool isCallback);

[LifecycleService(LifecycleState.BeforeLoading, Priority = int.MaxValue)]
[LifecycleScope("startup", "基本信息", false)]
public sealed partial class StartupService
{
    private static readonly Dictionary<string, CommandLine> _UnhandledCommandMap = [];
    private static readonly ConcurrentDictionary<string, CommandHandler> _HandleCallbackMap = [];

    /// <summary>解析后的命令行模型实例</summary>
    /// <exception cref="Exception">尚未初始化完成</exception>
    public static CommandLine CommandLine
    {
        get => field ?? throw _GetUninitializedException();
        private set;
    } = null!;

    /// <summary>未处理的子命令</summary>
    public static IReadOnlyDictionary<string, CommandLine> UnhandledCommands =>
        _UnhandledCommandMap.AsReadOnly();

    private static Exception _GetUninitializedException()
    {
        return new InvalidOperationException("Not initialized");
    }

    /// <summary>处理一个子命令</summary>
    /// <param name="command">子命令</param>
    /// <param name="handler">用于处理命令的委托，传入 <see langword="null" /> 则触发已注册的处理回调</param>
    /// <param name="registerCallback">指定是否注册该委托为处理回调</param>
    /// <returns>是否执行成功，若子命令不存在或未注册任何处理回调则不成功</returns>
    public static bool TryHandleCommand(
        string command,
        CommandHandler? handler = null,
        bool registerCallback = false)
    {
        var isCallback = false;
        if (handler is null)
        {
            _HandleCallbackMap.TryGetValue(command, out handler);
            if (handler is null) return false;
            isCallback = true;
        }
        else if (registerCallback)
        {
            _HandleCallbackMap.TryAdd(command, handler);
        }

        lock (_UnhandledCommandMap)
        {
            _UnhandledCommandMap.TryGetValue(command, out var model);
            if (model is null) return false;
            // remove all related commands
            foreach (var x in _UnhandledCommandMap.Keys.Where(x => x.StartsWith(command)).ToList())
                _UnhandledCommandMap.Remove(x);
            // run handler
            try
            {
                handler(model, isCallback);
            }
            catch (Exception ex)
            {
                Context.Warn($"Exception thrown while handle command: {command}", ex);
            }

            return true;
        }
    }


    [LifecycleStart]
    private static void _LogBasicInfo()
    {
        var info = new StringBuilder();
        var (_, osInfo, _, osArchitecture, processArchitecture, _, _, deviceModel, biosVersion, s,
                physicalCoreCount, logicalProcessorCount, totalMemoryBytes, availableMemoryBytes, _,
                timeZoneId, utcOffset, systemUptime, cultureName, uiCultureName, pv6Status,
                isAdministrator, frameworkDescription, _, _, dataVolume, _, _) =
            DeviceInfoFactsCollector.Collect(Paths.Data, includeGpu: false);

        AddLine(
            $"版本: {Basics.Metadata.Version} ({DeviceInfoFormat.FormatArchitecture(processArchitecture)})");
        AddLine($"路径: {Basics.ExecutablePath}");

        if (Basics.CommandLineArguments.Length == 0)
        {
            AddLine("命令行参数: []");
        }
        else
        {
            AddLine("命令行参数:");
            foreach (var argument in Basics.CommandLineArguments)
                AddLine($" - {argument}");
        }

        var cpuName = string.IsNullOrWhiteSpace(s) ? "Unknown" : s;

        AddLine($"系统: {osInfo.Name}");
        AddLineIfPresent("系统版本: ", osInfo.Version);
        AddLineIfPresent("系统构建: ", osInfo.Build);

        var kernel = $"{osInfo.KernelName} {osInfo.KernelVersion}".Trim();
        AddLine($"内核: {kernel}");

        AddLine($"平台: {GetPlatformName()}");
        AddLine(
            $"架构: {DeviceInfoFormat.FormatArchitecture(osArchitecture)} "
            + $"(进程: {DeviceInfoFormat.FormatArchitecture(processArchitecture)})");
        AddLine($"运行时: {frameworkDescription}");

        AddLineIfPresent("设备型号: ", deviceModel);
        AddLineIfPresent("BIOS: ", biosVersion);

        AddLine($"处理器: {cpuName}");

        if (physicalCoreCount is > 0)
            AddLine($"物理核心: {physicalCoreCount.Value}");

        AddLine($"逻辑处理器: {logicalProcessorCount}");

        AddLine(
            $"系统运行时长: {DeviceInfoFormat.FormatUptime(systemUptime)}");
        AddLine($"IPv6 支持: {GetIpv6StatusText(pv6Status)}");

        const ulong miB = 1024UL * 1024UL;

        var usedMemory = totalMemoryBytes > availableMemoryBytes
            ? totalMemoryBytes - availableMemoryBytes
            : 0UL;

        var memoryUsage = totalMemoryBytes == 0UL
            ? 0d
            : usedMemory * 100d / totalMemoryBytes;

        var formattedMemoryUsage = memoryUsage.ToString(
            "N1",
            CultureInfo.InvariantCulture);

        AddLine(
            $"可用内存: {availableMemoryBytes / miB}/{totalMemoryBytes / miB} MiB "
            + $"({formattedMemoryUsage}%)");

        AddLine($"系统语言: {uiCultureName}");
        AddLine($"区域格式: {cultureName}");
        AddLine($"时区: {DeviceInfoFormat.FormatTimeZone(timeZoneId, utcOffset)}");

        AddLineIfPresent(
            "桌面环境: ",
            Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP"));

        AddLineIfPresent("显示会话: ", GetDisplayProtocol());

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var encoding = Encoding.GetEncoding(0);
        AddLine($"默认代码页: {encoding.EncodingName} ({encoding.CodePage})");

        AddLine($"管理员身份: {isAdministrator}");

        AddLine($"数据目录: {Paths.Data}");

        if (dataVolume is not null)
            AddLine($"数据目录可用空间: {DeviceInfoFormat.FormatDiskSpace(dataVolume)}");

        AddLineIfPresent("安装形态: ", Paths.PackageKind);
        AddLine($"识别码: {Identify.LauncherId}");

        Context.Info(info.ToString());
        return;

        void AddLine(string value)
        {
            info.Append('\n').Append(value);
        }

        void AddLineIfPresent(string label, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                AddLine(label + value);
        }

        static string GetIpv6StatusText(NetworkInterfaceUtils.IPv6Status status)
        {
            return status switch
            {
                NetworkInterfaceUtils.IPv6Status.Public => "公网",
                NetworkInterfaceUtils.IPv6Status.RFC4193 => "唯一本地地址",
                NetworkInterfaceUtils.IPv6Status.Unavailable => "不可用",
                _ => "未知"
            };
        }

        static string GetPlatformName()
        {
            var name = ApplicationRuntime.Current.Platform.Kind.ToString();
            return name == "MacOs" ? "macOS" : name;
        }

        static string? GetDisplayProtocol()
        {
            var session = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
            return session switch
            {
                _ when string.Equals(session, "wayland", StringComparison.OrdinalIgnoreCase)
                    => "Wayland",
                _ when string.Equals(session, "x11", StringComparison.OrdinalIgnoreCase)
                    => "X11",
                _ when !string.IsNullOrWhiteSpace(session)
                    => session,
                _ when !string.IsNullOrWhiteSpace(
                        Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"))
                    => "Wayland",
                _ when !string.IsNullOrWhiteSpace(
                        Environment.GetEnvironmentVariable("DISPLAY"))
                    => "X11",
                _ => null
            };
        }
    }

    [LifecycleStart]
    private static void _ParseCommandLineArgs()
    {
        IEnumerable<SubcommandDefinition> subcommands =
        [
            ("update", ["execute", "success", "failed"]),
            ("activate", []),
            ("promote", [])
        ];
        Context.Debug("正在解析命令行参数...");
        var c = CommandLine.Parse(Basics.FullCommandLineArguments, subcommands);
        var prefix = new StringBuilder();
        while (true)
        {
            _UnhandledCommandMap[prefix.ToString()] = c;
            if (c.Subcommand is null) break;
            if (prefix.Length != 0) prefix.Append('.');
            prefix.Append(c.Subcommand.CommandText);
            c = c.Subcommand;
        }

        _UnhandledCommandMap.Remove("", out c!);
        CommandLine = c;
    }

    [RegisterRpc("cli")]
    public static RpcResponse OnRpcCommand(string? argument, string? content, bool indent)
    {
        if (content is null) return RpcResponse.Err("Must provide valid JSON model");
        try
        {
            var models = JsonSerializer.Deserialize<Dictionary<string, CommandLine>>(
                content,
                JsonCompat.SerializerOptions);
            if (models is null) return RpcResponse.Err("Invalid JSON: empty/null content");
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
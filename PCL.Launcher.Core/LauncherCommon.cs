using System.Security.Cryptography;
using System.Text;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;
using PCL.Core.Logging;
using PCL.Core.Platform;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;

namespace PCL;

/// <summary>启动器业务层日志级别，与原 <c>Common.LogLevel</c> 数值保持一致。</summary>
public enum LauncherLogLevel
{
    Normal = 0,
    Developer = 1,
    Debug = 2,
    Hint = 3,
    Msgbox = 4,
    Feedback = 5,
    Critical = 6
}

/// <summary>
/// 业务层通用工具。UI 提示通过 <see cref="UiLogHandler" /> 交给外层， 其余日志直接写入
/// <see cref="LogWrapper" />。
/// </summary>
public static class LauncherCommon
{
    /// <summary>UI 日志处理器；由 WPF 宿主在启动时注入。</summary>
    public delegate void LauncherLogHandler(
        Exception? ex,
        string text,
        LauncherLogLevel level,
        string? title,
        string? userSummary);

    public static LauncherLogHandler? UiLogHandler { get; set; }

    private static int _dispatcherMissingLogged;
    private static bool _modeDebug;

    /// <summary>是否处于调试模式，由宿主同步。</summary>
    public static bool ModeDebug
    {
        get => _modeDebug;
        set
        {
            if (_modeDebug == value) return;
            _modeDebug = value;
            ModeDebugChanged?.Invoke(value);
        }
    }

    /// <summary>调试模式状态变化事件；供启动日志等调试 UI 实时刷新。</summary>
    public static event Action<bool>? ModeDebugChanged;

    public static string VersionStandardCode => Basics.Metadata.Version.StandardVersion;
    public static string UpstreamVersion => Basics.Metadata.Version.UpstreamVersion;
    public static string VersionBaseName => Basics.VersionName;
    public static int VersionCode => Basics.VersionCode;

#if DEBUG
    public const string VersionBranchCode = "100";
#elif DEBUGCI
    public const string VersionBranchCode = "50";
#else
    public const string VersionBranchCode = "0";
#endif

    public static void Log(
        string text,
        LauncherLogLevel level = LauncherLogLevel.Normal,
        string? title = null,
        string? userSummary = null)
    {
        switch (level)
        {
            case >= LauncherLogLevel.Hint when UiLogHandler is not null:
                UiLogHandler(null, text, level, title, userSummary);
                break;

            case >= LauncherLogLevel.Feedback:
                LogWrapper.Error("Launcher", text);
                break;

            case >= LauncherLogLevel.Hint:
                LogWrapper.Warn("Launcher", text);
                break;

            case LauncherLogLevel.Debug or LauncherLogLevel.Developer:
                LogWrapper.Debug("Launcher", text);
                break;

            default:
                LogWrapper.Info("Launcher", text);
                break;
        }
    }

    public static void Log(
        Exception ex,
        string desc,
        LauncherLogLevel level = LauncherLogLevel.Debug,
        string? title = null,
        string? userSummary = null)
    {
        if (ex is ThreadInterruptedException) return;

        switch (level)
        {
            case >= LauncherLogLevel.Hint when UiLogHandler is not null:
                UiLogHandler(ex, desc, level, title, userSummary);
                break;

            case >= LauncherLogLevel.Feedback:
                LogWrapper.Error(ex, "Launcher", desc);
                break;

            case >= LauncherLogLevel.Hint:
                LogWrapper.Warn(ex, "Launcher", desc);
                break;

            default:
                LogWrapper.Debug(ex, "Launcher", desc);
                break;
        }
    }

    private static int _uuid;

    /// <summary>获取一个全程序内不会重复的数字（伪 Uuid）。</summary>
    public static int GetUuid()
    {
        return Interlocked.Increment(ref _uuid);
    }

    public static bool RunInUi()
    {
        var dispatcher = ApplicationRuntime.GetService<IUiDispatcher>();
        if (dispatcher is null)
        {
            _WarnMissingDispatcher();
            return false;
        }

        return dispatcher.CheckAccess();
    }

    /// <summary>确保在 UI 线程中执行代码，不阻断当前线程。</summary>
    public static void RunInUi(Action action, bool forceWaitUntilLoaded = false)
    {
        var dispatcher = ApplicationRuntime.GetService<IUiDispatcher>();
        if (dispatcher is null)
        {
            _WarnMissingDispatcher();
            return;
        }

        if (dispatcher.CheckAccess()) action();
        else dispatcher.Post(action);
    }

    /// <summary>确保在 UI 线程中执行代码并返回结果。</summary>
    public static T? RunInUiWait<T>(Func<T> func)
    {
        var dispatcher = ApplicationRuntime.GetService<IUiDispatcher>();
        if (dispatcher is null)
        {
            _WarnMissingDispatcher();
            return default;
        }

        return dispatcher.CheckAccess() ? func() : dispatcher.Invoke(func);
    }

    /// <summary>确保在 UI 线程中执行代码，必要时阻断当前线程直至执行完毕。</summary>
    public static void RunInUiWait(Action action)
    {
        var dispatcher = ApplicationRuntime.GetService<IUiDispatcher>();
        if (dispatcher is null)
        {
            _WarnMissingDispatcher();
            return;
        }

        if (dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action);
    }

    private static void _WarnMissingDispatcher()
    {
        if (Interlocked.Exchange(ref _dispatcherMissingLogged, 1) != 0) return;
        LogWrapper.Warn("Launcher", "UI 调度器未注册，界面操作已跳过");
    }

    /// <summary>确保在工作线程中执行代码。</summary>
    public static void RunInThread(Action action)
    {
        if (RunInUi()) Task.Run(action);
        else action();
    }

    /// <summary>在新线程中执行代码；线程内异常不会终止进程。</summary>
    public static Thread RunInNewThread(
        Action action,
        string name = "Launcher",
        ThreadPriority priority = ThreadPriority.Normal)
    {
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (ThreadInterruptedException)
            {
                Log($"{name}：线程已中止");
            }
            catch (Exception ex)
            {
                Log(ex, $"{name}：线程执行失败", LauncherLogLevel.Feedback);
            }
        })
        {
            IsBackground = true,
            Name = name,
            Priority = priority
        };
        thread.Start();
        return thread;
    }

    public static int GetHash(string text)
    {
        return BitConverter.ToInt32(SHA256.HashData(Encoding.UTF8.GetBytes(text)), 0)
            & int.MaxValue;
    }

    public static int Dpi { get; set; } = 96;

    public static string VersionBranchName => "Release";

    public enum ProcessReturnValues
    {
        Aborted = -1,
        Success = 0,
        Fail = 1,
        Exception = 2,
        Timeout = 3,
        Cancel = 4,
        TaskDone = 5
    }

    public static bool IsUtf8CodePage()
    {
        return Encoding.Default.CodePage == 65001;
    }

    public static bool IsValidInstanceName(string name)
    {
        return !string.IsNullOrWhiteSpace(name)
            && name.IndexOfAny(['\\', '/', ':', '*', '?', '"', '<', '>', '|']) < 0
            && name.Length <= 64;
    }

    public static string GetTempFolder(string prefix)
    {
        var path = Path.Combine(LauncherPaths.Temp, prefix, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public static string ShortenPath(string longPath, int shortenThreshold = 247)
    {
        return ApplicationRuntime.GetService<IPlatformPathService>()?.ShortenPath(longPath)
            ?? longPath;
    }

    /// <summary>读取物理内存信息；优先使用平台服务，未初始化时回退底层实现。</summary>
    public static SystemMemoryInfo GetMemoryInfo()
    {
        if (ApplicationRuntime.GetService<ISystemFacts>() is { } facts)
            return facts.GetMemoryInfo();

        var (total, available) = KernelInterop.GetPhysicalMemoryBytes();
        return new SystemMemoryInfo(total, available);
    }

    public static string? GetStringFromEnum(Enum enumData)
    {
        return Enum.GetName(enumData.GetType(), enumData);
    }

    /// <summary>将文件大小转化为适合的文本形式，如“1.28 M”。</summary>
    public static string GetString(long fileSize)
    {
        return ByteStream.GetReadableLength(fileSize, provider: Lang.Culture);
    }

    public static string GetHexString(Memory<byte> bytes)
    {
        return Convert.ToHexString(bytes.Span).ToLowerInvariant();
    }

    public static string GetStringMd5(string text)
    {
        return Convert
            .ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(text)))
            .ToLowerInvariant();
    }

    public static string Base64Encode(string text)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
    }

    public static string Base64Decode(string text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? ""
            : Encoding.UTF8.GetString(Convert.FromBase64String(text));
    }

    public static JsonNode GetJson(string data)
    {
        try
        {
            return JsonCompat.ParseNode(data);
        }
        catch (Exception ex)
        {
            var dataText = data ?? "";
            var length = dataText.Length;
            throw new Exception(
                $"格式化 JSON 失败：{(length > 2000
                    ? $"{dataText[..500]}...(全长 {length} 个字符)...{dataText[(length - 500)..]}"
                    : dataText)}",
                ex);
        }
    }
}
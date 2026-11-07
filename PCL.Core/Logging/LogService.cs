using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Essentials;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.Platform;
using PCL.Core.UI;
using PCL.Core.UI.Dialogs;

namespace PCL.Core.Logging;

/// <summary>
///     The single lifecycle log service. It owns the <see cref="Logger" /> instance, bridges both
///     logging
///     entry points (<c>LogWrapper</c> and <c>LifecycleContext</c>) into it, forwards unhandled
///     exceptions to
///     telemetry, and translates <see cref="ActionLevel" /> values into user-visible hints, message
///     boxes and
///     fatal exits. Level filtering is delegated to <see cref="Logger" /> so it can never suppress
///     those
///     side effects.
/// </summary>
[LifecycleService(LifecycleState.Loading, Priority = int.MaxValue)]
public class LogService : ILifecycleLogService
{
    private static LifecycleContext? _context;

    private static Logger? _logger;

    private static bool _wrapperRegistered;

    private static LogLevel? _explicitMinLevel;

    private LogService()
    {
        _context = Lifecycle.GetContext(this);
    }

    private static LifecycleContext Context => _context!;
    public static Logger Logger => _logger!;

    [RegisterConfigEvent]
    public static ConfigEventRegistry OnDebugModeChanged =>
        new(
            Config.Debug.EnabledConfig,
            trigger: ConfigEvent.Init | ConfigEvent.Update,
            handler: e => _ApplyDebugMode(e.NewValue is true));

    public string Identifier => "log";
    public string Name => "日志服务";
    public bool SupportAsync => false;

    /// <summary>
    ///     Creates the <see cref="Logger" /> and subscribes to <see cref="LogWrapper" />. Runs at the
    ///     highest
    ///     priority in the <see cref="LifecycleState.Loading" /> phase so that every later service is
    ///     logged.
    /// </summary>
    public Task StartAsync()
    {
        Context.Trace("正在初始化 Logger 实例");
        var baseConfig =
            new LoggerConfiguration(Path.Combine(Basics.ExecutableDirectory, "PCL", "Log"));
        var initialLevel = _GetInitialMinLevel(baseConfig.MinLogLevel);
        if (_explicitMinLevel is null && ConfigService.IsInitialized && Config.Debug.Enabled)
            initialLevel = LogLevel.Trace;

        var config = baseConfig with
        {
            MinLogLevel = initialLevel,
            ConsoleOutput = _ShouldWriteToConsole()
        };
        _logger = new Logger(config);
        Context.Trace("正在注册日志事件");
        LogWrapper.OnLog += _OnWrapperLog;
        _wrapperRegistered = true;
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Detaches from <see cref="LogWrapper" /> and flushes the pending queue. The logger swallows its
    ///     own
    ///     failures and bounds the wait, so this never blocks process shutdown indefinitely.
    /// </summary>
    public async Task StopAsync()
    {
        if (_wrapperRegistered)
            LogWrapper.OnLog -= _OnWrapperLog;
        if (_logger is not null)
            await _logger.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Entry point for logs raised through a <see cref="LifecycleContext" />.</summary>
    public void OnLog(LifecycleLogItem item)
    {
        _LogAction(item.ToLogEntry(), item.ActionLevel);
    }

    /// <summary>
    ///     读取启动参数和环境变量提供的日志级别。显式指定的级别优先于调试模式设置。
    /// </summary>
    private static LogLevel _GetInitialMinLevel(LogLevel fallback)
    {
        if (Basics.CommandLineArguments.Contains("--verbose"))
        {
            _explicitMinLevel = LogLevel.Trace;
            return LogLevel.Trace;
        }

        var value = Environment.GetEnvironmentVariable("PCL_LOG_LEVEL");
        if (string.IsNullOrWhiteSpace(value)) return fallback;

        value = value.Trim();
        if (value.Equals("warn", StringComparison.OrdinalIgnoreCase))
        {
            _explicitMinLevel = LogLevel.Warning;
            return LogLevel.Warning;
        }

        if (!Enum.TryParse<LogLevel>(value, true, out var level)) return fallback;

        _explicitMinLevel = level;
        return level;
    }

    /// <summary>
    ///     判断当前进程是否有可见的控制台。桌面快捷方式启动时标准输出不可见，此时只写文件。
    /// </summary>
    private static bool _ShouldWriteToConsole()
    {
        if (Debugger.IsAttached) return true;

        var forced = Environment.GetEnvironmentVariable("PCL_LOG_CONSOLE");
        if (forced is not null
            && (forced == "1" || forced.Equals("true", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (Basics.CommandLineArguments.Contains("--console")) return true;

        try
        {
            return !Console.IsOutputRedirected;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    ///     调试模式开启时输出完整的 Debug/Trace 日志；显式指定的日志级别不会被覆盖。
    /// </summary>
    private static void _ApplyDebugMode(bool enabled)
    {
        if (_explicitMinLevel is not null || _logger is null) return;
        _logger.SetMinLevel(enabled ? LogLevel.Trace : _logger.Configuration.MinLogLevel);
    }

    /// <summary>
    ///     Handles one log item: reports the exception to telemetry, enqueues the entry for the file
    ///     writer,
    ///     then performs the action required by <paramref name="actionLevel" />. The three steps are
    ///     independent, which is what keeps <see cref="LoggerConfiguration.MinLogLevel" /> from silencing
    ///     hints, message boxes or fatal exits.
    /// </summary>
    private static void _LogAction(in LogEntry entry, ActionLevel actionLevel)
    {
        if (entry.Exception is not null)
            TelemetryService.ReportException(entry.Exception, entry.Message, entry.Level);

        _logger?.Enqueue(entry);

        var plain = entry.Message;
        switch (actionLevel)
        {
            case <= ActionLevel.NormalLog:
                return;

            // hint
            case ActionLevel.Hint or ActionLevel.HintErr:
                HintWrapper.Show(
                    plain,
                    actionLevel == ActionLevel.Hint ? HintTheme.Info : HintTheme.Error);
                break;

            // message box
            case ActionLevel.MsgBox or ActionLevel.MsgBoxErr:
            {
                var isWarn = actionLevel == ActionLevel.MsgBoxErr;

                var caption = entry.Exception is null
                    ? null
                    : Lang.Text("SystemDialog.Error.Unexpected.Title");

                var message = _ComposeUserError(plain, entry.Exception);

                if (actionLevel == ActionLevel.MsgBoxErr)
                    message = Lang.Text(
                        "SystemDialog.Error.Message.WithLogExportGuidance",
                        message);

                DialogService.Instance.Show(
                    new TextDialogRequest
                    {
                        Title = caption ?? Lang.Text("SystemDialog.Error.Unexpected.Title"),
                        Content = message,
                        IsWarn = isWarn,
                        ForceWait = false
                    });

                break;
            }

            // fatal message box
            case ActionLevel.MsgBoxFatal:
            {
                var message = Lang.Text(
                    "SystemDialog.Fatal.Message.WithFeedbackGuidance",
                    _ComposeUserError(plain, entry.Exception));

                PlatformServices.DialogService?.ShowFatal(
                    message,
                    Lang.Text("SystemDialog.Fatal.Title"));

                break;
            }
        }
    }

    /// <summary>
    ///     Builds the user-facing error text: the plain message, with the exception details appended when
    ///     one
    ///     is present. Falls back to a localized generic message for an empty message.
    /// </summary>
    private static string _ComposeUserError(string plain, Exception? exception)
    {
        var summary = string.IsNullOrWhiteSpace(plain)
            ? Lang.Text("SystemDialog.Error.Unexpected.Message")
            : plain;

        return exception is null
            ? summary
            : ExceptionDetails.Compose(summary, exception);
    }

    /// <summary>
    ///     Converts a <see cref="LogWrapper" /> callback into a <see cref="LogEntry" />. Only field reads
    ///     happen here - the module prefix, thread label and timestamp are all produced by the consumer.
    /// </summary>
    private static void _OnWrapperLog(
        LogLevel level,
        string msg,
        string? module,
        Exception? ex)
    {
        _LogAction(
            new LogEntry
            {
                Level = level,
                Message = msg,
                Time = DateTime.Now,
                SourceName = module,
                ThreadName = Thread.CurrentThread.Name,
                ThreadId = Environment.CurrentManagedThreadId,
                TaskId = Task.CurrentId ?? 0,
                Exception = ex
            },
            level.DefaultActionLevel());
    }
}
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using PCL.Core.App;
using PCL.Core.App.Essentials;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL.Core.Logging;

/// <summary>
/// The single lifecycle log service. It owns the <see cref="Logger" /> instance, bridges both logging
/// entry points (<c>LogWrapper</c> and <c>LifecycleContext</c>) into it, forwards unhandled exceptions to
/// telemetry, and translates <see cref="ActionLevel" /> values into user-visible hints, message boxes and
/// fatal exits. Level filtering is delegated to <see cref="Logger" /> so it can never suppress those
/// side effects.
/// </summary>
[LifecycleService(LifecycleState.Loading, Priority = int.MaxValue)]
public class LogService : ILifecycleLogService
{
    private static LifecycleContext? _context;

    private static Logger? _logger;

    private static bool _wrapperRegistered;

    private LogService()
    {
        _context = Lifecycle.GetContext(this);
    }

    private static LifecycleContext Context => _context!;
    public static Logger Logger => _logger!;
    public string Identifier => "log";
    public string Name => "日志服务";
    public bool SupportAsync => false;

    /// <summary>
    /// Creates the <see cref="Logger" /> and subscribes to <see cref="LogWrapper" />. Runs at the highest
    /// priority in the <see cref="LifecycleState.Loading" /> phase so that every later service is logged.
    /// </summary>
    public Task StartAsync()
    {
        Context.Trace("正在初始化 Logger 实例");
        var config = new LoggerConfiguration(Path.Combine(Basics.ExecutableDirectory, "PCL", "Log"));
        _logger = new Logger(config);
        Context.Trace("正在注册日志事件");
        LogWrapper.OnLog += _OnWrapperLog;
        _wrapperRegistered = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Detaches from <see cref="LogWrapper" /> and flushes the pending queue. The logger swallows its own
    /// failures and bounds the wait, so this never blocks process shutdown indefinitely.
    /// </summary>
    public async Task StopAsync()
    {
        if (_wrapperRegistered)
            LogWrapper.OnLog -= _OnWrapperLog;
        if (_logger is not null)
            await _logger.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Entry point for logs raised through a <see cref="LifecycleContext" />.</summary>
    public void OnLog(LifecycleLogItem item) => _LogAction(item.ToLogEntry(), item.ActionLevel);

    /// <summary>
    /// Handles one log item: reports the exception to telemetry, enqueues the entry for the file writer,
    /// then performs the action required by <paramref name="actionLevel" />. The three steps are
    /// independent, which is what keeps <see cref="LoggerConfiguration.MinLogLevel" /> from silencing
    /// hints, message boxes or fatal exits.
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
                var theme = actionLevel == ActionLevel.MsgBoxErr
                    ? MsgBoxTheme.Error
                    : MsgBoxTheme.Info;

                var caption = entry.Exception is null
                    ? null
                    : Lang.Text("SystemDialog.Error.Unexpected.Title");

                var message = _ComposeUserError(plain, entry.Exception);

                if (actionLevel == ActionLevel.MsgBoxErr)
                    message = Lang.Text("SystemDialog.Error.Message.WithLogExportGuidance", message);

                MsgBoxWrapper.Show(message, caption, theme, false);
                break;
            }

            // fatal message box
            case ActionLevel.MsgBoxFatal:
            {
                var message = Lang.Text(
                    "SystemDialog.Fatal.Message.WithFeedbackGuidance",
                    _ComposeUserError(plain, entry.Exception));

                _ShowFatalMessageBox(message);

                break;
            }
        }
    }

    /// <summary>连续致命错误的弹窗节流窗口。</summary>
    private const long FatalDialogThrottleMs = 5000;

    private static long _lastFatalDialogStamp;

    /// <summary>等待致命错误弹窗真正显示的最长时间。</summary>
    private static readonly TimeSpan FatalDialogWaitTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    /// 弹出致命错误提示，并按 <see cref="FatalDialogThrottleMs"/> 节流。<br/>
    /// 这里不能直接调用 <see cref="MessageBox.Show(string, string, MessageBoxButton, MessageBoxImage)"/>：
    /// 该方法会同步阻塞调用线程，而 <see cref="ActionLevel.MsgBoxFatal"/> 往往来自日志消费线程，
    /// 阻塞该线程会让后续日志全部积压；启动期级联失败时还会叠出十余个模态框。<br/>
    /// 但也不能只把弹窗丢进队列就返回：<see cref="LifecycleContext"/> 在日志回调返回后会立即执行
    /// <c>_FatalExit()</c>，进程可能在 Dispatcher 执行到该回调之前就已退出，
    /// 使启动期的致命错误（如配置文件损坏）退化成"静默退出"。因此必须等弹窗真正显示后再返回。
    /// </summary>
    private static void _ShowFatalMessageBox(string message)
    {
        var stamp = Environment.TickCount64;
        var lastStamp = Interlocked.Read(ref _lastFatalDialogStamp);
        if (stamp - lastStamp < FatalDialogThrottleMs) return;
        if (Interlocked.CompareExchange(ref _lastFatalDialogStamp, stamp, lastStamp) != lastStamp) return;

        var title = Lang.Text("SystemDialog.Fatal.Title");
        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher is null || dispatcher.CheckAccess())
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        try
        {
            var operation = dispatcher.InvokeAsync(
                () => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error),
                DispatcherPriority.Send);
            // 等待用户关闭弹窗（或超时），确保调用方随后的进程退出不会抢在提示之前发生
            var status = operation.Wait(FatalDialogWaitTimeout);
            if (status != DispatcherOperationStatus.Completed)
                Trace.WriteLine("[LogService] 致命错误弹窗未在超时前关闭，继续执行退出流程");
        }
        catch (Exception ex)
        {
            // Dispatcher 已关闭或无法投递时退化为直接调用，至少让提示出现在日志中
            Trace.WriteLine($"[LogService] 致命错误弹窗显示失败：{ex.Message}");
        }
    }

    /// <summary>
    /// Builds the user-facing error text: the plain message, with the exception details appended when one
    /// is present. Falls back to a localized generic message for an empty message.
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
    /// Converts a <see cref="LogWrapper" /> callback into a <see cref="LogEntry" />. Only field reads
    /// happen here - the module prefix, thread label and timestamp are all produced by the consumer.
    /// </summary>
    private static void _OnWrapperLog(
        LogLevel level,
        string msg,
        string? module,
        Exception? ex)
    {
        _LogAction(new LogEntry
        {
            Level = level,
            Message = msg,
            Time = DateTime.Now,
            SourceName = module,
            ThreadName = Thread.CurrentThread.Name,
            ThreadId = Environment.CurrentManagedThreadId,
            TaskId = Task.CurrentId ?? 0,
            Exception = ex
        }, level.DefaultActionLevel());
    }
}

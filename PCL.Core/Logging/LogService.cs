using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using PCL.Core.App;
using PCL.Core.App.IoC;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL.Core.Logging;

[LifecycleService(LifecycleState.Loading, Priority = int.MaxValue)]
public class LogService : ILifecycleLogService
{
    public string Identifier => "log";
    public string Name => "日志服务";
    public bool SupportAsync => false;

    private static LifecycleContext? _context;
    private static LifecycleContext Context => _context!;
    private LogService() { _context = Lifecycle.GetContext(this); }

    private static Logger? _logger;
    public static Logger Logger => _logger!;

    private static bool _wrapperRegistered = false;

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

    public async Task StopAsync()
    {
        if (_wrapperRegistered) LogWrapper.OnLog -= _OnWrapperLog;
        if (_logger is not null)
            await _logger.DisposeAsync().ConfigureAwait(false);
    }

    private static void _LogAction(LogLevel level, ActionLevel actionLevel, string formatted, string plain, Exception? ex)
    {
        // log
#if !TRACE
        if (actionLevel != ActionLevel.TraceLog)
#endif
        Logger.Log(formatted);

        if (actionLevel <= ActionLevel.NormalLog) return;

        // hint
        if (actionLevel is ActionLevel.Hint or ActionLevel.HintErr)
        {
            HintWrapper.Show(plain, (actionLevel == ActionLevel.Hint) ? HintTheme.Info : HintTheme.Error);
        }

        // message box
        else if (actionLevel is ActionLevel.MsgBox or ActionLevel.MsgBoxErr)
        {
            var caption = (ex is null) ? null : Lang.Text("Core.Log.ExceptionTitle");
            var theme = (actionLevel == ActionLevel.MsgBoxErr) ? MsgBoxTheme.Info : MsgBoxTheme.Error;
            var message = plain;
            if (ex is not null)
                message += "\n\n" + Lang.Text("Core.Log.Details", ex);
            if (actionLevel == ActionLevel.MsgBoxErr)
                message += "\n\n" + Lang.Text("Core.Log.Help");
            MsgBoxWrapper.Show(message, caption, theme, false);
        }

        // fatal message box
        else if (actionLevel == ActionLevel.MsgBoxFatal)
        {
            var message = plain;
            if (ex is not null) message += "\n\n" + Lang.Text("Core.Log.RelatedException", ex);
            message += "\n\n" + Lang.Text("Core.Log.FatalFeedback");
            MessageBox.Show(message, Lang.Text("Core.Log.FatalTitle"), MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void _OnWrapperLog(LogLevel level, string msg, string? module, Exception? ex)
    {
        var thread = Thread.CurrentThread.Name ?? $"#{Environment.CurrentManagedThreadId}";
        if (module is not null) module = $"[{module}] ";
        var result = $"[{DateTime.Now:HH:mm:ss.fff}] [{level.PrintName()}] [{thread}] {module}{msg}";
        _LogAction(level, level.DefaultActionLevel(), (ex is null) ? result : $"{result}\n{ex}", msg, ex);
    }

    public void OnLog(LifecycleLogItem item) =>
        _LogAction(item.Level, item.ActionLevel, item.ComposeMessage(), item.Message, item.Exception);
}

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using PCL.Core.App;
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

    public Task StopAsync()
    {
        if (_wrapperRegistered) LogWrapper.OnLog -= _OnWrapperLog;
        _logger?.Dispose();
        return Task.CompletedTask;
    }

    private static void _LogAction(ActionLevel level, string formatted, string plain, Exception? ex)
    {
        // log
#if !TRACE
        if (level != ActionLevel.TraceLog)
#endif
        Logger.Log(formatted);

        if (level <= ActionLevel.NormalLog) return;

        // hint
        if (level is ActionLevel.Hint or ActionLevel.HintErr)
        {
            HintWrapper.Show(plain, (level == ActionLevel.Hint) ? HintTheme.Info : HintTheme.Error);
        }

        // message box
        else if (level is ActionLevel.MsgBox or ActionLevel.MsgBoxErr)
        {
            var caption = (ex == null) ? "提示" : "出现异常";
            var theme = (level == ActionLevel.MsgBoxErr) ? MsgBoxTheme.Info : MsgBoxTheme.Error;
            var message = plain;
            if (ex != null)
                message += $"\n\n详细信息:\n{ex}";
            if (level == ActionLevel.MsgBoxErr)
                message += "\n\n若要寻求他人帮助，请勿关闭启动器并立即导出日志 (更多 → 日志浏览 → 导出日志)，" +
                           "然后发送导出的日志压缩包，只发送这个窗口的截图通常无助于解决问题。";
            MsgBoxWrapper.Show(message, caption, theme, false);
        }

        // fatal message box
        else if (level == ActionLevel.MsgBoxFatal)
        {
            var message = plain;
            if (ex != null) message += $"\n\n相关异常信息:\n{ex}";
            message += "\n\n如果你认为这是启动器的问题，请提交反馈，否则它可能永远都不会被解决！\n导出日志: 更多 → 日志浏览 → 导出全部日志";
            MessageBox.Show(message, "锟斤拷烫烫烫", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void _OnWrapperLog(LogLevel level, string msg, string? module, Exception? ex)
    {
        var thread = Thread.CurrentThread.Name ?? $"#{Environment.CurrentManagedThreadId}";
        if (module != null) module = $"[{module}] ";
        var result = $"[{DateTime.Now:HH:mm:ss.fff}] [{level.PrintName()}] [{thread}] {module}{msg}";
        _LogAction(level.DefaultActionLevel(), (ex == null) ? result : $"{result}\n{ex}", msg, ex);
    }

    public void OnLog(LifecycleLogItem item) =>
        _LogAction(item.ActionLevel, item.ComposeMessage(), item.Message, item.Exception);
}

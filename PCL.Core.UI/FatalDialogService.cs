using System.Text;
using PCL.Core.App.IoC;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>
///     优先使用原生窗口的致命错误对话框实现。调用会阻塞到用户关闭对话框，返回后生命周期会终止进程。
/// </summary>
public sealed class FatalDialogService : IFatalDialogService
{
    private readonly object _gate = new();
    private readonly Func<string, string, bool> _showNative;
    private bool _dismissed;

    private bool _showing;

    public FatalDialogService() : this(NativeFatalDialog.Show)
    {
    }

    internal FatalDialogService(Func<string, string, bool> showNative)
    {
        _showNative = showNative ?? throw new ArgumentNullException(nameof(showNative));
    }

    public void ShowFatal(string message, string caption)
    {
        if (Lifecycle.HasShutdownStarted) return;

        lock (_gate)
        {
            if (_dismissed) return;

            if (_showing)
            {
                while (!_dismissed) Monitor.Wait(_gate);
                return;
            }

            _showing = true;
        }

        try
        {
            if (!_showNative(message, caption))
                _WriteFallbackReport(message, caption);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{caption}] {message}{Environment.NewLine}{ex}");
        }
        finally
        {
            lock (_gate)
            {
                _showing = false;
                _dismissed = true;
                Monitor.PulseAll(_gate);
            }
        }
    }

    private static void _WriteFallbackReport(string message, string caption)
    {
        try
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                $"PCL-CE-Crash-{DateTime.Now:yyyyMMdd-HHmmss}-{Environment.ProcessId}.txt");

            File.WriteAllText(
                path,
                $"{caption}{Environment.NewLine}{Environment.NewLine}{message}",
                new UTF8Encoding(false));

            Console.Error.WriteLine($"[FatalDialog] 无法显示原生对话框，崩溃报告已写入：{path}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[{caption}] {message}{Environment.NewLine}{ex}");
        }
    }
}
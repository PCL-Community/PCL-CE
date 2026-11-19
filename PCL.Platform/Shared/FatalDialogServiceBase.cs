using System.Diagnostics;
using System.Text;
using PCL.Core.App.IoC;
using PCL.Core.Platform;

namespace PCL.Platform;

public abstract class FatalDialogServiceBase : IFatalDialogService
{
    private const int MaxInlineTextLength = 8192;
    private readonly object _gate = new();
    private bool _dismissed;
    private bool _showing;

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
            if (!ShowFatalCore(message, caption))
                WriteFallbackReport(message, caption);
        }
        catch (Exception ex)
        {
            WriteFallbackReport(message, caption);
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

    protected abstract bool ShowFatalCore(string message, string caption);

    private static void WriteFallbackReport(string message, string caption)
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

    internal static bool NeedsTextFile(string message)
    {
        return message.Length > MaxInlineTextLength;
    }

    /// <summary>将超长消息压缩到可直接放进命令行/环境变量的长度，同时保留首尾内容。</summary>
    internal static string TruncateForInlineDisplay(string message)
    {
        if (message.Length <= MaxInlineTextLength) return message;

        const int tailLength = 1024;
        const int headLength = MaxInlineTextLength - tailLength - 8;
        return message[..headLength]
            + Environment.NewLine
            + "…"
            + Environment.NewLine
            + message[^tailLength..];
    }

    internal static bool WithTempFile(string message, Func<string, bool> action)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            $"pcl-ce-crash-{Environment.ProcessId}-{Guid.NewGuid():N}.txt");

        try
        {
            File.WriteAllText(path, message, new UTF8Encoding(false));
            return action(path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FatalDialog] 写入临时崩溃文本失败：{ex}");
            return false;
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }

    internal static bool RunAndWait(
        string fileName,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string>? environment = null)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in arguments)
                startInfo.ArgumentList.Add(argument);

            if (environment is not null)
                foreach (var pair in environment)
                    startInfo.Environment[pair.Key] = pair.Value;

            using var process = Process.Start(startInfo);
            if (process is null) return false;

            process.WaitForExit();
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
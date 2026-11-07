using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PCL.Core.UI;

/// <summary>
///     原生致命错误对话框。崩溃后 Avalonia 的消息循环可能已经不可用，因此这里只使用
///     操作系统自带的对话框或独立的对话框进程，不创建任何 Avalonia 窗口。
/// </summary>
internal static class NativeFatalDialog
{
    private const int MaxInlineTextLength = 8192;

    private const uint MbIconError = 0x00000010u;
    private const uint MbSetForeground = 0x00010000u;
    private const uint MbTopmost = 0x00040000u;

    public static bool Show(string message, string caption)
    {
        message = string.IsNullOrWhiteSpace(message) ? caption : message;
        caption = string.IsNullOrWhiteSpace(caption) ? "PCL CE" : caption;

        try
        {
            return (OperatingSystem.IsWindows() && _ShowWindows(message, caption))
                || (OperatingSystem.IsMacOS() && _ShowMac(message, caption))
                || (OperatingSystem.IsLinux() && _ShowLinux(message, caption));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FatalDialog] 显示原生致命错误对话框失败：{ex}");
            return false;
        }
    }

    private static bool _ShowWindows(string message, string caption)
    {
        return MessageBoxW(
                IntPtr.Zero,
                message,
                caption,
                MbIconError | MbSetForeground | MbTopmost)
            != 0;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int MessageBoxW(
        IntPtr hWnd,
        string text,
        string caption,
        uint type);

    private static bool _ShowMac(string message, string caption)
    {
        const string script =
            "display alert (system attribute \"PCL_FATAL_TITLE\") "
            + "message (system attribute \"PCL_FATAL_MESSAGE\") "
            + "as critical buttons {\"OK\"} default button \"OK\"";

        return _RunAndWait(
            "osascript",
            ["-e", script],
            new Dictionary<string, string>
            {
                ["PCL_FATAL_TITLE"] = caption,
                ["PCL_FATAL_MESSAGE"] = message
            });
    }

    private static bool _ShowLinux(string message, string caption)
    {
        return (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
                || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            && (_TryShowKDialog(message, caption)
                || _TryShowZenity(message, caption)
                || _TryShowYad(message, caption)
                || _TryShowXMessage(message, caption));
    }

    private static bool _TryShowZenity(string message, string caption)
    {
        return !_NeedsTextFile(message)
            ? _RunAndWait(
                "zenity",
                [
                    "--error",
                    "--no-markup",
                    "--modal",
                    "--icon=error",
                    $"--title={caption}",
                    $"--text={message}",
                    "--width=680"
                ])
            : _WithTempFile(
                message,
                path => _RunAndWait(
                    "zenity",
                    [
                        "--text-info",
                        "--modal",
                        $"--title={caption}",
                        $"--filename={path}",
                        "--width=760",
                        "--height=560"
                    ]));
    }

    private static bool _TryShowKDialog(string message, string caption)
    {
        return !_NeedsTextFile(message)
            ? _RunAndWait(
                "kdialog",
                [
                    "--title",
                    caption,
                    "--icon",
                    "dialog-error",
                    "--error",
                    message
                ])
            : _WithTempFile(
                message,
                path => _RunAndWait(
                    "kdialog",
                    [
                        "--title",
                        caption,
                        "--icon",
                        "dialog-error",
                        "--textbox",
                        path,
                        "760",
                        "560"
                    ]));
    }

    private static bool _TryShowYad(string message, string caption)
    {
        return !_NeedsTextFile(message)
            ? _RunAndWait(
                "yad",
                [
                    "--error",
                    $"--title={caption}",
                    $"--text={message}",
                    "--image=dialog-error",
                    "--button=OK:0",
                    "--width=680",
                    "--wrap"
                ])
            : _WithTempFile(
                message,
                path => _RunAndWait(
                    "yad",
                    [
                        "--text-info",
                        $"--title={caption}",
                        $"--filename={path}",
                        "--button=OK:0",
                        "--width=760",
                        "--height=560"
                    ]));
    }

    private static bool _TryShowXMessage(string message, string caption)
    {
        return _WithTempFile(
            message,
            path => _RunAndWait(
                "xmessage",
                [
                    "-center",
                    "-title",
                    caption,
                    "-file",
                    path,
                    "-buttons",
                    "OK:0",
                    "-default",
                    "OK"
                ]));
    }

    private static bool _NeedsTextFile(string message)
    {
        return message.Length > MaxInlineTextLength;
    }

    private static bool _WithTempFile(string message, Func<string, bool> action)
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

    private static bool _RunAndWait(
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
            // 命令不存在或当前环境无法启动 GUI 时继续尝试下一种实现。
            return false;
        }
    }
}
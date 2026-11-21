namespace PCL.Platform;

/// <summary>Linux 原生致命错误对话框：依次尝试 kdialog、zenity、yad、xmessage。</summary>
public sealed class LinuxFatalDialogService : FatalDialogServiceBase
{
    protected override bool ShowFatalCore(string message, string caption)
    {
        return (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
                || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            && (_TryShowKDialog(message, caption)
                || _TryShowZenity(message, caption)
                || _TryShowYad(message, caption)
                || _TryShowXMessage(message, caption));
    }

    private static bool _TryShowKDialog(string message, string caption)
    {
        return !NeedsTextFile(message)
            ? RunAndWait(
                "kdialog",
                [
                    "--title",
                    caption,
                    "--icon",
                    "dialog-error",
                    "--error",
                    message
                ])
            : WithTempFile(
                message,
                path => RunAndWait(
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

    private static bool _TryShowZenity(string message, string caption)
    {
        return !NeedsTextFile(message)
            ? RunAndWait(
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
            : WithTempFile(
                message,
                path => RunAndWait(
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


    private static bool _TryShowYad(string message, string caption)
    {
        return !NeedsTextFile(message)
            ? RunAndWait(
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
            : WithTempFile(
                message,
                path => RunAndWait(
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
        return WithTempFile(
            message,
            path => RunAndWait(
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
}
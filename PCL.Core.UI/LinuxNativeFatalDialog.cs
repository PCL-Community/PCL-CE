namespace PCL.Core.UI;

/// <summary>Linux 原生致命错误对话框：依次尝试 kdialog、zenity、yad、xmessage。</summary>
internal static class LinuxNativeFatalDialog
{
    public static bool Show(string message, string caption)
    {
        return (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
                || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
            && (TryShowKDialog(message, caption)
                || TryShowZenity(message, caption)
                || TryShowYad(message, caption)
                || TryShowXMessage(message, caption));
    }

    private static bool TryShowZenity(string message, string caption)
    {
        return !NativeFatalDialogCommon.NeedsTextFile(message)
            ? NativeFatalDialogCommon.RunAndWait(
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
            : NativeFatalDialogCommon.WithTempFile(
                message,
                path => NativeFatalDialogCommon.RunAndWait(
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

    private static bool TryShowKDialog(string message, string caption)
    {
        return !NativeFatalDialogCommon.NeedsTextFile(message)
            ? NativeFatalDialogCommon.RunAndWait(
                "kdialog",
                [
                    "--title",
                    caption,
                    "--icon",
                    "dialog-error",
                    "--error",
                    message
                ])
            : NativeFatalDialogCommon.WithTempFile(
                message,
                path => NativeFatalDialogCommon.RunAndWait(
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

    private static bool TryShowYad(string message, string caption)
    {
        return !NativeFatalDialogCommon.NeedsTextFile(message)
            ? NativeFatalDialogCommon.RunAndWait(
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
            : NativeFatalDialogCommon.WithTempFile(
                message,
                path => NativeFatalDialogCommon.RunAndWait(
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

    private static bool TryShowXMessage(string message, string caption)
    {
        return NativeFatalDialogCommon.WithTempFile(
            message,
            path => NativeFatalDialogCommon.RunAndWait(
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
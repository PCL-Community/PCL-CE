namespace PCL.Platform;

/// <summary>macOS 原生致命错误对话框：使用 osascript。</summary>
public sealed class MacOsFatalDialogService : FatalDialogServiceBase
{
    protected override bool ShowFatalCore(string message, string caption)
    {
        const string script =
            "display alert (system attribute \"PCL_FATAL_TITLE\") "
            + "message (system attribute \"PCL_FATAL_MESSAGE\") "
            + "as critical buttons {\"OK\"} default button \"OK\"";

        var displayMessage = TruncateForInlineDisplay(message);

        return RunAndWait(
            "osascript",
            ["-e", script],
            new Dictionary<string, string>
            {
                ["PCL_FATAL_TITLE"] = caption,
                ["PCL_FATAL_MESSAGE"] = displayMessage
            });
    }
}
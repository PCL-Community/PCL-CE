namespace PCL.Core.UI;

/// <summary>macOS 原生致命错误对话框。</summary>
internal static class MacOsNativeFatalDialog
{
    public static bool Show(string message, string caption)
    {
        const string script =
            "display alert (system attribute \"PCL_FATAL_TITLE\") "
            + "message (system attribute \"PCL_FATAL_MESSAGE\") "
            + "as critical buttons {\"OK\"} default button \"OK\"";

        return NativeFatalDialogCommon.RunAndWait(
            "osascript",
            ["-e", script],
            new Dictionary<string, string>
            {
                ["PCL_FATAL_TITLE"] = caption,
                ["PCL_FATAL_MESSAGE"] = message
            });
    }
}
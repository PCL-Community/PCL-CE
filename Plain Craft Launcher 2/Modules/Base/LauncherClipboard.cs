using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace PCL;

/// <summary>
/// Owns clipboard operations and WPF/STA failure handling.
/// </summary>
public static class LauncherClipboard
{
    public static void ClipboardSet(string text, bool showSuccessHint = true) => Set(text, showSuccessHint);

    /// <summary>
     /// Sets clipboard text on the UI thread and retries transient STA/clipboard contention failures.
     /// </summary>
    public static void Set(string text, bool showSuccessHint = true)
    {
        _ = Task.Run(() =>
        {
            var success = false;

            for (var attempt = 0; attempt <= 5; attempt++)
                try
                {
                    LauncherDispatcher.RunInUiWait(() => Clipboard.SetText(text));
                    success = true;
                    break;
                }
                catch (Exception) when (attempt < 5)
                {
                    Thread.Sleep(20);
                }
                catch (Exception finalEx)
                {
                    LauncherLogger.Log(finalEx, "剪贴板被占用，文本复制失败", LauncherLogger.LogLevel.Hint);
                }

            if (success && showSuccessHint)
                LauncherDispatcher.RunInUi(() => LauncherFeedback.ShowHint("已成功复制！", HintKind.Finish));
        });
    }
}

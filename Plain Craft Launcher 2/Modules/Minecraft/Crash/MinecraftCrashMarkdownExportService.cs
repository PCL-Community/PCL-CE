using System.IO;
using System.Text;
using System.Windows;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL;

public static class MinecraftCrashMarkdownExportService
{
    public static void ExportCurrent()
    {
        var markdown = MinecraftCrashSessionStore.Current.Markdown;
        string? path = null;
        ModBase.RunInUiWait(() =>
        {
            path = SystemDialogs.SelectSaveFile(
                MinecraftCrashUi.Text("Crash.Export.Markdown.Title"),
                markdown.FileName,
                MinecraftCrashUi.Text("Crash.Export.Markdown.Filter"));
        });
        if (string.IsNullOrWhiteSpace(path)) return;
        File.WriteAllText(path, markdown.Content, Encoding.UTF8);
        ModMain.Hint(MinecraftCrashUi.Text("Crash.Export.Markdown.Success"), ModMain.HintType.Finish);
        ModBase.OpenExplorer(path);
    }

    public static void CopyCurrentSummary()
    {
        Clipboard.SetText(MinecraftCrashSessionStore.Current.Markdown.Content);
        ModMain.Hint(MinecraftCrashUi.Text("Crash.Export.Markdown.Copied"), ModMain.HintType.Finish);
    }
}
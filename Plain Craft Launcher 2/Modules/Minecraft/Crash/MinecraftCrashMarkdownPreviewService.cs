using PCL.Core.App.Localization;

namespace PCL;

public static class MinecraftCrashMarkdownPreviewService
{
    public static void PreviewCurrent()
    {
        var session = MinecraftCrashSessionStore.TryGetCurrent();
        if (session is null)
        {
            ModMain.Hint(MinecraftCrashUi.Text("Crash.Page.NoSession"), ModMain.HintType.Critical);
            return;
        }

        ModMain.MyMsgBoxMarkdown(session.Markdown.Content, MinecraftCrashUi.Text("Crash.Markdown.Preview.Title"));
    }
}
using System.Text.Json.Nodes;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft;
using PCL.Core.Utils;
using PCL.Navigation;

namespace PCL.UI.Bridges;

/// <summary>下载过程的客户端更新提示：消息框 + 跳转下载页 + 打开更新日志。</summary>
public sealed class DownloadInteractionBridge : IDownloadUserInteraction
{
    public void ShowClientUpdateHint(string versionName, JsonObject json)
    {
        try
        {
            JsonNode? version = null;
            foreach (var token in json["versions"]?.AsArray() ?? [])
                if (token?["id"] is not null && token["id"]?.ToString() == versionName)
                {
                    version = token;
                    break;
                }

            if (version is null) return;
            var time = version["releaseTime"]?.ToObject<DateTime>() ?? DateTime.Now;
            var elapsed = DateTime.Now - time;
            var message =
                $"{Lang.Text("Minecraft.Update.NewVersion", versionName)}\r\n"
                + (elapsed.TotalDays > 1d
                    ? Lang.Text("Minecraft.Update.UpdateTime") + Lang.Date(time)
                    : Lang.Text("Minecraft.Update.UpdatedAt")
                    + Lang.TimeSpan(time - DateTime.Now));
            var result = UiInteraction.MessageBlockingOnWorkerThread(
                message,
                Lang.Text("Minecraft.Update.Title"),
                Lang.Text("Common.Action.Confirm"),
                Lang.Text("Common.Action.Download"),
                (DateTime.Now - time).TotalHours > 3d
                    ? Lang.Text("Common.Action.UpdateLog")
                    : "",
                button3Action: () => OpenUpdateLog(version));
            if (result == 2)
            {
                var parameter = version as JsonObject
                    ?? (PageParameter)versionName;
                PageRouter.Navigate(
                    PageIds.Download,
                    SubPageIds.DownloadInstall,
                    parameter);
            }
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Minecraft.Error.UpdateNotify", versionName),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Error.UpdateNotify", versionName));
        }
    }

    private static void OpenUpdateLog(JsonNode version)
    {
        var wikiName = McFormatter.GetWikiUrlSuffix(version["id"]?.ToString() ?? "");
        var wikiUrl = $"{McFormatter.GetWikiBaseUrl()}/{wikiName.TrimStart('/')}";
        UiInteraction.OpenWebsite(wikiUrl);
    }
}
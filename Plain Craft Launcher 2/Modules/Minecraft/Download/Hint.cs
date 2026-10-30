using PCL.Core.App.Localization;
using PCL.Core.Utils;

namespace PCL;

public static partial class GameDownload
{
    /// <summary>
    ///     发送 Minecraft 更新提示。
    /// </summary>
    public static void McDownloadClientUpdateHint(string versionName, JsonObject json)
    {
        try
        {
            JsonNode? version = null;
            foreach (var token in json["versions"]?.AsArray() ?? [])
                if (token?["id"] is not null
                    && token["id"]?.ToString() == versionName)
                {
                    version = token;
                    break;
                }

            if (version is null)
                return;
            var time = version["releaseTime"].ToObject<DateTime>();
            var msgBoxText =
                $"{Lang.Text("Minecraft.Update.NewVersion", versionName)}\r\n{((DateTime.Now - time).TotalDays > 1d
                    ? Lang.Text("Minecraft.Update.UpdateTime") + Lang.Date(time)
                    : Lang.Text("Minecraft.Update.UpdatedAt") + Lang.TimeSpan(time - DateTime.Now))}";
            var msgResult = Launcher.MyMsgBox(
                msgBoxText,
                Lang.Text("Minecraft.Update.Title"),
                Lang.Text("Common.Action.Confirm"),
                Lang.Text("Common.Action.Download"),
                (DateTime.Now - time).TotalHours > 3d
                    ? Lang.Text("Common.Action.UpdateLog")
                    : "",
                button3Action: () => DownloadUi.McUpdateLogShow(version));
            if (msgResult == 2)
                Common.RunInUi(() =>
                {
                    PageDownloadInstall.mcVersionWaitingForSelect = versionName;
                    Launcher.frmMain!.PageChange(
                        FormMain.PageType.Download,
                        FormMain.PageSubType.DownloadInstall);
                });
        }

        catch (Exception ex)
        {
            Common.Log(
                ex,
                Lang.Text("Minecraft.Error.UpdateNotify", versionName ?? "Nothing"),
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Minecraft.Error.UpdateNotify", versionName ?? "Nothing"));
        }
    }
}

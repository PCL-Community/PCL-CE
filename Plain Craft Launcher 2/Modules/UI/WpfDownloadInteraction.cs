using PCL.Core.App.Localization;
using PCL.Core.Utils;

namespace PCL;

/// <summary>将下载业务提示桥接到 WPF 消息框与下载页导航。</summary>
public sealed class WpfDownloadInteraction : IDownloadUserInteraction
{
    public void ShowClientUpdateHint(string versionName, JsonObject json)
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
            var msgResult = DialogCompat.ShowMessageBox(
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
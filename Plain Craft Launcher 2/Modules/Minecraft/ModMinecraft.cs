using System.Text.Json.Nodes;
using PCL.Core.App.Localization;
using PCL.Core.Utils;

namespace PCL;

public static class ModMinecraft
{
    /// <summary>
    ///     发送 Minecraft 更新提示。
    /// </summary>
    public static void McDownloadClientUpdateHint(string versionName, JsonObject json)
    {
        try
        {
            // 获取对应版本
            JsonNode version = null;
            foreach (var Token in json["versions"].AsArray())
                if (Token["id"] is not null && (Token["id"].ToString() ?? "") == (versionName ?? ""))
                {
                    version = Token;
                    break;
                }

            // 进行提示
            if (version is null)
                return;
            var time = version["releaseTime"].ToObject<DateTime>();
            var msgBoxText = Lang.Text("Minecraft.Update.NewVersion", versionName) + "\r\n" +
                             ((DateTime.Now - time).TotalDays > 1d
                                 ? Lang.Text("Minecraft.Update.UpdateTime") + Lang.Date(time)
                                 : Lang.Text("Minecraft.Update.UpdatedAt") + Lang.TimeSpan(time - DateTime.Now));
            var msgResult = ModMain.MyMsgBox(msgBoxText, Lang.Text("Minecraft.Update.Title"),
                Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Download"),
                (DateTime.Now - time).TotalHours > 3d ? Lang.Text("Common.Action.UpdateLog") : "",
                button3Action: () => ModDownloadLib.McUpdateLogShow(version));
            // 弹窗结果
            if (msgResult == 2)
                // 下载
                ModBase.RunInUi(() =>
                {
                    PageDownloadInstall.mcVersionWaitingForSelect = versionName;
                    ModMain.frmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall);
                });
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Minecraft.Error.UpdateNotify", versionName ?? "Nothing"), ModBase.LogLevel.Feedback);
        }
    }
}

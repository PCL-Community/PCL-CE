using System;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;

namespace PCL;

public static partial class Launcher
{
    /// <summary>显示设备码登录弹窗。</summary>
    public static void ShowDeviceCodeLogin(DialogRequest request)
    {
        try
        {
            DialogService.Instance.Show(request);
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "显示弹窗失败",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Main.Error.OperationFailed"));
        }
    }
}

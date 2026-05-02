using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Core.IO.Net.Http.Client.Request;
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch.Utils;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;
using PCL.Network;


namespace PCL;

public static partial class ModLaunch
{
    /// <summary>
    ///     记录启动日志。
    /// </summary>
    public static void McLaunchLog(string Text)
    {
        Text = ModMinecraft.FilterUserName(ModMinecraft.FilterAccessToken(Text, '*'), '*');
        LauncherDispatcher.RunInUi(() =>
            ModMain.FrmLaunchRight.LabLog.Text += "\r\n" + "[" + TimeUtils.GetTimeNow() + "] " + Text);
        LauncherLogger.Log("[Launch] " + Text);
    }

    /// <summary>
    ///     统一处理 HttpWebException
    /// </summary>
    private static void HandleHttpWebException(WebException ex, string logPrefix)
    {
        var allMessage = ex.ToString();
        ModProfile.ProfileLog(logPrefix + "：" + allMessage);

        if ((allMessage.Contains("超时") || allMessage.Contains("imeout")) && !allMessage.Contains("403"))
        {
            ModProfile.ProfileLog("已触发超时登录失败");
            ModMain.MyMsgBox(
                "$登录失败：连接登录服务器超时。" + "\r\n" +
                "请检查你的网络状况是否良好，或尝试使用 VPN！" + "\r\n" + "\r\n" +
                "详细信息：" + ex.InnerException,
                "第三方验证失败", IsWarn: true);

            throw new Exception("$登录失败：连接登录服务器超时。" + "\r\n" +
                                "请检查你的网络状况是否良好，或尝试使用 VPN！" + "\r\n" +
                                "\r\n" + "详细信息：" + ex.InnerException);
        }
    }

    /// <summary>
    ///     统一处理普通异常
    /// </summary>
    private static void HandleException(Exception ex, string logPrefix)
    {
        ModProfile.ProfileLog(logPrefix + "：" + ex);
        ModMain.MyMsgBox(logPrefix + ": " + ex, "第三方验证失败", IsWarn: true);
        throw new Exception("$" + logPrefix + "\r\n" + "\r\n" + "详细信息：" + ex);
    }
}

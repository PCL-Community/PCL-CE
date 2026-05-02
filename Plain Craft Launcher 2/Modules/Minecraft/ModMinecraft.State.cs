using System.Collections;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Core.UI;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using PCL.Network;


namespace PCL;

public static partial class ModMinecraft
{
    /// <summary>
    ///     发送 Minecraft 更新提示。
    /// </summary>
    public static void McDownloadClientUpdateHint(string versionName, JObject json)
    {
        try
        {
            // 获取对应版本
            JToken version = null;
            foreach (var Token in json["versions"])
                if (Token["id"] is not null && (Token["id"].ToString() ?? "") == (versionName ?? ""))
                {
                    version = Token;
                    break;
                }

            // 进行提示
            if (version is null)
                return;
            var time = (DateTime)version["releaseTime"];
            var msgBoxText = $"新版本：{versionName}{"\r\n"}" + ((DateTime.Now - time).TotalDays > 1d
                ? "更新时间：" + time
                : "更新于：" + TimeUtils.GetTimeSpanString(time - DateTime.Now, false));
            var msgResult = ModMain.MyMsgBox(msgBoxText, "Minecraft 更新提示", "确定", "下载",
                (DateTime.Now - time).TotalHours > 3d ? "更新日志" : "",
                Button3Action: () => ModDownloadLib.McUpdateLogShow(version));
            // 弹窗结果
            if (msgResult == 2)
                // 下载
                ModBase.RunInUi(() =>
                {
                    PageDownloadInstall.McVersionWaitingForSelect = versionName;
                    ModMain.FrmMain.PageChange(FormMain.PageType.Download, FormMain.PageSubType.DownloadInstall);
                });
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "Minecraft 更新提示发送失败（" + (versionName ?? "Nothing") + "）", ModBase.LogLevel.Feedback);
        }
    }

    /// <summary>
    ///     比较两个版本名；等同 Left >= Right。
    ///     无法比较两个预发布版的大小。
    ///     支持的格式：未知版本, 1.13.2, 1.7.10-pre4, 1.8_pre, 1.14 Pre-Release 2, 1.14.4 C6
    /// </summary>
    public static bool CompareVersionGe(string left, string right)
    {
        return CompareVersion(left, right) >= 0;
    }

    /// <summary>
    ///     比较两个版本名，若 Left 较新则返回 1，相同则返回 0，Right 较新则返回 -1；等同 Left - Right。
    ///     无法比较两个预发布版的大小。
    ///     支持的格式：未知版本, 26.1-snapshot-1，1.13.2, 1.7.10-pre4, 1.8_pre, 1.14 Pre-Release 2, 1.14.4 C6
    /// </summary>
    public static int CompareVersion(string left, string right)
    {
        if (left == "未知版本" || right == "未知版本")
        {
            if (left == "未知版本" && right != "未知版本")
                return 1;
            if (left == "未知版本" && right == "未知版本")
                return 0;
            if (left != "未知版本" && right == "未知版本")
                return -1;
        }

        left = left.ToLowerInvariant();
        right = right.ToLowerInvariant();
        var lefts = left.Replace("快照", "snapshot").Replace("预览版", "pre").RegexSearch("[a-z]+|[0-9]+");
        var rights = right.Replace("快照", "snapshot").Replace("预览版", "pre").RegexSearch("[a-z]+|[0-9]+");
        var i = 0;
        while (true)
        {
            // 两边均缺失，感觉是一个东西
            if (lefts.Count - 1 < i && rights.Count - 1 < i)
            {
                if (Operators.CompareString(left, right, false) > 0)
                    return 1;
                if (Operators.CompareString(left, right, false) < 0)
                    return -1;
                return 0;
            }

            // 确定两边的数值
            var leftValue = Conversions.ToString(lefts.Count - 1 < i ? 0 : lefts[i]);
            var rightValue = Conversions.ToString(rights.Count - 1 < i ? 0 : rights[i]);
            if ((leftValue ?? "") == (rightValue ?? ""))
                goto NextEntry;
            if (leftValue == "rc")
                leftValue = (-1).ToString();
            if (leftValue == "pre")
                leftValue = (-2).ToString();
            if (leftValue == "snapshot")
                leftValue = (-3).ToString();
            if (leftValue == "experimental")
                leftValue = (-4).ToString();
            var leftValValue = ModBase.Val(leftValue);
            if (rightValue == "rc")
                rightValue = (-1).ToString();
            if (rightValue == "pre")
                rightValue = (-2).ToString();
            if (rightValue == "snapshot")
                rightValue = (-3).ToString();
            if (rightValue == "experimental")
                rightValue = (-4).ToString();
            var rightValValue = ModBase.Val(rightValue);
            if (leftValValue == 0d && rightValValue == 0d)
            {
                // 如果没有数值则直接比较字符串
                if (Operators.CompareString(leftValue, rightValue, false) > 0) return 1;

                if (Operators.CompareString(leftValue, rightValue, false) < 0) return -1;
            }
            // 如果有数值则比较数值
            // 这会使得一边是数字一边是字母时数字方更大
            else if (leftValValue > rightValValue)
            {
                return 1;
            }
            else if (leftValValue < rightValValue)
            {
                return -1;
            }

            NextEntry: ;

            i += 1;
        }

        return 0;
    }

    /// <summary>
    ///     打码字符串中的 AccessToken。
    /// </summary>
    public static string FilterAccessToken(string Raw, char FilterChar)
    {
        // 打码 "accessToken " 后的内容
        if (Raw.Contains("accessToken "))
            foreach (var Token in Raw.RegexSearch("(?<=accessToken ([^ ]{5}))[^ ]+(?=[^ ]{5})"))
                Raw = Raw.Replace(Token, new string(FilterChar, Token.Count()));
        // 打码当前登录的结果
        var AccessToken = ModLaunch.McLoginLoader.Output.AccessToken;
        if (AccessToken is not null && AccessToken.Length >= 10 && Raw.ContainsF(AccessToken, true) &&
            (ModLaunch.McLoginLoader.Output.Uuid ?? "") !=
            (ModLaunch.McLoginLoader.Output.AccessToken ?? "")) // UUID 和 AccessToken 一样则不打码
            Raw = Raw.Replace(AccessToken,
                Strings.Left(AccessToken, 5) + new string(FilterChar, AccessToken.Length - 10) +
                Strings.Right(AccessToken, 5));
        return Raw;
    }

    /// <summary>
    ///     打码字符串中的 Windows 用户名。
    /// </summary>
    public static string FilterUserName(string Raw, char FilterChar)
    {
        var UserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var UserName = UserProfile.Split(@"\").Last();
        var MaskedProfile = UserProfile.Replace(UserName, new string(FilterChar, UserName.Length));
        return Raw.Replace(UserProfile, MaskedProfile);
    }

    /// <summary>
    ///     比较两个版本名的排序器。
    /// </summary>
    public class VersionComparer : IComparer<string>
    {
        public int Compare(string x, string y)
        {
            return CompareVersion(x, y);
        }
    }

}

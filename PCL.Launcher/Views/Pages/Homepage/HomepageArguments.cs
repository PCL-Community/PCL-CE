using System.Globalization;
using System.Text.RegularExpressions;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.Localization;
using PCL.Core.Utils.Secret;

namespace PCL.Views.Pages.Homepage;

/// <summary>替换主页 AXAML 中的启动器参数标记。</summary>
public static class HomepageArguments
{
    public static string ReplaceArguments(string text)
    {
        text = text
            .Replace("{pcl_version}", LauncherCommon.VersionBaseName)
            .Replace(
                "{pcl_version_code}",
                LauncherCommon.VersionCode.ToString(CultureInfo.InvariantCulture))
            .Replace("{pcl_version_branch}", LauncherCommon.VersionBranchName)
            .Replace("{pcl_branch}", LauncherCommon.VersionBranchName)
            .Replace("{identify}", Identify.LauncherId)
            .Replace("{path}", Basics.ExecutableDirectory)
            .Replace("{path_with_name}", Basics.ExecutableName)
            .Replace("{path_temp}", Paths.Temp)
            .Replace("{date}", Lang.Date(DateTime.Now, "d"))
            .Replace("{time}", Lang.Date(DateTime.Now, "T"))
            .Replace("{java}", GameLaunch.mcLaunchJavaSelected?.Installation.JavaFolder ?? "")
            .Replace("{minecraft}", GameFolders.mcFolderSelected ?? "");

        var instance = InstanceList.McMcInstanceSelected;
        if (instance is null)
        {
            text = text
                .Replace("{version_path}", "")
                .Replace("{verpath}", "")
                .Replace("{version_indie}", "")
                .Replace("{verindie}", "")
                .Replace("{name}", "")
                .Replace("{version}", "");
        }
        else
        {
            text = text
                .Replace("{version_path}", instance.PathInstance)
                .Replace("{verpath}", instance.PathInstance)
                .Replace("{version_indie}", instance.PathIndie ?? "")
                .Replace("{verindie}", instance.PathIndie ?? "")
                .Replace("{name}", instance.Name);
            var vanillaName = instance.Info.VanillaName;
            var versionName = vanillaName is "unknown" or "old" or "pending"
                ? instance.Name
                : vanillaName;
            text = text.Replace("{version}", versionName);
        }

        var loginLoader = GameLaunch.mcLoginLoader;
        if (loginLoader.State == LoadState.Finished)
        {
            var login = loginLoader.output;
            text = text
                .Replace("{user}", login.Name ?? "")
                .Replace("{uuid}", login.Uuid?.ToLowerInvariant() ?? "");
            text = loginLoader.input?.LoginType switch
            {
                GameLaunch.McLoginType.Legacy => text.Replace(
                    "{login}",
                    Lang.Text("Launch.Account.Type.Offline")),
                GameLaunch.McLoginType.Ms => text.Replace(
                    "{login}",
                    Lang.Text("Launch.Account.Type.Microsoft")),
                GameLaunch.McLoginType.Auth => text.Replace("{login}", "Authlib-Injector"),
                _ => text.Replace("{login}", "")
            };
        }
        else
        {
            text = text
                .Replace("{user}", "")
                .Replace("{uuid}", "")
                .Replace("{login}", "");
        }

        text = Regex.Replace(
            text,
            @"\{setup:([a-zA-Z0-9]+)\}",
            match =>
            {
                if (!ConfigService.TryGetConfigItemNoType(match.Groups[1].Value, out var item)
                    || item.Source == ConfigSource.SharedEncrypt)
                    return "";
                return item.GetValueNoType(instance?.PathInstance)?.ToString() ?? "";
            });
        text = Regex.Replace(
            text,
            @"\{(?:varible|variable):([^:\}]+)(?::([^\}]+))?\}",
            match => CustomEvent.GetCustomVariable(
                match.Groups[1].Value,
                match.Groups[2].Success ? match.Groups[2].Value : ""));
        text = text
            .Replace("{hint}", Lang.Text("Tools.Test.CeNotice"))
            .Replace("{cave}", Lang.Text("Tools.Test.CeNotice"));
        return text;
    }
}
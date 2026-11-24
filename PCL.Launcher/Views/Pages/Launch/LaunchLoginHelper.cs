using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.Network;

namespace PCL.Views.Pages.Launch;

/// <summary>
///     启动页与登录视图共用的账号辅助逻辑：档案展示文本、离线 UUID、启动用登录数据与皮肤地址。
///     档案状态与持久化由 <see cref="ProfileService" /> 负责，这里只做界面层的取值与格式转换。
/// </summary>
public static class LaunchLoginHelper
{
    public static string GetProfileInfo(McProfile profile)
    {
        var info = profile.ProfileType switch
        {
            ProfileType.Authlib => Lang.Text("Launch.Account.Type.ThirdParty")
                                   + (string.IsNullOrWhiteSpace(profile.ServerName)
                                       ? ""
                                       : $" / {profile.ServerName}"),
            ProfileType.YggdrasilConnect => Lang.Text("Launch.Account.Type.ThirdParty")
                                            + (string.IsNullOrWhiteSpace(profile.ServerName)
                                                ? " / Yggdrasil Connect"
                                                : $" / {profile.ServerName}"),
            ProfileType.Microsoft => Lang.Text("Launch.Account.Type.Microsoft"),
            _ => Lang.Text("Launch.Account.Type.Offline")
        };
        return string.IsNullOrWhiteSpace(profile.Description)
            ? info
            : $"{info}，{profile.Description}";
    }

    public static GameLaunch.McLoginData GetLoginData(GameLaunch.McLoginType targetAuthType = default)
    {
        var profile = ProfileService.Current;
        if (profile is null)
            return targetAuthType switch
            {
                GameLaunch.McLoginType.Ms => new GameLaunch.McLoginMs(),
                GameLaunch.McLoginType.Auth => new GameLaunch.McLoginServer(GameLaunch.McLoginType.Auth)
                {
                    Description = "Authlib-Injector",
                    IsExist = false
                },
                _ => new GameLaunch.McLoginLegacy()
            };

        return profile.ProfileType switch
        {
            ProfileType.Microsoft => new GameLaunch.McLoginMs(),
            ProfileType.Authlib => new GameLaunch.McLoginServer(GameLaunch.McLoginType.Auth)
            {
                BaseUrl = profile.Server,
                UserName = profile.LoginName,
                Password = profile.Password,
                Description = profile.ServerName ?? "Authlib-Injector",
                IsExist = true
            },
            ProfileType.YggdrasilConnect => new GameLaunch.McLoginServer(GameLaunch.McLoginType.Auth)
            {
                BaseUrl = profile.Server,
                Description = profile.ServerName ?? "Yggdrasil Connect",
                IsExist = true,
                ProviderType = ProfileType.YggdrasilConnect,
                DiscoveryAddress = profile.DiscoveryAddress
            },
            _ => new GameLaunch.McLoginLegacy { UserName = profile.UserName, Uuid = profile.Uuid }
        };
    }

    public static string IsProfileValid()
    {
        var profile = ProfileService.Current;
        if (profile is null)
            return Lang.Text("Minecraft.Launch.Precheck.NoProfile");
        if (profile.ProfileType != ProfileType.Offline)
            return string.Empty;
        if (string.IsNullOrWhiteSpace(profile.UserName))
            return Lang.Text("Launch.Account.Profile.Validation.EmptyUsername");
        if (profile.UserName.Contains('"'))
            return Lang.Text("Launch.Account.Profile.Validation.QuoteInUsername");
        if (InstanceList.McMcInstanceSelected is not null
            && InstanceList.McMcInstanceSelected.Info.Drop >= 203
            && profile.UserName.Trim().Length > 16)
            return Lang.Text("Launch.Account.Profile.Validation.UsernameTooLong");
        return string.Empty;
    }

    public static string GetOfflineUuid(string userName, bool isSplited = false, bool isLegacy = false)
    {
        if (isLegacy)
        {
            var fullUuid = StrFill(userName.Length.ToString("X"), "0", 16)
                           + StrFill(LauncherCommon.GetHash(userName).ToString("X"), "0", 16);
            return fullUuid[..12] + "3" + fullUuid.Substring(13, 3) + "9" + fullUuid.Substring(17, 15);
        }

        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" + userName));
        hash[6] = (byte)((hash[6] & 0xF) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        var uuid = new Guid(hash);
        return isSplited ? uuid.ToString() : uuid.ToString("N");
    }

    private static string StrFill(string text, string fillChar, int length)
    {
        return text.Length >= length ? text[..length] : fillChar + text;
    }

    #region 档案编辑

    public static void EditOfflineUuid(McProfile profile)
    {
        _ = EditOfflineUuidAsync(profile);
    }

    private static async Task EditOfflineUuidAsync(McProfile profile)
    {
        var current = ProfileService.Profiles.FirstOrDefault(p => p.ProfileId == profile.ProfileId);
        if (current is null)
            return;

        var typeResult = await DialogService.Instance.ShowAsync(new SelectDialogRequest
        {
            Title = Lang.Text("Launch.Account.Profile.Uuid.SelectType.Title"),
            Options =
            [
                new DialogOption { Text = Lang.Text("Launch.Account.Profile.Uuid.Standard") },
                new DialogOption { Text = Lang.Text("Launch.Account.Profile.Uuid.Legacy") },
                new DialogOption { Text = Lang.Text("Common.Option.Customize") }
            ],
            Buttons = DialogButtons.From(
                DialogButton.Create(Lang.Text("Common.Action.Continue")),
                DialogButton.Create(Lang.Text("Common.Action.Cancel"))),
            HighLight = false
        });
        if (!typeResult.IsConfirmed)
            return;

        string? newUuid;
        switch (typeResult.SelectedIndex)
        {
            case 0:
                newUuid = GetOfflineUuid(current.UserName);
                break;
            case 1:
                newUuid = GetOfflineUuid(current.UserName, isLegacy: true);
                break;
            default:
            {
                var inputResult = await DialogService.Instance.ShowAsync(new InputDialogRequest
                {
                    Title = Lang.Text("Launch.Account.Profile.Uuid.ChangeTitle", current.UserName),
                    HintText = Lang.Text("Launch.Account.Profile.Uuid.Hint"),
                    DefaultValue = current.Uuid,
                    Buttons = DialogButtons.OkCancel,
                    HighLight = false
                });
                if (!inputResult.IsConfirmed)
                    return;
                newUuid = (inputResult.InputText ?? "").Replace("-", "").Trim();
                if (newUuid.Length != 32 || !newUuid.All(char.IsAsciiHexDigit))
                {
                    HintService.Show(Hints.Error(Lang.Text("Launch.Account.Profile.Uuid.InvalidChars")));
                    return;
                }

                break;
            }
        }

        ApplyProfileChange(current, updated => updated.Uuid = newUuid);
    }

    public static void EditAuthServerName(McProfile profile)
    {
        _ = EditAuthServerNameAsync(profile);
    }

    private static async Task EditAuthServerNameAsync(McProfile profile)
    {
        var current = ProfileService.Profiles.FirstOrDefault(p => p.ProfileId == profile.ProfileId);
        if (current is null)
            return;

        var result = await DialogService.Instance.ShowAsync(new InputDialogRequest
        {
            Title = Lang.Text("Launch.Account.Profile.EditServerName.Title"),
            Content = Lang.Text("Launch.Account.Profile.EditServerName.Message"),
            DefaultValue = current.ServerName ?? "",
            Buttons = DialogButtons.OkCancel,
            HighLight = false
        });
        if (!result.IsConfirmed)
            return;

        var name = result.InputText ?? "";
        ApplyProfileChange(current, updated => updated.ServerName = name);
    }

    public static void EditOfflineUserName(McProfile profile)
    {
        _ = EditOfflineUserNameAsync(profile);
    }

    private static async Task EditOfflineUserNameAsync(McProfile profile)
    {
        var current = ProfileService.Profiles.FirstOrDefault(p => p.ProfileId == profile.ProfileId);
        if (current is null)
            return;

        var result = await DialogService.Instance.ShowAsync(new InputDialogRequest
        {
            Title = Lang.Text("Launch.Account.Profile.EditPlayerId.Title"),
            DefaultValue = current.UserName,
            HintText = Lang.Text("Launch.Account.Profile.EditPlayerId.Hint"),
            Buttons = DialogButtons.OkCancel,
            HighLight = false
        });
        if (!result.IsConfirmed)
            return;

        var name = (result.InputText ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            HintService.Show(Hints.Error(Lang.Text("Launch.Account.Profile.EditPlayerId.Empty")));
            return;
        }

        if (name.Length is < 3 or > 16 || !name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
        {
            HintService.Show(Hints.Error(Lang.Text("Launch.Account.Profile.EditPlayerId.NotAllowed")));
            return;
        }

        ApplyProfileChange(current, updated =>
        {
            updated.UserName = name;
            updated.Uuid = GetOfflineUuid(name);
        });
    }

    private static void ApplyProfileChange(McProfile current, Action<McProfile> modify)
    {
        var updated = current.Clone();
        modify(updated);
        ProfileService.Update(current, updated);
        if (ReferenceEquals(ProfileService.Current, current))
            ProfileService.Select(updated);
        HintService.Show(Hints.Success(Lang.Text("Launch.Account.Profile.Saved")));
    }

    #endregion

    #region 皮肤

    public static string GetSkinVariant(string uuid)
    {
        if (uuid.Length != 32)
            return "Steve";

        var a = Convert.ToInt32(uuid[7].ToString(), 16);
        var b = Convert.ToInt32(uuid[15].ToString(), 16);
        var c = Convert.ToInt32(uuid[23].ToString(), 16);
        return (a & 1) == 0 && (b & 1) == 0 && (c & 1) == 0 ? "Steve" : "Alex";
    }

    public static string GetOfflineSkinAddress(string uuid)
    {
        var variant = GetSkinVariant(uuid);
        return $"{LauncherPaths.ImageRoot}Skins/{variant}.png";
    }

    public static string GetOnlineSkinAddress(McProfile profile)
    {
        var cacheFolder = Path.Combine(LauncherPaths.Temp, "Cache", "Skin");
        Directory.CreateDirectory(cacheFolder);
        var cacheFile = Path.Combine(cacheFolder, LauncherCommon.GetHash(profile.Uuid + profile.ProfileType) + ".png");
        if (File.Exists(cacheFile) && new FileInfo(cacheFile).Length > 0L)
            return cacheFile;

        var baseUrl = profile.ProfileType switch
        {
            ProfileType.Authlib or ProfileType.YggdrasilConnect when !string.IsNullOrEmpty(profile.Server) =>
                profile.Server.Replace("/authserver", ""),
            _ => "https://sessionserver.mojang.com"
        };
        var url = $"{baseUrl.TrimEnd('/')}/session/minecraft/profile/{profile.Uuid}";
        var json = NetFacade.NetGetCodeByRequestRetry(url) as string
                   ?? throw new InvalidDataException("获取玩家档案失败");
        var textures = (JsonNode.Parse(json) as JsonObject)?["properties"]
            ?.AsArray()
            .FirstOrDefault(property => property?["name"]?.ToString() == "textures")?["value"]?.ToString()
                       ?? throw new InvalidDataException("玩家档案中没有皮肤信息");
        var decoded = JsonNode.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(textures)));
        var skinUrl = decoded?["textures"]?["SKIN"]?["url"]?.ToString()
                      ?? throw new InvalidDataException("玩家档案中没有皮肤地址");

        var downloaded = PCL.UI.Controls.MyImage.DownloadImageAsync(skinUrl)
            .GetAwaiter()
            .GetResult();
        if (string.IsNullOrEmpty(downloaded))
            throw new InvalidDataException("皮肤下载失败");
        File.Copy(downloaded, cacheFile, true);
        return cacheFile;
    }

    #endregion
}
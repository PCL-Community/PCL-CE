using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Authentication;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.Utils;
using PCL.Core.Utils.Validate;
using PCL.Network;

namespace PCL;

/// <summary>
/// WPF-only profile workflows. Profile state and persistence live in the Core ProfileService.
/// </summary>
public static class ProfileUi
{
    public static void ProfileLog(string content, ModBase.LogLevel level = ModBase.LogLevel.Normal)
        => ModBase.Log("[Profile] " + content, level);

    public static Task<AuthorizeResult?> ShowDeviceCodeLoginAsync(DeviceCodeAuthenticationContext context,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource<AuthorizeResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var converter = new ModMain.MyMsgBoxConverter
        {
            Content = JsonSerializer.SerializeToNode(context.Data, JsonCompat.SerializerOptions)!.AsObject(),
            ForceWait = true,
            Type = ModMain.MyMsgBoxType.Login,
            DeviceCodePoll = (_, pollToken) => context.PollAsync(pollToken),
            LoginResultHandler = (oauth, _) =>
            {
                completion.TrySetResult(oauth);
                return Task.CompletedTask;
            },
            CompletionHandler = result =>
            {
                if (result is Exception ex) completion.TrySetException(ex);
            }
        };
        ModMain.WaitingMyMsgBox.Add(converter);
        return completion.Task;
    }

    public static object McLoginMojangUuid(string name, bool throwOnNotFound)
    {
        if (string.IsNullOrWhiteSpace(name)) return ModBase.StrFill("", "0", 32);
        var uuid = ModBase.ReadIni(ModBase.pathTemp + @"Cache\Uuid\Mojang.ini", name);
        if (uuid?.Length == 32) return uuid;
        try
        {
            JsonObject? json = null;
            var finished = false;
            ModBase.RunInNewThread(() =>
            {
                try { json = (JsonObject)ModNet.NetGetCodeByRequestRetry("https://api.mojang.com/users/profiles/minecraft/" + name, isJson: true); }
                catch { }
                finally { finished = true; }
            }, $"{name} Uuid Get");
            while (!finished) Thread.Sleep(50);
            if (json is null) throw new FileNotFoundException("正版玩家档案不存在（" + name + "）");
            uuid = json["id"]?.ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "从官网获取正版 UUID 失败（" + name + "）");
            if (!throwOnNotFound && ex is FileNotFoundException) uuid = GetOfflineUuid(name, isLegacy: true);
            else throw new Exception("从官网获取正版 UUID 失败", ex);
        }
        if (uuid.Length != 32) throw new Exception("获取的正版 UUID 长度不足（" + uuid + "）");
        ModBase.WriteIni(ModBase.pathTemp + @"Cache\Uuid\Mojang.ini", name, uuid);
        return uuid;
    }

    public static bool CanCreateOtherProfile()
    {
#if DEBUG || DEBUGCI
        return true;
#else
        return ProfileService.HasMicrosoftProfile || (Lang.IsFeaturesUnrestricted && ProfileService.Profiles.Count > 0) || NetworkHelper.IsNetworkAvailable() is false;
#endif
    }

    public static void CreateProfile()
    {
        int? selected = null;
        ModBase.RunInUiWait(() =>
        {
            var includeOthers = CanCreateOtherProfile();
            selected = ModMain.MyMsgBoxSelect(_GetAvailableProfileSelection(includeOthers),
                Lang.Text("Launch.Account.Profile.Create.SelectAuthType.Title"), Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel"));
        });
        if (selected is null) return;
        ProfileService.IsCreatingProfile = true;
        var type = selected.Value switch
        {
            0 => ModLaunch.McLoginType.Ms,
            1 => ModLaunch.McLoginType.Auth,
            _ => ModLaunch.McLoginType.Legacy
        };
        if (type == ModLaunch.McLoginType.Auth)
        {
            string? server = null;
            ModBase.RunInUiWait(() => server = ModMain.MyMsgBoxAuthServer(
                PageLoginAuth.DefaultAuthServer, PageLoginAuth.PredefinedAuthServers));
            if (string.IsNullOrWhiteSpace(server))
            {
                ProfileService.IsCreatingProfile = false;
                return;
            }
            PageLoginAuth.draggedAuthServerOAuthSupported =
                PageLoginAuth.IsOAuthSupportedAsync(server).GetAwaiter().GetResult();
            PageLoginAuth.draggedAuthServer = server;
        }
        ModBase.RunInUi(() => ModMain.frmLaunchLeft.RefreshPage(true, type));
    }

    private static List<IMyRadio> _GetAvailableProfileSelection(bool includeOthers) => includeOthers
        ? [
            new MyListItem { Title = Lang.Text("Launch.Account.Type.Microsoft"), Type = MyListItem.CheckType.RadioBox, SvgIcon = "lucide/shield-check" },
            new MyListItem { Title = Lang.Text("Launch.Account.Type.ThirdParty"), Type = MyListItem.CheckType.RadioBox, SvgIcon = "lucide/network" },
            new MyListItem { Title = Lang.Text("Launch.Account.Type.Offline"), Type = MyListItem.CheckType.RadioBox, SvgIcon = "lucide/link-2-off" }
        ]
        : [new MyListItem { Title = Lang.Text("Launch.Account.Type.Microsoft"), Type = MyListItem.CheckType.RadioBox, SvgIcon = "lucide/shield-check" }];

    public static void EditProfileId()
    {
        var profile = ProfileService.Current;
        if (profile is null) return;
        if (profile.ProfileType == ProfileType.Microsoft)
        {
            string? username = null;
            ModBase.RunInUiWait(() => username = ModMain.MyMsgBoxInput(
                Lang.Text("Launch.Account.Profile.EditPlayerId.Title"),
                Lang.Text("Launch.Account.Profile.EditPlayerId.MicrosoftWarning"), profile.UserName,
                [new StringLengthValidator(3, 16), new RegexValidator("([A-z]|[0-9]|_)+")],
                Lang.Text("Launch.Account.Profile.EditPlayerId.Hint"), Lang.Text("Common.Action.Confirm")));
            if (string.IsNullOrWhiteSpace(username)) return;
            if (ModMain.MyMsgBox(Lang.Text("Launch.Account.Profile.EditPlayerId.Confirm.Message"),
                    Lang.Text("Launch.Account.Profile.EditPlayerId.Confirm.Title"), Lang.Text("Common.Action.Continue"),
                    Lang.Text("Common.Action.Cancel"), isWarn: true) == 2) return;
            ModBase.RunInNewThread(() =>
            {
                try
                {
                    var check = (JsonObject)ModBase.GetJson(Requester.Fetch(
                        $"https://api.minecraftservices.com/minecraft/profile/name/{username}/available",
                        new FetchParam { Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer " + profile.AccessToken } }));
                    var status = check["status"]?.ToString();
                    if (status is "DUPLICATE" or "NOT_ALLOWED")
                    {
                        ModMain.MyMsgBox(status == "DUPLICATE"
                                ? Lang.Text("Launch.Account.Profile.EditPlayerId.Duplicate")
                                : Lang.Text("Launch.Account.Profile.EditPlayerId.NotAllowed"),
                            Lang.Text("Launch.Account.Profile.EditPlayerId.Failed.Title"), Lang.Text("Common.Action.Confirm"), isWarn: true);
                        return;
                    }
                    var result = (JsonObject)ModBase.GetJson(Requester.Fetch(
                        $"https://api.minecraftservices.com/minecraft/profile/name/{username}",
                        new FetchParam { Method = "PUT", ContentType = "application/json", Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer " + profile.AccessToken } }));
                    var updated = profile.Clone();
                    updated.UserName = result["name"]?.ToString() ?? username;
                    ProfileService.Update(profile, updated);
                    ProfileService.Select(updated);
                    HintService.Hint(Lang.Text("Launch.Account.Profile.EditPlayerId.Success", updated.UserName), HintType.Success);
                    ModBase.RunInUi(() => { ModMain.frmLoginProfileSkin?.Reload(); ModMain.frmLaunchLeft.RefreshPage(true); });
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, Lang.Text("Launch.Account.Profile.Error.ChangeId"), ModBase.LogLevel.Msgbox,
                        userSummary: Lang.Text("Launch.Account.Profile.Error.ChangeId"));
                }
            });
            return;
        }
        if (profile.ProfileType == ProfileType.Authlib || profile.ProfileType == ProfileType.YggdrasilConnect)
        {
            if (!string.IsNullOrWhiteSpace(profile.Server))
                ModBase.OpenWebsite(profile.Server.Replace("/api/yggdrasil/authserver", "/user/profile"));
            return;
        }

        string? newName = null;
        ModBase.RunInUiWait(() => newName = ModMain.MyMsgBoxInput(Lang.Text("Launch.Account.Profile.EditPlayerId.Title"),
            defaultInput: profile.UserName, validateRules: [new StringLengthValidator(3, 16), new RegexValidator("([A-z]|[0-9]|_)+")],
            hintText: Lang.Text("Launch.Account.Profile.EditPlayerId.Hint"), button1: Lang.Text("Common.Action.Confirm"), button2: Lang.Text("Common.Action.Cancel")));
        if (!string.IsNullOrWhiteSpace(newName)) EditOfflineUuid(profile, GetOfflineUuid(newName));
    }

    public static void EditOfflineUuid(McProfile profile, string? uuid = null)
    {
        var current = ProfileService.Profiles.FirstOrDefault(p => p.ProfileId == profile.ProfileId);
        if (current is null) return;
        string? newUuid = uuid;
        if (newUuid is null)
        {
            int? type = null;
            ModBase.RunInUiWait(() => type = ModMain.MyMsgBoxSelect([
                new MyRadioBox { Text = Lang.Text("Launch.Account.Profile.Uuid.Standard") },
                new MyRadioBox { Text = Lang.Text("Launch.Account.Profile.Uuid.Legacy") },
                new MyRadioBox { Text = Lang.Text("Common.Option.Customize") }],
                Lang.Text("Launch.Account.Profile.Uuid.SelectType.Title"), Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel")));
            if (type is null) return;
            newUuid = type switch
            {
                0 => GetOfflineUuid(current.UserName),
                1 => GetOfflineUuid(current.UserName, isLegacy: true),
                _ => ModMain.MyMsgBoxInput(Lang.Text("Launch.Account.Profile.Uuid.ChangeTitle", current.UserName), defaultInput: current.Uuid,
                    hintText: Lang.Text("Launch.Account.Profile.Uuid.Hint"), validateRules: [new StringLengthValidator(32, 32), new RegexValidator("([A-z]|[0-9]){32}", Lang.Text("Launch.Account.Profile.Uuid.InvalidChars"))],
                    button1: Lang.Text("Common.Action.Continue"), button2: Lang.Text("Common.Action.Cancel"))
            };
        }
        if (string.IsNullOrWhiteSpace(newUuid)) return;
        var updated = current.Clone();
        updated.Uuid = newUuid;
        ProfileService.Update(current, updated);
        ProfileService.Select(updated);
        HintService.Hint(Lang.Text("Launch.Account.Profile.Saved"), HintType.Success);
    }

    public static void EditAuthServerName(McProfile profile, string serverName)
    {
        var current = ProfileService.Profiles.FirstOrDefault(p => p.ProfileId == profile.ProfileId);
        if (current is null) return;
        var updated = current.Clone();
        updated.ServerName = serverName;
        ProfileService.Update(current, updated);
        if (ReferenceEquals(ProfileService.Current, current)) ProfileService.Select(updated);
        HintService.Hint(Lang.Text("Launch.Account.Profile.Saved"), HintType.Success);
    }

    public static void RemoveProfile(McProfile profile)
    {
        ProfileService.Remove(profile);
        HintService.Hint(Lang.Text("Launch.Account.Profile.Deleted"), HintType.Success);
    }

    public static string GetOfflineUuid(string userName, bool isSplited = false, bool isLegacy = false)
    {
        if (isLegacy)
        {
            var fullUuid = ModBase.StrFill(userName.Length.ToString("X"), "0", 16) + ModBase.StrFill(ModBase.GetHash(userName).ToString("X"), "0", 16);
            var value = fullUuid.Substring(0, 12) + "3" + fullUuid.Substring(13, 3) + "9" + fullUuid.Substring(17, 15);
            return value;
        }
        using var md5 = MD5.Create();
        var hash = md5.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" + userName));
        hash[6] = (byte)((hash[6] & 0xF) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        var uuid = Guid.Parse(_ToUuidString(hash));
        return isSplited ? uuid.ToString() : uuid.ToString("N");
    }

    private static string _ToUuidString(byte[] bytes)
    {
        long msb = 0;
        long lsb = 0;
        for (var i = 0; i <= 7; i++) msb = (msb << 8) | (bytes[i] & 0xff);
        for (var i = 8; i <= 15; i++) lsb = (lsb << 8) | (bytes[i] & 0xff);
        return $"{_Digits(msb >> 32, 8)}-{_Digits(msb >> 16, 4)}-{_Digits(msb, 4)}-{_Digits(lsb >> 48, 4)}-{_Digits(lsb, 12)}";
    }

    private static string _Digits(long value, int digits)
    {
        var high = 1L << (digits * 4);
        return (high | (value & (high - 1L))).ToString("X")[1..];
    }

    public static string GetProfileInfo(McProfile profile)
    {
        var info = profile.ProfileType switch
        {
            ProfileType.Authlib => Lang.Text("Launch.Account.Type.ThirdParty") + (string.IsNullOrWhiteSpace(profile.ServerName) ? "" : $" / {profile.ServerName}"),
            ProfileType.YggdrasilConnect => Lang.Text("Launch.Account.Type.ThirdParty") + (string.IsNullOrWhiteSpace(profile.ServerName) ? " / Yggdrasil Connect" : $" / {profile.ServerName}"),
            ProfileType.Microsoft => Lang.Text("Launch.Account.Type.Microsoft"),
            _ => Lang.Text("Launch.Account.Type.Offline")
        };
        return string.IsNullOrWhiteSpace(profile.Description) ? info : $"{info}，{profile.Description}";
    }

    public static ModLaunch.McLoginData GetLoginData(ModLaunch.McLoginType targetAuthType = default)
    {
        var profile = ProfileService.Current;
        if (profile is null)
        {
            return targetAuthType switch
            {
                ModLaunch.McLoginType.Ms => new ModLaunch.McLoginMs(),
                ModLaunch.McLoginType.Auth => new ModLaunch.McLoginServer(ModLaunch.McLoginType.Auth) { Description = "Authlib-Injector", IsExist = false },
                _ => new ModLaunch.McLoginLegacy()
            };
        }
        return profile.ProfileType switch
        {
            ProfileType.Microsoft => new ModLaunch.McLoginMs(),
            ProfileType.Authlib => new ModLaunch.McLoginServer(ModLaunch.McLoginType.Auth)
            {
                BaseUrl = profile.Server,
                UserName = profile.LoginName,
                Password = profile.Password,
                Description = profile.ServerName ?? "Authlib-Injector",
                IsExist = true
            },
            ProfileType.YggdrasilConnect => new ModLaunch.McLoginServer(ModLaunch.McLoginType.Auth)
            {
                BaseUrl = profile.Server,
                Description = profile.ServerName ?? "Yggdrasil Connect",
                IsExist = true,
                ProviderType = ProfileType.YggdrasilConnect,
                DiscoveryAddress = profile.DiscoveryAddress
            },
            _ => new ModLaunch.McLoginLegacy { UserName = profile.UserName, Uuid = profile.Uuid }
        };
    }

    public static string IsProfileValid()
    {
        var profile = ProfileService.Current;
        if (profile is null) return Lang.Text("Minecraft.Launch.Precheck.NoProfile");
        if (profile.ProfileType != ProfileType.Offline) return string.Empty;
        if (string.IsNullOrWhiteSpace(profile.UserName)) return Lang.Text("Launch.Account.Profile.Validation.EmptyUsername");
        if (profile.UserName.Contains('"')) return Lang.Text("Launch.Account.Profile.Validation.QuoteInUsername");
        if (ModInstanceList.McMcInstanceSelected is not null && ModInstanceList.McMcInstanceSelected.Info.Drop >= 203 && profile.UserName.Trim().Length > 16)
            return Lang.Text("Launch.Account.Profile.Validation.UsernameTooLong");
        return string.Empty;
    }

    public static void ChangeSkinMs()
    {
        var profile = ProfileService.Current;
        if (profile is null) return;
        var profileId = profile.ProfileId;
        if (ModLaunch.mcLoginLoader.State == ModBase.LoadState.Failed)
        {
            HintService.Hint(Lang.Text("Launch.Skin.Change.LoginFailed"), HintType.Error);
            return;
        }
        var skinInfo = ModSkin.McSkinSelect();
        if (!skinInfo.IsVaild) return;
        ModBase.RunInNewThread(() =>
        {
            try
            {
                if (ModLaunch.mcLoginMsLoader.State == ModBase.LoadState.Loading) ModLaunch.mcLoginMsLoader.WaitForExit();
                if (ModLaunch.mcLoginMsLoader.State != ModBase.LoadState.Finished) ModLaunch.mcLoginMsLoader.WaitForExit(GetLoginData());
                if (ModLaunch.mcLoginMsLoader.State != ModBase.LoadState.Finished) throw new Exception("Microsoft login failed");
                var latestProfile = ProfileService.Profiles.FirstOrDefault(item => item.ProfileId == profileId)
                                    ?? throw new InvalidOperationException("Microsoft profile no longer exists.");
                var contents = new MultipartFormDataContent
                {
                    { new StringContent(skinInfo.IsSlim ? "slim" : "classic"), "variant" },
                    { new ByteArrayContent(ModBase.ReadFileBytes(skinInfo.LocalFile)), "file", ModBase.GetFileNameFromPath(skinInfo.LocalFile) }
                };
                var result = Requester.Fetch("https://api.minecraftservices.com/minecraft/profile/skins", new FetchParam
                {
                    Method = "POST", Content = contents,
                    Headers = new Dictionary<string, string> { ["Authorization"] = "Bearer " + latestProfile.AccessToken }
                });
                if (result.Contains("request requires user authentication")) throw new Exception("$$");
                var json = (JsonObject)ModBase.GetJson(result);
                var active = json["skins"]?.AsArray().FirstOrDefault(s => s?["state"]?.ToString() == "ACTIVE")?.AsObject();
                if (active?["url"] is not null) MySkin.ReloadCache(active["url"]!.ToString(), latestProfile.Uuid);
                else throw new Exception(json["errorMessage"]?.ToString() ?? result);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, Lang.Text("Launch.Account.Profile.Error.ChangeSkin"), ModBase.LogLevel.Hint,
                    userSummary: Lang.Text("Launch.Account.Profile.Error.ChangeSkin"));
            }
        }, "Ms Skin Upload");
    }
}

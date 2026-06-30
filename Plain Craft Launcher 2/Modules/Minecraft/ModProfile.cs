using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using PCL.Core.Utils.Secret;
using PCL.Core.Utils.Validate;
using PCL.Network;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net;

namespace PCL;

public static class ModProfile
{
    /// <summary>
    ///     当前选定的档案
    /// </summary>
    public static McProfile selectedProfile;

    /// <summary>
    ///     上次选定的档案编号
    /// </summary>
    public static int lastUsedProfile;

    /// <summary>
    ///     档案列表
    /// </summary>
    public static List<McProfile> profileList = new();

    public static bool isCreatingProfile;

    /// <summary>
    ///     档案操作日志
    /// </summary>
    public static void ProfileLog(string content, ModBase.LogLevel level = ModBase.LogLevel.Normal)
    {
        var output = "[Profile] " + content;
        ModBase.Log(output, level);
    }

    #region 获取正版档案 UUID

    /// <summary>
    ///     根据用户名返回对应 UUID，需要多线程
    /// </summary>
    /// <param name="name">玩家 ID</param>
    public static object McLoginMojangUuid(string name, bool throwOnNotFound)
    {
        if (name.Trim().Length == 0)
            return ModBase.StrFill("", "0", 32);
        // 从缓存获取
        var uuid = ModBase.ReadIni(ModBase.pathTemp + @"Cache\Uuid\Mojang.ini", name);
        if ((uuid?.Length ?? 0) == 32)
            return uuid;
        // 从官网获取
        try
        {
            JsonObject gotJson = null;
            var finished = false;
            ModBase.RunInNewThread(() =>
                {
                    try
                    {
                        gotJson = (JsonObject)ModNet.NetGetCodeByRequestRetry(
                            "https://api.mojang.com/users/profiles/minecraft/" + name, isJson: true);
                    }
                    catch (Exception ex)
                    {
                    }
                    finally
                    {
                        finished = true;
                    }
                }, $"{name} Uuid Get");
            while (!finished)
                Thread.Sleep(50);
            if (gotJson is null)
                throw new FileNotFoundException("正版玩家档案不存在（" + name + "）");
            uuid = (string)(gotJson["id"] ?? "");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "从官网获取正版 UUID 失败（" + name + "）");
            if (!throwOnNotFound && ex is FileNotFoundException)
                uuid = GetOfflineUuid(name, isLegacy: true); // 玩家档案不存在
            else
                throw new Exception("从官网获取正版 UUID 失败", ex);
        }

        // 写入缓存
        if ((uuid?.Length ?? 0) != 32)
            throw new Exception("获取的正版 UUID 长度不足（" + uuid + "）");
        ModBase.WriteIni(ModBase.pathTemp + @"Cache\Uuid\Mojang.ini", name, uuid);
        return uuid;
    }

    #endregion

    #region 类型声明

    public class McProfile
    {
        public string AccessToken;
        public string ClientToken;

        /// <summary>
        ///     档案描述，暂时没做功能
        /// </summary>
        public string Desc;

        /// <summary>
        ///     联网验证档案的验证有效期
        /// </summary>
        public long Expires;

        /// <summary>
        ///     用于识别正版档案的 ID 标识符
        /// </summary>
        [Obsolete("暂时弃用，应当使用 AccessToken 与 RefreshToken")]
        public string IdentityId;

        /// <summary>
        ///     登录用户名，用于第三方验证
        /// </summary>
        public string Name;

        /// <summary>
        ///     登录密码，用于第三方验证
        /// </summary>
        public string Password;

        /// <summary>
        ///     原始 JSON 数据，用于正版验证部分功能
        /// </summary>
        public string RawJson;

        public string RefreshToken;

        /// <summary>
        ///     验证服务器地址，用于第三方验证
        /// </summary>
        public string Server;

        /// <summary>
        ///     验证服务器名称，来自第三方验证服务器返回的 Metadata
        /// </summary>
        public string ServerName;

        /// <summary>
        ///     用于档案列表头像显示的皮肤 ID
        /// </summary>
        public string SkinHeadId;

        /// <summary>
        ///     档案类型
        /// </summary>
        public ModLaunch.McLoginType Type;

        /// <summary>
        ///     玩家 ID
        /// </summary>
        public string Username;

        public string Uuid;
    }

    #endregion

    #region 读写档案

    /// <summary>
    ///     重新获取已有档案列表
    /// </summary>
    public static void GetProfile()
    {
        ProfileLog("开始获取本地档案");
        profileList.Clear();
        var profilePath = Path.Combine(ModBase.pathAppdataConfig, "profiles.json");
        try
        {
            if (!Directory.Exists(ModBase.pathAppdataConfig))
                Directory.CreateDirectory(ModBase.pathAppdataConfig);
            if (!File.Exists(profilePath))
            {
                File.Create(profilePath).Close();
                ModBase.WriteFile(profilePath, "{\"lastUsed\":0,\"profiles\":[]}"); // 创建档案列表文件
            }

            var profileJobj = ModBase.GetJson(ModBase.ReadFile(profilePath));
            lastUsedProfile = (int)profileJobj["lastUsed"];
            var profileListJobj = (JsonArray)profileJobj["profiles"];
            foreach (var Profile in profileListJobj)
            {
                McProfile newProfile = null;
                if ((string)Profile["type"] == "microsoft")
                    newProfile = new McProfile
                    {
                        Type = ModLaunch.McLoginType.Ms,
                        Uuid = (string)Profile["uuid"],
                        Username = (string)Profile["username"],
                        AccessToken = EncryptHelper.SecretDecrypt((string?)Profile["accessToken"]),
                        RefreshToken = EncryptHelper.SecretDecrypt((string?)Profile["refreshToken"]),
                        Expires = (long)Profile["expires"],
                        Desc = (string)Profile["desc"],
                        RawJson = EncryptHelper.SecretDecrypt((string?)Profile["rawJson"]),
                        SkinHeadId = (string)Profile["skinHeadId"]
                    };
                else if ((string)Profile["type"] == "authlib")
                    newProfile = new McProfile
                    {
                        Type = ModLaunch.McLoginType.Auth,
                        Uuid = (string)Profile["uuid"],
                        Username = (string)Profile["username"],
                        AccessToken = EncryptHelper.SecretDecrypt((string?)Profile["accessToken"]),
                        RefreshToken = EncryptHelper.SecretDecrypt((string?)Profile["refreshToken"]),
                        Expires = (long)Profile["expires"],
                        Server = (string)Profile["server"],
                        ServerName = (string)Profile["serverName"],
                        Name = EncryptHelper.SecretDecrypt((string?)Profile["name"]),
                        Password = EncryptHelper.SecretDecrypt((string?)Profile["password"]),
                        ClientToken = EncryptHelper.SecretDecrypt((string?)Profile["clientToken"]),
                        Desc = (string)Profile["desc"],
                        SkinHeadId = (string)Profile["skinHeadId"]
                    };
                else
                    newProfile = new McProfile
                    {
                        Type = ModLaunch.McLoginType.Legacy,
                        Uuid = (string)Profile["uuid"],
                        Username = (string)Profile["username"],
                        Desc = (string)Profile["desc"],
                        SkinHeadId = (string)Profile["skinHeadId"]
                    };
                profileList.Add(newProfile);
            }

            ProfileLog($"获取到 {profileList.Count} 个档案");
        }
        catch (Exception ex)
        {
            try
            {
                var profilePathBak =
                    Path.Combine(ModBase.pathAppdataConfig, $"profiles.json.bak{DateTime.Now.ToBinary()}");
                File.Move(profilePath, profilePathBak);
            }
            catch (Exception ex1)
            {
            }

            ModBase.Log(
                ex,
                Lang.Text("Launch.Account.Profile.Error.Corrupted"),
                ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Launch.Account.Profile.Error.Corrupted"));
        }
    }

    /// <summary>
    ///     以当前的档案列表写入配置文件
    /// </summary>
    public static void SaveProfile(JsonArray listJson = null)
    {
        try
        {
            var json = new JsonObject();
            if (listJson is not null)
            {
                json = new JsonObject { { "lastUsed", lastUsedProfile }, { "profiles", listJson } };
            }
            else
            {
                var list = new JsonArray();
                foreach (var Profile in profileList)
                {
                    JsonObject profileJobj = null;
                    if (Profile.Type == ModLaunch.McLoginType.Ms)
                        profileJobj = new JsonObject
                        {
                            { "type", "microsoft" }, { "uuid", Profile.Uuid }, { "username", Profile.Username },
                            { "accessToken", EncryptHelper.SecretEncrypt(Profile.AccessToken) },
                            { "refreshToken", EncryptHelper.SecretEncrypt(Profile.RefreshToken) },
                            { "expires", Profile.Expires }, { "desc", Profile.Desc },
                            { "rawJson", EncryptHelper.SecretEncrypt(Profile.RawJson) },
                            { "skinHeadId", Profile.SkinHeadId }
                        };
                    else if (Profile.Type == ModLaunch.McLoginType.Auth)
                        profileJobj = new JsonObject
                        {
                            { "type", "authlib" }, { "uuid", Profile.Uuid }, { "username", Profile.Username },
                            { "accessToken", EncryptHelper.SecretEncrypt(Profile.AccessToken) },
                            { "refreshToken", EncryptHelper.SecretEncrypt(Profile.RefreshToken) },
                            { "expires", Profile.Expires }, { "server", Profile.Server },
                            { "serverName", Profile.ServerName }, { "name", EncryptHelper.SecretEncrypt(Profile.Name) },
                            { "password", EncryptHelper.SecretEncrypt(Profile.Password) },
                            { "clientToken", EncryptHelper.SecretEncrypt(Profile.ClientToken) },
                            { "desc", Profile.Desc }, { "skinHeadId", Profile.SkinHeadId }
                        };
                    else
                        profileJobj = new JsonObject
                        {
                            { "type", "offline" }, { "uuid", Profile.Uuid }, { "username", Profile.Username },
                            { "desc", Profile.Desc }, { "skinHeadId", Profile.SkinHeadId }
                        };
                    list.Add(profileJobj);
                }

                ProfileLog($"开始保存档案，共 {list.Count} 个");
                json = new JsonObject { { "lastUsed", lastUsedProfile }, { "profiles", list } };
            }

            var actualFile = Path.Combine(ModBase.pathAppdataConfig, "profiles.json");
            var tempFile = actualFile + ".tmp";
            var bakFile = actualFile + ".bak";
            File.WriteAllBytes(tempFile, Encoding.UTF8.GetBytes(json.ToJsonString()));
            if (File.Exists(actualFile))
                File.Replace(tempFile, actualFile, bakFile);
            else
                File.Move(tempFile, actualFile);
            ProfileLog("档案已保存");
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                Lang.Text("Launch.Account.Profile.Error.Write"),
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Launch.Account.Profile.Error.Write"));
        }
    }

    #endregion

    #region 新建与编辑

    /// <summary>
    ///     新建档案
    /// </summary>
    public static void CreateProfile()
    {
        int? selectedAuthTypeNum = default; // 验证类型序号
        ModBase.RunInUiWait(() =>
        {
            List<IMyRadio> authTypeList;
#if DEBUG || DEBUGCI
            authTypeList = _GetAvailableProfileSelection(true);
#else
            var hasMinecraftAccount = profileList.Any(x => x.Type == ModLaunch.McLoginType.Ms);
            var restricted = Lang.IsFeaturesUnrestricted && profileList.Count > 0;
            var hasNetwork = NetworkHelper.IsNetworkAvailable();
            if (hasMinecraftAccount || restricted || !hasNetwork)
                authTypeList = _GetAvailableProfileSelection(true);
            else
                authTypeList = _GetAvailableProfileSelection(false);
            
#endif
        
            selectedAuthTypeNum = ModMain.MyMsgBoxSelect(authTypeList, Lang.Text("Launch.Account.Profile.Create.SelectAuthType.Title"), Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel"));
        });
        if (selectedAuthTypeNum is null)
            return;
        isCreatingProfile = true;
        if (selectedAuthTypeNum.HasValue && selectedAuthTypeNum.Value == 0) // 正版验证
            ModBase.RunInUi(() => ModMain.frmLaunchLeft.RefreshPage(true, ModLaunch.McLoginType.Ms));
        else if (selectedAuthTypeNum.HasValue && selectedAuthTypeNum.Value == 1) // 第三方验证
            ModBase.RunInUi(() => ModMain.frmLaunchLeft.RefreshPage(true, ModLaunch.McLoginType.Auth));
        else // 离线验证
            ModBase.RunInUi(() => ModMain.frmLaunchLeft.RefreshPage(true, ModLaunch.McLoginType.Legacy));
    }

    private static List<IMyRadio> _GetAvailableProfileSelection(bool includeOfflineAndThirdParty) => includeOfflineAndThirdParty switch
    {
        true =>
        [
            new MyListItem
            {
                Title = Lang.Text("Launch.Account.Type.Microsoft"),
                Type = MyListItem.CheckType.RadioBox,
                SvgIcon = "lucide/shield-check"
            },

            new MyListItem
            {
                Title = Lang.Text("Launch.Account.Type.ThirdParty"),
                Type = MyListItem.CheckType.RadioBox,
                SvgIcon = "lucide/network"
            },

            new MyListItem
            {
                Title = Lang.Text("Launch.Account.Type.Offline"),
                Type = MyListItem.CheckType.RadioBox,
                SvgIcon = "lucide/link-2-off"
            }
        ],
        _ =>
        [
            new MyListItem
            {
                Title = Lang.Text("Launch.Account.Type.Microsoft"),
                Type = MyListItem.CheckType.RadioBox,
                SvgIcon = "lucide/shield-check"
            }
        ]
    };
            

    /// <summary>
    ///     编辑当前档案的 ID
    /// </summary>
    public static void EditProfileId()
    {
        if (selectedProfile.Type == ModLaunch.McLoginType.Ms)
        {
            string newUsername = null;
            ModBase.RunInUiWait(() => newUsername = ModMain.MyMsgBoxInput(Lang.Text("Launch.Account.Profile.EditPlayerId.Title"), Lang.Text("Launch.Account.Profile.EditPlayerId.MicrosoftWarning"),
                selectedProfile.Username,
                [new StringLengthValidator(3, 16), new RegexValidator("([A-z]|[0-9]|_)+")],
                Lang.Text("Launch.Account.Profile.EditPlayerId.Hint"), Lang.Text("Common.Action.Confirm")));
            if (string.IsNullOrEmpty(newUsername))
                return;
            if (string.IsNullOrWhiteSpace(newUsername))
            {
                HintService.Hint(Lang.Text("Launch.Account.Profile.EditPlayerId.Empty"));
                return;
            }

            if (ModMain.MyMsgBox(Lang.Text("Launch.Account.Profile.EditPlayerId.Confirm.Message"), Lang.Text("Launch.Account.Profile.EditPlayerId.Confirm.Title"), Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel"), isWarn: true) == 2)
                return;
            // 更新档案信息
            // 刷新页面信息
            ModBase.RunInNewThread(() =>
            {
                try
                {
                    var checkResult = (JsonObject)ModBase.GetJson(Requester.Fetch(
                        $"https://api.minecraftservices.com/minecraft/profile/name/{newUsername}/available", 
                        new FetchParam
                        {
                            Headers = new Dictionary<string, string>
                                { { "Authorization", "Bearer " + selectedProfile.AccessToken } }
                        }));
                    if ((string)checkResult["status"] == "DUPLICATE")
                    {
                        ModMain.MyMsgBox(Lang.Text("Launch.Account.Profile.EditPlayerId.Duplicate"), Lang.Text("Launch.Account.Profile.EditPlayerId.Failed.Title"), Lang.Text("Common.Action.Confirm"), isWarn: true);
                        return;
                    }

                    if ((string)checkResult["status"] == "NOT_ALLOWED")
                    {
                        ModMain.MyMsgBox(Lang.Text("Launch.Account.Profile.EditPlayerId.NotAllowed"), Lang.Text("Launch.Account.Profile.EditPlayerId.Failed.Title"), Lang.Text("Common.Action.Confirm"), isWarn: true);
                        return;
                    }

                    var result = Requester.Fetch(
                        $"https://api.minecraftservices.com/minecraft/profile/name/{newUsername}",
                        new FetchParam
                        {
                            Method = "PUT",
                            ContentType = "application/json",
                            Headers = new Dictionary<string, string>
                                { { "Authorization", "Bearer " + selectedProfile.AccessToken } }
                        });
                    var resultJson = (JsonObject)ModBase.GetJson(result);
                    HintService.Hint(Lang.Text("Launch.Account.Profile.EditPlayerId.Success", resultJson["name"]), HintType.Success);
                    profileList.Remove(selectedProfile);
                    selectedProfile.Username = (string)resultJson["name"];
                    profileList.Add(selectedProfile);
                    lastUsedProfile = profileList.Count - 1;
                    ModMain.frmLaunchLeft.RefreshPage(true);
                    SaveProfile();
                }
                catch (HttpRequestException ex)
                {
                    var exSummary = ex.ToString();
                    if (exSummary.Contains("403"))
                        ModMain.MyMsgBox(Lang.Text("Launch.Account.Profile.EditPlayerId.Cooldown"), Lang.Text("Launch.Account.Profile.EditPlayerId.Failed.Title"), Lang.Text("Common.Action.Confirm"));
                    else
                        ModBase.Log(
                            ex,
                            Lang.Text("Launch.Account.Profile.Error.ChangeId"),
                            ModBase.LogLevel.Msgbox,
                            userSummary: Lang.Text("Launch.Account.Profile.Error.ChangeId"));
                }
            });
        }


        else if (selectedProfile.Type == ModLaunch.McLoginType.Auth)
        {
            var server = selectedProfile.Server;
            ModBase.OpenWebsite(server.Replace("/api/yggdrasil/authserver" + (server.EndsWithF("/") ? "/" : ""),
                "/user/profile"));
        }
        else
        {
            string newUsername = null;
            ModBase.RunInUiWait(() => newUsername = ModMain.MyMsgBoxInput(Lang.Text("Launch.Account.Profile.EditPlayerId.Title"),
                defaultInput: selectedProfile.Username,
                validateRules: [new StringLengthValidator(3, 16), new RegexValidator("([A-z]|[0-9]|_)+")],
                hintText: Lang.Text("Launch.Account.Profile.EditPlayerId.Hint"), button1: Lang.Text("Common.Action.Confirm"), button2: Lang.Text("Common.Action.Cancel")));
            if (string.IsNullOrEmpty(newUsername))
                return;
            EditOfflineUuid(selectedProfile, GetOfflineUuid(newUsername));
        }
    }

    /// <summary>
    ///     编辑离线档案的 UUID
    /// </summary>
    /// <param name="profile">目标档案</param>
    public static void EditOfflineUuid(McProfile profile, string uuid = null)
    {
        var profileIndex = profileList.IndexOf(profile);
        string newUuid;
        if (uuid is not null)
        {
            newUuid = uuid;
            goto Write;
        }

        int uuidType;
        int? uuidTypeInput = default;
        ModBase.RunInUiWait(() =>
        {
            var uuidTypeList = new List<IMyRadio>
            {
                new MyRadioBox { Text = Lang.Text("Launch.Account.Profile.Uuid.Standard") }, new MyRadioBox { Text = Lang.Text("Launch.Account.Profile.Uuid.Legacy") },
                new MyRadioBox { Text = Lang.Text("Common.Option.Customize") }
            };
            uuidTypeInput = ModMain.MyMsgBoxSelect(uuidTypeList, Lang.Text("Launch.Account.Profile.Uuid.SelectType.Title"), Lang.Text("Common.Action.Continue"), Lang.Text("Common.Action.Cancel"));
        });
        if (uuidTypeInput is null)
            return;
        uuidType = (int)uuidTypeInput;
        if (uuidType == 0)
            newUuid = GetOfflineUuid(profile.Username);
        else if (uuidType == 1)
            newUuid = GetOfflineUuid(profile.Username, isLegacy: true);
        else
            newUuid = ModMain.MyMsgBoxInput(Lang.Text("Launch.Account.Profile.Uuid.ChangeTitle", profile.Username), defaultInput: profile.Uuid,
                hintText: Lang.Text("Launch.Account.Profile.Uuid.Hint"),
                validateRules:
                [new StringLengthValidator(32, 32), new RegexValidator("([A-z]|[0-9]){32}", Lang.Text("Launch.Account.Profile.Uuid.InvalidChars"))],
                button1: Lang.Text("Common.Action.Continue"), button2: Lang.Text("Common.Action.Cancel"));
        if (string.IsNullOrEmpty(newUuid))
            return;
        Write: ;

        profileList[profileIndex].Uuid = newUuid;
        selectedProfile = profileList[profileIndex];
        SaveProfile();
        HintService.Hint(Lang.Text("Launch.Account.Profile.Saved"), HintType.Success);
    }

    /// <summary>
    ///     编辑指定档案的验证服务器显示名称
    /// </summary>
    public static void EditAuthServerName(McProfile profile, string serverName)
    {
        var profileIndex = profileList.IndexOf(profile);
        profileList[profileIndex].ServerName = serverName;
        SaveProfile();
        HintService.Hint(Lang.Text("Launch.Account.Profile.Saved"), HintType.Success);
    }

    /// <summary>
    ///     删除特定档案
    /// </summary>
    /// <param name="profile">目标档案</param>
    public static void RemoveProfile(McProfile profile)
    {
        profileList.Remove(profile);
        lastUsedProfile = default;
        SaveProfile();
        HintService.Hint(Lang.Text("Launch.Account.Profile.Deleted"), HintType.Success);
    }

    #endregion

    #region 离线 UUID 获取

    /// <summary>
    ///     获取离线 UUID
    /// </summary>
    /// <param name="userName">玩家 ID</param>
    /// <param name="isSplited">返回的 UUID 是否有连字符分割</param>
    /// <param name="isLegacy">是否使用旧版 PCL 生成方式，若为 True 则返回的 UUID 总是不带连字符</param>
    public static string GetOfflineUuid(string userName, bool isSplited = false, bool isLegacy = false)
    {
        if (isLegacy)
        {
            var fullUuid = ModBase.StrFill(userName.Length.ToString("X"), "0", 16) +
                           ModBase.StrFill(ModBase.GetHash(userName).ToString("X"), "0", 16);
            return fullUuid.Substring(0, 12) + "3" + fullUuid.Substring(13, 3) + "9" + fullUuid.Substring(17, 15);
        }

        var md5Hash = MD5.Create();
        var hash = md5Hash.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" + userName));
        hash[6] = (byte)(hash[6] & 0xF);
        hash[6] = (byte)(hash[6] | 0x30);
        hash[8] = (byte)(hash[8] & 0x3F);
        hash[8] = (byte)(hash[8] | 0x80);
        var parsed = new Guid(ToUuidString(hash));
        ProfileLog("获取到离线 UUID: " + parsed);
        if (isSplited) return parsed.ToString();

        return parsed.ToString().Replace("-", "");
    }

    private static string ToUuidString(byte[] bytes)
    {
        var msb = 0L;
        var lsb = 0L;
        for (var i = 0; i <= 7; i++)
            msb = (msb << 8) | (bytes[i] & 0xFF);
        for (var i = 8; i <= 15; i++)
            lsb = (lsb << 8) | (bytes[i] & 0xFF);
        return $"{Digits(msb >> 32, 8)}-{Digits(msb >> 16, 4)}-{Digits(msb, 4)}-{Digits(lsb >> 48, 4)}-{Digits(lsb, 12)}";
    }

    private static object Digits(long val, int digs)
    {
        var hi = 1L << (digs * 4);
        return (hi | (val & (hi - 1L))).ToString("X").Substring(1);
    }

    #endregion

    #region 档案信息获取

    /// <summary>
    ///     获取档案详情信息用于显示
    /// </summary>
    /// <param name="profile">目标档案</param>
    /// <returns>显示的详情信息</returns>
    public static object GetProfileInfo(McProfile profile)
    {
        string info = null;
        if (profile.Type == ModLaunch.McLoginType.Auth)
        {
            info += Lang.Text("Launch.Account.Type.ThirdParty");
            if (!string.IsNullOrWhiteSpace(profile.ServerName))
                info += $" / {profile.ServerName}";
        }
        else if (profile.Type == ModLaunch.McLoginType.Ms)
        {
            info += Lang.Text("Launch.Account.Type.Microsoft");
        }
        else
        {
            info += Lang.Text("Launch.Account.Type.Offline");
        }

        if (!string.IsNullOrWhiteSpace(profile.Desc))
            info += $"，{profile.Desc}";
        return info;
    }

    /// <summary>
    ///     获取当前档案的验证信息。
    ///     <param name="targetAuthType">验证类型，若为新档案需填</param>
    /// </summary>
    public static ModLaunch.McLoginData GetLoginData(ModLaunch.McLoginType targetAuthType = default)
    {
        ModLaunch.McLoginType authType = default;
        if (selectedProfile is null) // 新档案
        {
            if (targetAuthType != default)
                authType = targetAuthType;
            else
                authType = ModLaunch.McLoginType.Legacy;
            if (authType == ModLaunch.McLoginType.Auth)
                return new ModLaunch.McLoginServer(ModLaunch.McLoginType.Auth)
                {
                    Description = "Authlib-Injector",
                    LoginType = ModLaunch.McLoginType.Auth,
                    IsExist = ModMain.frmLoginAuth is null
                };

            if (authType == ModLaunch.McLoginType.Ms) return new ModLaunch.McLoginMs();

            return new ModLaunch.McLoginLegacy();
        }

        // 已有档案
        authType = selectedProfile.Type;
        if (authType == ModLaunch.McLoginType.Auth)
            return new ModLaunch.McLoginServer(ModLaunch.McLoginType.Auth)
            {
                BaseUrl = selectedProfile.Server,
                UserName = selectedProfile.Name,
                Password = selectedProfile.Password,
                Description = "Authlib-Injector",
                LoginType = ModLaunch.McLoginType.Auth,
                IsExist = ModMain.frmLoginAuth is null
            };

        if (authType == ModLaunch.McLoginType.Ms)
        {
            if (ModLaunch.mcLoginMsLoader.State == ModBase.LoadState.Finished)
                return new ModLaunch.McLoginMs
                {
                    OAuthRefreshToken = selectedProfile.RefreshToken,
                    UserName = selectedProfile.Username,
                    AccessToken = selectedProfile.AccessToken,
                    Uuid = selectedProfile.Uuid,
                    ProfileJson = selectedProfile.RawJson
                };

            return new ModLaunch.McLoginMs
                { OAuthRefreshToken = selectedProfile.RefreshToken, UserName = selectedProfile.Name };
        }

        return new ModLaunch.McLoginLegacy { UserName = selectedProfile.Username, Uuid = selectedProfile.Uuid };
    }

    /// <summary>
    ///     检查当前档案是否有效
    /// </summary>
    /// <returns>若档案验证有效，则返回空字符串，否则返回错误原因</returns>
    public static string IsProfileValid()
    {
        switch (selectedProfile.Type)
        {
            case ModLaunch.McLoginType.Legacy:
            {
                if (string.IsNullOrEmpty(selectedProfile.Username.Trim()))
                    return Lang.Text("Launch.Account.Profile.Validation.EmptyUsername");
                if (selectedProfile.Username.Contains("\""))
                    return Lang.Text("Launch.Account.Profile.Validation.QuoteInUsername");
                if (ModInstanceList.McMcInstanceSelected is not null && ModInstanceList.McMcInstanceSelected.Info.Drop >= 203 &&
                    selectedProfile.Username.Trim().Length > 16) return Lang.Text("Launch.Account.Profile.Validation.UsernameTooLong");
                return "";
            }
            case ModLaunch.McLoginType.Ms:
            {
                return "";
            }
            case ModLaunch.McLoginType.Auth:
            {
                return "";
            }
        }

        return Lang.Text("Launch.Account.Profile.Validation.UnknownAuthType");
    }

    #endregion

    #region 皮肤

    private static bool _isMsSkinChanging;

    public static void ChangeSkinMs()
    {
        // 检查条件，获取新皮肤
        if (_isMsSkinChanging)
        {
            HintService.Hint(Lang.Text("Launch.Skin.Change.Busy"));
            return;
        }

        if (ModLaunch.mcLoginLoader.State == ModBase.LoadState.Failed)
        {
            HintService.Hint(Lang.Text("Launch.Skin.Change.LoginFailed"), HintType.Error);
            return;
        }

        var skinInfo = ModSkin.McSkinSelect();
        if (!skinInfo.IsVaild)
            return;
        HintService.Hint(Lang.Text("Launch.Skin.Change.Starting"));
        _isMsSkinChanging = true;
        // 开始实际获取

        // 获取登录信息

        // 获取新皮肤地址
        ModBase.RunInNewThread(() =>
        {
            try
            {
                Retry: ;
                if (ModLaunch.mcLoginMsLoader.State == ModBase.LoadState.Loading)
                    ModLaunch.mcLoginMsLoader.WaitForExit();
                if (ModLaunch.mcLoginMsLoader.State != ModBase.LoadState.Finished)
                    ModLaunch.mcLoginMsLoader.WaitForExit(GetLoginData());
                if (ModLaunch.mcLoginMsLoader.State != ModBase.LoadState.Finished)
                {
                    HintService.Hint(Lang.Text("Launch.Skin.Change.LoginFailed"), HintType.Error);
                    return;
                }

                var accessToken = selectedProfile.AccessToken;
                var headers = new Dictionary<string, string>();
                headers.Add("Authorization", $"Bearer {accessToken}");
                headers.Add("Accept", "*/*");
                headers.Add("User-Agent", "MojangSharp/0.1");
                var contents = new MultipartFormDataContent
                {
                    { new StringContent(skinInfo.IsSlim ? "slim" : "classic"), "variant" },
                    {
                        new ByteArrayContent(ModBase.ReadFileBytes(skinInfo.LocalFile)), "file",
                        ModBase.GetFileNameFromPath(skinInfo.LocalFile)
                    }
                };
                var res = Requester.Fetch("https://api.minecraftservices.com/minecraft/profile/skins", 
                    new FetchParam
                    {
                        Method = "POST",
                        Content = contents,
                        Headers = headers
                    });
                if (res.Contains("request requires user authentication"))
                {
                    HintService.Hint(Lang.Text("Launch.Skin.Change.Reauthenticating"));
                    ModLaunch.mcLoginMsLoader.Start(GetLoginData(), true);
                    goto Retry;
                }

                if (res.Contains("\"error\""))
                {
                    HintService.Hint(
                        Lang.Text(
                            "Launch.Skin.Change.FailedWithDetail",
                            ((JsonObject)ModBase.GetJson(res))["error"]?.ToString() ?? res),
                        HintType.Error);
                    return;
                }

                ModBase.Log("[Skin] 皮肤修改返回值：" + "\r\n" + res);
                var resultJson = (JsonObject)ModBase.GetJson(res);
                if (resultJson.ContainsKey("errorMessage")) throw new Exception(resultJson["errorMessage"].ToString());
                foreach (var skinNode in resultJson["skins"].AsArray()) { var skin = skinNode.AsObject();
                    if (skin["state"].ToString() == "ACTIVE")
                    {
                        MySkin.ReloadCache((string)skin["url"]);
                        return;
                    } }

                 throw new Exception("未知错误（" + res + "）");
            }
            catch (Exception ex)
            {
                if (ex.GetType().Equals(typeof(TaskCanceledException)))
                    HintService.Hint(
                        Lang.Text("Launch.Skin.Change.Timeout.WithDetail", ex.ToString()),
                        HintType.Error);
                else
                    ModBase.Log(
                        ex,
                        Lang.Text("Launch.Account.Profile.Error.ChangeSkin"),
                        ModBase.LogLevel.Hint,
                        userSummary: Lang.Text("Launch.Account.Profile.Error.ChangeSkin"));
            }
            finally
            {
                _isMsSkinChanging = false;
            }
        }, "Ms Skin Upload"); // 等待登录结束
        // #5309
    }

    #endregion
}

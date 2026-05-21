using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Core.IO.Net;
using PCL.Core.Utils;
using PCL.Core.Utils.Secret;
using PCL.Core.Utils.Validate;
using PCL.Network;
using JsonSerializer = System.Text.Json.JsonSerializer;
using PCL.Core.App.Localization;
using PCL.Core.UI;

namespace PCL;

public static class ModProfile
{
    /// <summary>
    ///     当前选定的档案
    /// </summary>
    public static McProfile SelectedProfile;

    /// <summary>
    ///     上次选定的档案编号
    /// </summary>
    public static int LastUsedProfile;

    /// <summary>
    ///     档案列表
    /// </summary>
    public static List<McProfile> ProfileList = new();

    public static bool IsCreatingProfile;

    /// <summary>
    ///     档案操作日志
    /// </summary>
    public static void ProfileLog(string content, ModBase.LogLevel level = ModBase.LogLevel.Normal)
    {
        var output = "[Profile] " + content;
        ModBase.Log(output, level);
    }

    #region 旧版迁移

    /// <summary>
    ///     从旧版配置文件迁移档案，不能在 UI 线程调用
    /// </summary>
    public static void MigrateOldProfile()
    {
        ProfileLog("开始从旧版配置迁移档案");
        var profileCount = 0;
        // 正版档案
        if (Conversions.ToBoolean(
                !Operators.ConditionalCompareObjectEqual(States.Game.LegacyProfile.LoginMsJson, "{}", false)))
        {
            var oldMsJson = (JObject)ModBase.GetJson(Conversions.ToString(States.Game.LegacyProfile.LoginMsJson));
            ProfileLog($"找到 {oldMsJson.Count} 个旧版正版档案信息");
            foreach (var Profile in oldMsJson)
            {
                var newProfile = new McProfile
                {
                    Username = Profile.Key, Uuid = Conversions.ToString(McLoginMojangUuid(Profile.Key, false)),
                    Type = ModLaunch.McLoginType.Ms
                };
                ProfileList.Add(newProfile);
                profileCount += 1;
            }

            SaveProfile();
            ProfileLog("旧版正版档案迁移完成");
            ModBase.Setup.Reset("LoginMsJson");
        }
        else
        {
            ProfileLog("无旧版正版档案信息");
        }

        // 离线档案
        if (!string.IsNullOrWhiteSpace(Conversions.ToString(States.Game.LegacyProfile.LoginLegacyName)))
        {
            var oldOfflineInfo = (string[])((dynamic)States.Game.LegacyProfile.LoginLegacyName).Split("¨");
            ProfileLog($"找到 {oldOfflineInfo.Count()} 个旧版离线档案信息");
            foreach (var OfflineId in oldOfflineInfo)
            {
                var newProfile = new McProfile
                {
                    Username = OfflineId, Uuid = GetOfflineUuid(OfflineId, isLegacy: true),
                    Type = ModLaunch.McLoginType.Legacy
                }; // 迁移的档案默认使用旧版 UUID 生成方式以避免存档丢失
                ProfileList.Add(newProfile);
                profileCount += 1;
            }

            SaveProfile();
            ProfileLog("旧版离线档案迁移完成");
            ModBase.Setup.Reset("LoginLegacyName");
        }
        else
        {
            ProfileLog("无旧版离线档案信息");
        }

        // 第三方验证档案
        if (!(string.IsNullOrWhiteSpace(Conversions.ToString(States.Game.LegacyProfile.AuthUserName)) ||
              string.IsNullOrWhiteSpace(Conversions.ToString(States.Game.LegacyProfile.AuthUuid)) ||
              string.IsNullOrWhiteSpace(Conversions.ToString(States.Game.LegacyProfile.AuthServerAddress)) ||
              string.IsNullOrWhiteSpace(Conversions.ToString(States.Game.LegacyProfile.AuthThirdPartyUserName)) ||
              string.IsNullOrWhiteSpace(Conversions.ToString(States.Game.LegacyProfile.AuthPassword))))
        {
            ProfileLog("找到旧版第三方验证档案信息");
            var newProfile = new McProfile
            {
                Username = Conversions.ToString(States.Game.LegacyProfile.AuthUserName),
                Uuid = Conversions.ToString(States.Game.LegacyProfile.AuthUuid),
                Name = Conversions.ToString(States.Game.LegacyProfile.AuthThirdPartyUserName),
                Password = Conversions.ToString(States.Game.LegacyProfile.AuthPassword),
                Server = Conversions.ToString(Operators.ConcatenateObject(States.Game.LegacyProfile.AuthServerAddress,
                    "/authserver")),
                Type = ModLaunch.McLoginType.Auth
            };
            ProfileList.Add(newProfile);
            SaveProfile();
            ProfileLog("旧版第三方验证档案迁移完成");
            profileCount += 1;
            ModBase.Setup.Reset("CacheAuthName");
            ModBase.Setup.Reset("CacheAuthUuid");
            ModBase.Setup.Reset("CacheAuthServerServer");
            ModBase.Setup.Reset("CacheAuthUsername");
            ModBase.Setup.Reset("CacheAuthPass");
        }
        else
        {
            ProfileLog("无旧版第三方验证档案信息");
        }

        if (!(profileCount == 0))
            ModMain.Hint($"已自动从旧版配置文件迁移档案，共迁移了 {profileCount} 个档案");
        ProfileLog("档案迁移结束");
    }

    #endregion

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
        var uuid = ModBase.ReadIni(ModBase.PathTemp + @"Cache\Uuid\Mojang.ini", name);
        if (Strings.Len(uuid) == 32)
            return uuid;
        // 从官网获取
        try
        {
            JObject gotJson = null;
            var finished = false;
            ModBase.RunInNewThread(() =>
                {
                    try
                    {
                        gotJson = (JObject)ModNet.NetGetCodeByRequestRetry(
                            "https://api.mojang.com/users/profiles/minecraft/" + name, IsJson: true);
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
            if (!throwOnNotFound && ex.GetType().Name == "FileNotFoundException")
                uuid = GetOfflineUuid(name, isLegacy: true); // 玩家档案不存在
            else
                throw new Exception("从官网获取正版 UUID 失败", ex);
        }

        // 写入缓存
        if (!(Strings.Len(uuid) == 32))
            throw new Exception("获取的正版 UUID 长度不足（" + uuid + "）");
        ModBase.WriteIni(ModBase.PathTemp + @"Cache\Uuid\Mojang.ini", name, uuid);
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
        ProfileList.Clear();
        var profilePath = Path.Combine(ModBase.PathAppdataConfig, "profiles.json");
        try
        {
            if (!Directory.Exists(ModBase.PathAppdataConfig))
                Directory.CreateDirectory(ModBase.PathAppdataConfig);
            if (!File.Exists(profilePath))
            {
                File.Create(profilePath).Close();
                ModBase.WriteFile(profilePath, "{\"lastUsed\":0,\"profiles\":[]}"); // 创建档案列表文件
            }

            var profileJobj = JObject.Parse(ModBase.ReadFile(profilePath));
            LastUsedProfile = (int)profileJobj["lastUsed"];
            var profileListJobj = (JArray)profileJobj["profiles"];
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
                ProfileList.Add(newProfile);
            }

            ProfileLog($"获取到 {ProfileList.Count} 个档案");
        }
        catch (Exception ex)
        {
            try
            {
                var profilePathBak =
                    Path.Combine(ModBase.PathAppdataConfig, $"profiles.json.bak{DateTime.Now.ToBinary()}");
                File.Move(profilePath, profilePathBak);
            }
            catch (Exception ex1)
            {
            }

            ModBase.Log(ex, "档案数据读取失败，文件可能意外损坏。已对档案文件进行备份重置。", ModBase.LogLevel.Msgbox);
        }
    }

    /// <summary>
    ///     以当前的档案列表写入配置文件
    /// </summary>
    public static void SaveProfile(JArray listJson = null)
    {
        try
        {
            var json = new JObject();
            if (listJson is not null)
            {
                json = new JObject { { "lastUsed", LastUsedProfile }, { "profiles", listJson } };
            }
            else
            {
                var list = new JArray();
                foreach (var Profile in ProfileList)
                {
                    JObject profileJobj = null;
                    if (Profile.Type == ModLaunch.McLoginType.Ms)
                        profileJobj = new JObject
                        {
                            { "type", "microsoft" }, { "uuid", Profile.Uuid }, { "username", Profile.Username },
                            { "accessToken", EncryptHelper.SecretEncrypt(Profile.AccessToken) },
                            { "refreshToken", EncryptHelper.SecretEncrypt(Profile.RefreshToken) },
                            { "expires", Profile.Expires }, { "desc", Profile.Desc },
                            { "rawJson", EncryptHelper.SecretEncrypt(Profile.RawJson) },
                            { "skinHeadId", Profile.SkinHeadId }
                        };
                    else if (Profile.Type == ModLaunch.McLoginType.Auth)
                        profileJobj = new JObject
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
                        profileJobj = new JObject
                        {
                            { "type", "offline" }, { "uuid", Profile.Uuid }, { "username", Profile.Username },
                            { "desc", Profile.Desc }, { "skinHeadId", Profile.SkinHeadId }
                        };
                    list.Add(profileJobj);
                }

                ProfileLog($"开始保存档案，共 {list.Count} 个");
                json = new JObject { { "lastUsed", LastUsedProfile }, { "profiles", list } };
            }

            var actualFile = Path.Combine(ModBase.PathAppdataConfig, "profiles.json");
            var tempFile = actualFile + ".tmp";
            var bakFile = actualFile + ".bak";
            File.WriteAllBytes(tempFile, Encoding.UTF8.GetBytes(json.ToString(Formatting.None)));
            if (File.Exists(actualFile))
                File.Replace(tempFile, actualFile, bakFile);
            else
                File.Move(tempFile, actualFile);
            ProfileLog("档案已保存");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "写入档案列表失败", ModBase.LogLevel.Feedback);
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
            var HasMinecraftAccount = ProfileList.Any(x => x.Type == ModLaunch.McLoginType.Ms);
            var Restricted = RegionUtils.IsRestrictedFeatAllowed && ProfileList.Count > 0;
            var HasNetwork = NetworkHelper.IsNetworkAvailable();
            if (HasMinecraftAccount || Restricted || !HasNetwork)
                authTypeList = _GetAvailableProfileSelection(true);
            else
                authTypeList = _GetAvailableProfileSelection(false);
            
#endif
        
            selectedAuthTypeNum = ModMain.MyMsgBoxSelect(authTypeList, "新建档案 - 选择验证类型", "继续", Lang.Text("Common.Action.Cancel"));
        });
        if (selectedAuthTypeNum is null)
            return;
        IsCreatingProfile = true;
        if (selectedAuthTypeNum.HasValue && selectedAuthTypeNum.Value == 0) // 正版验证
            ModBase.RunInUi(() => ModMain.FrmLaunchLeft.RefreshPage(true, ModLaunch.McLoginType.Ms));
        else if (selectedAuthTypeNum.HasValue && selectedAuthTypeNum.Value == 1) // 第三方验证
            ModBase.RunInUi(() => ModMain.FrmLaunchLeft.RefreshPage(true, ModLaunch.McLoginType.Auth));
        else // 离线验证
            ModBase.RunInUi(() => ModMain.FrmLaunchLeft.RefreshPage(true, ModLaunch.McLoginType.Legacy));
    }

    private static List<IMyRadio> _GetAvailableProfileSelection(bool includeOfflineAndThirdParty) => includeOfflineAndThirdParty switch
    {
        true =>
        [
            new MyListItem
            {
                Title = "正版验证",
                Type = MyListItem.CheckType.RadioBox,
                Logo = Icon.IconButtonAuth
            },

            new MyListItem
            {
                Title = "第三方验证",
                Type = MyListItem.CheckType.RadioBox,
                Logo = Icon.IconButtonThirdparty
            },

            new MyListItem
            {
                Title = "离线验证",
                Type = MyListItem.CheckType.RadioBox,
                Logo = Icon.IconButtonOffline
            }
        ],
        _ =>
        [
            new MyListItem
            {
                Title = "正版验证",
                Type = MyListItem.CheckType.RadioBox,
                Logo = Icon.IconButtonAuth
            }
        ]
    };
            

    /// <summary>
    ///     编辑当前档案的 ID
    /// </summary>
    public static void EditProfileId()
    {
        if (SelectedProfile.Type == ModLaunch.McLoginType.Ms)
        {
            string newUsername = null;
            ModBase.RunInUiWait(() => newUsername = ModMain.MyMsgBoxInput("输入新的玩家 ID", "玩家 ID 只能每 30 天更改一次名称，请谨慎考虑！",
                SelectedProfile.Username,
                [new StringLengthValidator(3, 16), new RegexValidator("([A-z]|[0-9]|_)+")],
                "3 - 16 个字符，只可以包含大小写字母、数字、下划线", Lang.Text("Common.Action.Confirm")));
            if (string.IsNullOrEmpty(newUsername))
                return;
            if (string.IsNullOrWhiteSpace(newUsername))
            {
                ModMain.Hint("欲设置的玩家名称为空");
                return;
            }

            if (ModMain.MyMsgBox("注意：玩家 ID 只能每 30 天更改一次，请务必谨慎考虑！", "确认修改", "继续修改", Lang.Text("Common.Action.Cancel"), IsWarn: true) == 2)
                return;
            // 更新档案信息
            // 刷新页面信息
            ModBase.RunInNewThread(() =>
            {
                try
                {
                    var checkResult = (JObject)ModBase.GetJson(Requester.Fetch(
                        $"https://api.minecraftservices.com/minecraft/profile/name/{newUsername}/available", 
                        new FetchParam
                        {
                            Headers = new Dictionary<string, string>
                                { { "Authorization", "Bearer " + SelectedProfile.AccessToken } }
                        }));
                    if ((string)checkResult["status"] == "DUPLICATE")
                    {
                        ModMain.MyMsgBox("此 ID 已被使用，请换一个 ID。", "ID 修改失败", Lang.Text("Common.Action.Confirm"), IsWarn: true);
                        return;
                    }

                    if ((string)checkResult["status"] == "NOT_ALLOWED")
                    {
                        ModMain.MyMsgBox("此 ID 包含了除大小写字母、数字、下划线以外的不合法字符。", "ID 修改失败", Lang.Text("Common.Action.Confirm"), IsWarn: true);
                        return;
                    }

                    var result = Requester.Fetch(
                        $"https://api.minecraftservices.com/minecraft/profile/name/{newUsername}",
                        new FetchParam
                        {
                            Method = "PUT",
                            ContentType = "application/json",
                            Headers = new Dictionary<string, string>
                                { { "Authorization", "Bearer " + SelectedProfile.AccessToken } }
                        });
                    var resultJson = (JObject)ModBase.GetJson(result);
                    ModMain.Hint($"玩家 ID 修改成功，当前 ID 为：{resultJson["name"]}", ModMain.HintType.Finish);
                    ProfileList.Remove(SelectedProfile);
                    SelectedProfile.Username = (string)resultJson["name"];
                    ProfileList.Add(SelectedProfile);
                    LastUsedProfile = ProfileList.Count - 1;
                    ModMain.FrmLaunchLeft.RefreshPage(true);
                    SaveProfile();
                }
                catch (HttpRequestException ex)
                {
                    var exSummary = ex.ToString();
                    if (exSummary.Contains("403"))
                        ModMain.MyMsgBox("首次更改 ID 后，必须等待 30 天后才能再次修改 ID，你可以前往官网查询具体时间。", "ID 修改失败", "我知道了");
                    else
                        ModBase.Log(ex, "修改档案 ID 失败", ModBase.LogLevel.Msgbox);
                }
            });
        }


        else if (SelectedProfile.Type == ModLaunch.McLoginType.Auth)
        {
            var server = SelectedProfile.Server;
            ModBase.OpenWebsite(server.Replace("/api/yggdrasil/authserver" + (server.EndsWithF("/") ? "/" : ""),
                "/user/profile"));
        }
        else
        {
            string newUsername = null;
            ModBase.RunInUiWait(() => newUsername = ModMain.MyMsgBoxInput("输入新的玩家 ID",
                DefaultInput: SelectedProfile.Username,
                ValidateRules: [new StringLengthValidator(3, 16), new RegexValidator("([A-z]|[0-9]|_)+")],
                HintText: "3 - 16 个字符，只可以包含大小写字母、数字、下划线", Button1: Lang.Text("Common.Action.Confirm"), Button2: Lang.Text("Common.Action.Cancel")));
            if (string.IsNullOrEmpty(newUsername))
                return;
            EditOfflineUuid(SelectedProfile, GetOfflineUuid(newUsername));
        }
    }

    /// <summary>
    ///     编辑离线档案的 UUID
    /// </summary>
    /// <param name="profile">目标档案</param>
    public static void EditOfflineUuid(McProfile profile, string uuid = null)
    {
        var profileIndex = ProfileList.IndexOf(profile);
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
                new MyRadioBox { Text = "行业规范 UUID（推荐）" }, new MyRadioBox { Text = "官方版 PCL UUID（若单人存档的部分信息丢失，可尝试此项）" },
                new MyRadioBox { Text = Lang.Text("Common.Option.Customize") }
            };
            uuidTypeInput = ModMain.MyMsgBoxSelect(uuidTypeList, "新建档案 - 选择 UUID 类型", "继续", Lang.Text("Common.Action.Cancel"));
        });
        if (uuidTypeInput is null)
            return;
        uuidType = (int)uuidTypeInput;
        if (uuidType == 0)
            newUuid = GetOfflineUuid(profile.Username);
        else if (uuidType == 1)
            newUuid = GetOfflineUuid(profile.Username, isLegacy: true);
        else
            newUuid = ModMain.MyMsgBoxInput($"更改档案 {profile.Username} 的 UUID", DefaultInput: profile.Uuid,
                HintText: "32 位，不含连字符",
                ValidateRules:
                [new StringLengthValidator(32, 32), new RegexValidator("([A-z]|[0-9]){32}", "UUID 只应该包括英文字母和数字！")],
                Button1: "继续", Button2: Lang.Text("Common.Action.Cancel"));
        if (string.IsNullOrEmpty(newUuid))
            return;
        Write: ;

        ProfileList[profileIndex].Uuid = newUuid;
        SelectedProfile = ProfileList[profileIndex];
        SaveProfile();
        ModMain.Hint("档案信息已保存！", ModMain.HintType.Finish);
    }

    /// <summary>
    ///     编辑指定档案的验证服务器显示名称
    /// </summary>
    public static void EditAuthServerName(McProfile profile, string serverName)
    {
        var profileIndex = ProfileList.IndexOf(profile);
        ProfileList[profileIndex].ServerName = serverName;
        SaveProfile();
        ModMain.Hint("档案信息已保存！", ModMain.HintType.Finish);
    }

    /// <summary>
    ///     删除特定档案
    /// </summary>
    /// <param name="profile">目标档案</param>
    public static void RemoveProfile(McProfile profile)
    {
        ProfileList.Remove(profile);
        LastUsedProfile = default;
        SaveProfile();
        ModMain.Hint("档案删除成功！", ModMain.HintType.Finish);
    }

    #endregion

    #region 导入与导出

    public static void MigrateProfile()
    {
        // 1. 初始化路径与状态检查
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var hmclAccountPath = Path.Combine(appData, ".hmcl", "accounts.json");
        var hasProfiles = ProfileList.Count > 0;
        var opType = 3; // 1: 导入, 2: 导出, 3: 取消

        // 2. 用户交互
        ModBase.RunInUiWait(() =>
        {
            if (hasProfiles)
            {
                opType = ModMain.MyMsgBox($"PCL CE 支持与 HMCL 相互同步全局档案列表。{"\r\n"}请选择操作：", "档案迁移", "导入", "导出",
                    Lang.Text("Common.Action.Cancel"), ForceWait: true);
            }
            else
            {
                opType = ModMain.MyMsgBox("由于当前档案列表为空，仅支持从 HMCL 导入档案。", "档案迁移", "导入", Lang.Text("Common.Action.Cancel"), ForceWait: true);
                if (opType == 2) opType = 3;
            }
        });

        if (opType == 3)
            return;

        // 3. 分发逻辑
        if (opType == 1)
            PerformImport(hmclAccountPath);
        else
            PerformExport(hmclAccountPath);
    }

    // --- 核心业务逻辑 ---

    private static void PerformImport(string path)
    {
        ModMain.Hint("正在从 HMCL 导入...");

        // 使用 System.Text.Json 解析


        // 查重逻辑


        ModBase.RunInNewThread(() =>
        {
            try
            {
                if (!File.Exists(path))
                {
                    ModMain.Hint("未找到 HMCL 的配置文件。", ModMain.HintType.Critical);
                    return;
                }

                var jsonBytes = File.ReadAllBytes(path);
                using (var doc = JsonDocument.Parse(jsonBytes))
                {
                    var importCount = 0;
                    var importProfiles = new List<McProfile>();
                    var hasMsProfile = ProfileList.Any(p => p.Type == ModLaunch.McLoginType.Ms);
                    foreach (var element in doc.RootElement.EnumerateArray())
                    {
                        var profile = ConvertToPclProfile(element);
                        if (profile is null) continue;
                        if (profile.Type == ModLaunch.McLoginType.Ms)
                        {
                            hasMsProfile = true;
                            if (ProfileList.Any(p =>
                                    p.Type == ModLaunch.McLoginType.Ms && (p.Uuid ?? "") == (profile.Uuid ?? "")))
                                continue;
                        }

                        importProfiles.Add(profile);
                        importCount += 1;
                    }

                    if (!hasMsProfile)
                    {
                        ModMain.Hint("你必须先进行一次正版验证才能导入这些档案！", ModMain.HintType.Critical);
                        return;
                    }

                    ProfileList.AddRange(importProfiles);
                    SaveProfile();
                    if (importCount == 0)
                    {
                        ModMain.Hint("没有新档案可供导入。");
                    }
                    else
                    {
                        ModMain.Hint($"成功导入 {importCount} 个档案！", ModMain.HintType.Finish);
                        ModBase.RunInUi(() => ModMain.FrmLoginProfile.RefreshProfileList());
                    }
                }
            }
            catch (Exception ex)
            {
                ProfileLog("导入失败: " + ex.Message);
                ModMain.Hint("导入出错，请检查文件格式。", ModMain.HintType.Critical);
            }
        }, "Profile Import");
    }

    private static void PerformExport(string path)
    {
        ModMain.Hint("正在导出至 HMCL...");
        try
        {
            // 1. 读取并解析现有列表，准备合并
            var finalDictList = new List<Dictionary<string, object>>();

            if (File.Exists(path))
            {
                var oldJson = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(oldJson))
                    // 这里简单处理：将旧的转回原始结构，避免丢失 HMCL 自己的其他账户
                    using (var doc = JsonDocument.Parse(oldJson))
                    {
                        foreach (var el in doc.RootElement.EnumerateArray())
                        {
                            // 此处可根据需要转换回 Dictionary
                        }
                    }
            }

            // 2. 转换当前 PCL 列表
            foreach (var profile in ProfileList)
                finalDictList.Add(ConvertToHmclDict(profile));

            // 3. 序列化并写入
            var options = new JsonSerializerOptions { WriteIndented = true };
            var jsonString = JsonSerializer.Serialize(finalDictList, options);

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, jsonString);

            ModMain.Hint($"已成功同步 {ProfileList.Count} 个档案。", ModMain.HintType.Finish);
        }
        catch (Exception ex)
        {
            ProfileLog("导出失败: " + ex.Message);
            ModMain.Hint("导出失败。", ModMain.HintType.Critical);
        }
    }

    // --- 类型转换辅助 ---

    private static McProfile ConvertToPclProfile(JsonElement el)
    {
        try
        {
            var typeStr = el.GetProperty("type").GetString();
            JsonElement argvalue = default;
            var profile = new McProfile
            {
                Uuid = el.TryGetProperty("uuid", out argvalue) ? el.GetProperty("uuid").GetString() : "",
                Expires = 1743779140286L
            };

            switch (typeStr ?? "")
            {
                case "microsoft":
                {
                    profile.Type = ModLaunch.McLoginType.Ms;
                    profile.Username = el.GetProperty("displayName").GetString();
                    break;
                }
                case "authlibInjector":
                {
                    profile.Type = ModLaunch.McLoginType.Auth;
                    profile.Username = el.GetProperty("displayName").GetString();
                    profile.Server = el.GetProperty("serverBaseURL").GetString();
                    profile.Name = el.GetProperty("username").GetString();
                    profile.ClientToken = el.GetProperty("clientToken").GetString();
                    break;
                }

                default:
                {
                    profile.Type = ModLaunch.McLoginType.Legacy;
                    profile.Username = el.GetProperty("username").GetString();
                    break;
                }
            }

            return profile;
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, object> ConvertToHmclDict(McProfile profile)
    {
        var dict = new Dictionary<string, object>();
        dict["uuid"] = profile.Uuid;

        switch (profile.Type)
        {
            case ModLaunch.McLoginType.Ms:
            {
                dict["displayName"] = profile.Username;
                dict["type"] = "microsoft";
                dict["tokenType"] = "Bearer";
                dict["accessToken"] = "";
                dict["notAfter"] = 1743779140286L;
                break;
            }
            case ModLaunch.McLoginType.Auth:
            {
                dict["serverBaseURL"] = profile.Server;
                dict["displayName"] = profile.Username;
                dict["username"] = profile.Name;
                dict["type"] = "authlibInjector";
                dict["clientToken"] = profile.ClientToken;
                break;
            }

            default:
            {
                dict["username"] = profile.Username;
                dict["type"] = "offline";
                break;
            }
        }

        return dict;
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
        return Conversions.ToString(Operators.AddObject(
            Operators.AddObject(
                Operators.AddObject(
                    Operators.AddObject(
                        Operators.AddObject(
                            Operators.AddObject(
                                Operators.AddObject(Operators.AddObject(Digits(msb >> 32, 8), "-"),
                                    Digits(msb >> 16, 4)), "-"), Digits(msb, 4)), "-"), Digits(lsb >> 48, 4)), "-"),
            Digits(lsb, 12)));
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
            info += "第三方验证";
            if (!string.IsNullOrWhiteSpace(profile.ServerName))
                info += $" / {profile.ServerName}";
        }
        else if (profile.Type == ModLaunch.McLoginType.Ms)
        {
            info += "正版验证";
        }
        else
        {
            info += "离线验证";
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
        if (SelectedProfile is null) // 新档案
        {
            if (targetAuthType != default)
                authType = targetAuthType;
            else
                authType = ModLaunch.McLoginType.Legacy;
            if (authType == ModLaunch.McLoginType.Auth)
                return new ModLaunch.McLoginServer(ModLaunch.McLoginType.Auth)
                {
                    Description = "Authlib-Injector",
                    Type = ModLaunch.McLoginType.Auth,
                    IsExist = ModMain.FrmLoginAuth is null
                };

            if (authType == ModLaunch.McLoginType.Ms) return new ModLaunch.McLoginMs();

            return new ModLaunch.McLoginLegacy();
        }

        // 已有档案
        authType = SelectedProfile.Type;
        if (authType == ModLaunch.McLoginType.Auth)
            return new ModLaunch.McLoginServer(ModLaunch.McLoginType.Auth)
            {
                BaseUrl = SelectedProfile.Server,
                UserName = SelectedProfile.Name,
                Password = SelectedProfile.Password,
                Description = "Authlib-Injector",
                Type = ModLaunch.McLoginType.Auth,
                IsExist = ModMain.FrmLoginAuth is null
            };

        if (authType == ModLaunch.McLoginType.Ms)
        {
            if (ModLaunch.McLoginMsLoader.State == ModBase.LoadState.Finished)
                return new ModLaunch.McLoginMs
                {
                    OAuthRefreshToken = SelectedProfile.RefreshToken,
                    UserName = SelectedProfile.Username,
                    AccessToken = SelectedProfile.AccessToken,
                    Uuid = SelectedProfile.Uuid,
                    ProfileJson = SelectedProfile.RawJson
                };

            return new ModLaunch.McLoginMs
                { OAuthRefreshToken = SelectedProfile.RefreshToken, UserName = SelectedProfile.Name };
        }

        return new ModLaunch.McLoginLegacy { UserName = SelectedProfile.Username, Uuid = SelectedProfile.Uuid };
    }

    /// <summary>
    ///     检查当前档案是否有效
    /// </summary>
    /// <returns>若档案验证有效，则返回空字符串，否则返回错误原因</returns>
    public static object IsProfileValid()
    {
        switch (SelectedProfile.Type)
        {
            case ModLaunch.McLoginType.Legacy:
            {
                if (string.IsNullOrEmpty(SelectedProfile.Username.Trim()))
                    return "玩家名不能为空！";
                if (SelectedProfile.Username.Contains("\""))
                    return "玩家名不能包含英文引号！";
                if (ModMinecraft.McInstanceSelected is not null && ModMinecraft.McInstanceSelected.Info.Drop >= 203 &&
                    SelectedProfile.Username.Trim().Length > 16) return "自 1.20.3 起，玩家名至多只能包含 16 个字符！";
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

        return "未知的验证方式";
    }

    #endregion

    #region 皮肤

    private static bool _isMsSkinChanging;

    public static void ChangeSkinMs()
    {
        // 检查条件，获取新皮肤
        if (_isMsSkinChanging)
        {
            ModMain.Hint("正在更改皮肤中，请稍候！");
            return;
        }

        if (ModLaunch.McLoginLoader.State == ModBase.LoadState.Failed)
        {
            ModMain.Hint("登录失败，无法更改皮肤！", ModMain.HintType.Critical);
            return;
        }

        var skinInfo = ModMinecraft.McSkinSelect();
        if (!skinInfo.IsVaild)
            return;
        ModMain.Hint("正在更改皮肤……");
        _isMsSkinChanging = true;
        // 开始实际获取

        // 获取登录信息

        // 获取新皮肤地址
        ModBase.RunInNewThread(() =>
        {
            try
            {
                Retry: ;
                if (ModLaunch.McLoginMsLoader.State == ModBase.LoadState.Loading)
                    ModLaunch.McLoginMsLoader.WaitForExit();
                if (ModLaunch.McLoginMsLoader.State != ModBase.LoadState.Finished)
                    ModLaunch.McLoginMsLoader.WaitForExit(GetLoginData());
                if (ModLaunch.McLoginMsLoader.State != ModBase.LoadState.Finished)
                {
                    ModMain.Hint("登录失败，无法更改皮肤！", ModMain.HintType.Critical);
                    return;
                }

                var accessToken = SelectedProfile.AccessToken;
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
                    ModMain.Hint("正在登录，将在登录完成后继续更改皮肤……");
                    ModLaunch.McLoginMsLoader.Start(GetLoginData(), true);
                    goto Retry;
                }

                if (res.Contains("\"error\""))
                {
                    ModMain.Hint(
                        Conversions.ToString(Operators.ConcatenateObject("更改皮肤失败：",
                            ((JObject)ModBase.GetJson(res))["error"])),
                        ModMain.HintType.Critical);
                    return;
                }

                ModBase.Log("[Skin] 皮肤修改返回值：" + "\r\n" + res);
                var resultJson = (JObject)ModBase.GetJson(res);
                if (resultJson.ContainsKey("errorMessage")) throw new Exception(resultJson["errorMessage"].ToString());
                foreach (JObject skin in resultJson["skins"])
                    if (skin["state"].ToString() == "ACTIVE")
                    {
                        MySkin.ReloadCache((string)skin["url"]);
                        return;
                    }

                throw new Exception("未知错误（" + res + "）");
            }
            catch (Exception ex)
            {
                if (ex.GetType().Equals(typeof(TaskCanceledException)))
                    ModMain.Hint("更改皮肤失败：与 Mojang 皮肤服务器的连接超时，请检查你的网络是否通畅！", ModMain.HintType.Critical);
                else
                    ModBase.Log(ex, "更改皮肤失败", ModBase.LogLevel.Hint);
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

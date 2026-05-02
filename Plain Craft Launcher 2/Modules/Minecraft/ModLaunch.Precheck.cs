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
    #region 预检测

    private static void McLaunchPrecheck()
    {
        if (Conversions.ToBoolean(Config.Debug.AddRandomDelay))
            Thread.Sleep(RandomUtils.NextInt(100, 2000));
        // 检查路径
        if (ModMinecraft.McInstanceSelected.PathIndie.Contains("!") ||
            ModMinecraft.McInstanceSelected.PathIndie.Contains(";"))
            throw new Exception("游戏路径中不可包含 ! 或 ;（" + ModMinecraft.McInstanceSelected.PathIndie + "）");
        if (ModMinecraft.McInstanceSelected.PathInstance.Contains("!") ||
            ModMinecraft.McInstanceSelected.PathInstance.Contains(";"))
            throw new Exception("游戏路径中不可包含 ! 或 ;（" + ModMinecraft.McInstanceSelected.PathInstance + "）");
        if (Conversions.ToBoolean(ModBase.IsUtf8CodePage() && !(bool)States.Hint.NonAsciiGamePath &&
                                  !ModMinecraft.McInstanceSelected.PathInstance.IsASCII()))
        {
            var userChoice = ModMain.MyMsgBox(
                $"欲启动实例 \"{ModMinecraft.McInstanceSelected.Name}\" 的路径中存在可能影响游戏正常运行的字符（非 ASCII 字符），是否仍旧启动游戏？{"\r\n"}{"\r\n"}如果不清楚具体作用，你可以先选择 \"继续\"，发现游戏在启动后很快出现崩溃的情况后再尝试修改游戏路径等操作",
                "游戏路径检查", "继续", "返回处理", "不再提示");
            if (userChoice == 2) throw new Exception("$$");
            if (userChoice == 3) States.Hint.NonAsciiGamePath = true;
        }

        // 检查实例
        if (ModMinecraft.McInstanceSelected is null)
            throw new Exception("未选择 Minecraft 实例！");
        ModMinecraft.McInstanceSelected.Load();
        if (ModMinecraft.McInstanceSelected.State == ModMinecraft.McInstanceState.Error)
            throw new Exception("Minecraft 存在问题：" + ModMinecraft.McInstanceSelected.Desc);
        // 检查输入信息
        var CheckResult = "";
        ModBase.RunInUiWait(() => CheckResult = Conversions.ToString(ModProfile.IsProfileValid()));
        if (ModProfile.SelectedProfile is null) // 没选档案
        {
            CheckResult = "请先选择一个档案再启动游戏！";
        }
        else if (ModMinecraft.McInstanceSelected.Info.HasLabyMod || Conversions.ToBoolean(
                     Operators.ConditionalCompareObjectEqual(
                         ModBase.Setup.Get("VersionServerLoginRequire", ModMinecraft.McInstanceSelected), 1,
                         false))) // 要求正版验证
        {
            if (!(ModProfile.SelectedProfile.Type == McLoginType.Ms)) CheckResult = "当前实例要求使用正版验证，请使用正版验证档案启动游戏！";
        }
        else if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(
                     ModBase.Setup.Get("VersionServerLoginRequire", ModMinecraft.McInstanceSelected), 2,
                     false))) // 要求第三方验证
        {
            if (!(ModProfile.SelectedProfile.Type == McLoginType.Auth))
                CheckResult = "当前实例要求使用第三方验证，请使用第三方验证档案启动游戏！";
            else if (Conversions.ToBoolean(!Operators.ConditionalCompareObjectEqual(
                         ModProfile.SelectedProfile.Server.BeforeLast("/authserver"),
                         ModBase.Setup.Get("VersionServerAuthServer", ModMinecraft.McInstanceSelected), false)))
                CheckResult = "当前档案使用的第三方验证服务器与实例要求使用的不一致，请使用符合要求的档案启动游戏！";
        }
        else if (Conversions.ToBoolean(Operators.ConditionalCompareObjectEqual(
                     ModBase.Setup.Get("VersionServerLoginRequire", ModMinecraft.McInstanceSelected), 3,
                     false))) // 要求正版验证或第三方验证
        {
            if (ModProfile.SelectedProfile.Type == McLoginType.Legacy)
                CheckResult = "当前实例要求使用正版验证或第三方验证，请使用符合要求的档案启动游戏！";
            else if (Conversions.ToBoolean(ModProfile.SelectedProfile.Type == McLoginType.Auth &&
                                           !Operators.ConditionalCompareObjectEqual(
                                               ModProfile.SelectedProfile.Server.BeforeLast("/authserver"),
                                               ModBase.Setup.Get("VersionServerAuthServer",
                                                   ModMinecraft.McInstanceSelected), false)))
                CheckResult = "当前档案使用的第三方验证服务器与实例要求使用的不一致，请使用符合要求的档案启动游戏！";
        }

        if (!string.IsNullOrEmpty(CheckResult))
            throw new ArgumentException(CheckResult);

#if BETA
        if (CurrentLaunchOptions?.SaveBatch == null) // 保存脚本时不提示
            {
                RunInNewThread(() =>
                {
                    switch ((int)States.System.LaunchCount)
                    {
                        case 10:
                        case 20:
                        case 40:
                        case 60:
                        case 80:
                        case 100:
                        case 120:
                        case 150:
                        case 200:
                        case 250:
                        case 300:
                        case 350:
                        case 400:
                        case 500:
                        case 600:
                        case 700:
                        case 800:
                        case 900:
                        case 1000:
                        case 1200:
                        case 1400:
                        case 1600:
                        case 1800:
                        case 2000:
                            if (ModMain.MyMsgBox(
                                    "PCL 已经为你启动了 " + Setup.Get("SystemLaunchCount") + " 次游戏啦！\n" +
                                    "如果 PCL 还算好用的话，也许可以考虑赞助一下 PCL 原作者……\n" +
                                    "如果没有大家的支持，PCL 很难在免费、无任何广告的情况下维持数年的更新（磕头）……！",
                                    Setup.Get("SystemLaunchCount") + " 次启动！",
                                    "支持一下！",
                                    "但是我拒绝") == 1)
                            {
                                OpenWebsite("https://afdian.com/a/LTCat");
                            }
                            break;
                    }
                }, "Donate");
            }
#endif

        // 正版购买提示
        if (!ModProfile.ProfileList.Any(x => x.Type == McLoginType.Ms))
        {
            if (RegionUtils.IsRestrictedFeatAllowed)
            {
                if (ModMain.MyMsgBox(
                        $"看起来你似乎没买正版...{"\r\n"}如果觉得 Minecraft 还不错，可以购买正版支持一下，毕竟开发游戏也真的很不容易...不要一直白嫖啦。{"\r\n"}{"\r\n"}在验证一个正版账号之后，就不会出现这个提示了！",
                        "考虑一下正版？", "支持正版游戏！", "下次一定") ==
                    1)
                    ModBase.OpenWebsite(
                        "https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj");
            }
            else
            {
                switch (ModMain.MyMsgBox("你必须先登录正版账号才能启动游戏！", "正版验证", "购买正版", "试玩", "返回",
                            Button1Action: () =>
                                ModBase.OpenWebsite(
                                    "https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj")))
                {
                    case 2:
                    {
                        ModMain.Hint("游戏将以试玩模式启动！", ModMain.HintType.Critical);
                        CurrentLaunchOptions.ExtraArgs.Add("--demo");
                        break;
                    }
                    case 3:
                    {
                        throw new Exception("$$");
                    }
                }
            }
        }
    }

    #endregion

    #region 档案验证

    #region 主模块

    // 登录方式
    public enum McLoginType
    {
        Legacy = 1,
        Auth = 2,
        Ms = 3
    }

    // 各个登录方式的对应数据
    public abstract partial class McLoginData
    {
        /// <summary>
        ///     登录方式。
        /// </summary>
        public McLoginType Type;

        public override bool Equals(object obj)
        {
            return obj is not null && obj.GetHashCode() == GetHashCode();
        }
    }

    #region 第三方验证类型

    public partial class McLoginServer : McLoginData
    {
        /// <summary>
        ///     登录服务器基础地址。
        /// </summary>
        public string BaseUrl;

        /// <summary>
        ///     登录方式的描述字符串，如 “正版”、“统一通行证”。
        /// </summary>
        public string Description;

        /// <summary>
        ///     是否在本次登录中强制要求玩家重新选择角色，目前仅对 Authlib-Injector 生效。
        /// </summary>
        public bool ForceReselectProfile = false;

        /// <summary>
        ///     是否已经存在该验证信息，用于判断是否为新增档案。
        /// </summary>
        public bool IsExist = false;

        /// <summary>
        ///     登录密码。
        /// </summary>
        public string Password;

        /// <summary>
        ///     登录用户名。
        /// </summary>
        public string UserName;

        public McLoginServer(McLoginType Type)
        {
            this.Type = Type;
        }

        public override int GetHashCode()
        {
            return (int)Math.Round(ModBase.GetHash(UserName + Password + BaseUrl + (int)Type) %
                                   (decimal)int.MaxValue);
        }
    }

    #endregion

    #region 正版验证类型

    public partial class McLoginMs : McLoginData
    {
        public string AccessToken = "";

        /// <summary>
        ///     缓存的 OAuth RefreshToken。若没有则为空字符串。
        /// </summary>
        public string OAuthRefreshToken = "";

        public string ProfileJson = "";
        public string UserName = "";
        public string Uuid = "";

        public McLoginMs()
        {
            Type = McLoginType.Ms;
        }

        public override int GetHashCode()
        {
            return (int)Math.Round(ModBase.GetHash(OAuthRefreshToken + AccessToken + Uuid + UserName + ProfileJson) %
                                   (decimal)int.MaxValue);
        }
    }

    #endregion

    #region 离线验证类型

    public partial class McLoginLegacy : McLoginData
    {
        /// <summary>
        ///     若采用正版皮肤，则为该皮肤名。
        /// </summary>
        public string SkinName;

        /// <summary>
        ///     皮肤种类。
        /// </summary>
        public int SkinType;

        /// <summary>
        ///     登录用户名。
        /// </summary>
        public string UserName;

        /// <summary>
        ///     UUID。
        /// </summary>
        public string Uuid;

        public McLoginLegacy()
        {
            Type = McLoginType.Legacy;
        }

        public override int GetHashCode()
        {
            return (int)Math.Round(
                ModBase.GetHash(UserName + SkinType + SkinName + (int)Type) % (decimal)int.MaxValue);
        }
    }

    #endregion

    // 登录返回结果
    public partial struct McLoginResult
    {
        public string Name;
        public string Uuid;
        public string AccessToken;
        public string Type;
        public string ClientToken;

        /// <summary>
        ///     进行微软登录时返回的 profile 信息。
        /// </summary>
        public string ProfileJson;
    }

    // 登录主模块加载器
    public static ModLoader.LoaderTask<McLoginData, McLoginResult> McLoginLoader =
        new("登录", McLoginStart, McLoginInput, ThreadPriority.BelowNormal)
            { ReloadTimeout = 1, ProgressWeight = 15d, Block = false };

    public static McLoginData McLoginInput()
    {
        McLoginData LoginData = null;
        try
        {
            LoginData = ModProfile.GetLoginData();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "获取登录输入信息失败", ModBase.LogLevel.Feedback);
        }

        return LoginData;
    }

    private static void McLoginStart(ModLoader.LoaderTask<McLoginData, McLoginResult> Data)
    {
        ModBase.Log("[Profile] 开始加载选定档案");
        // 校验登录信息
        var CheckResult = Conversions.ToString(ModProfile.IsProfileValid());
        if (!string.IsNullOrEmpty(CheckResult))
            throw new ArgumentException(CheckResult);
        // 获取对应加载器
        ModLoader.LoaderBase Loader = null;
        switch (Data.Input.Type)
        {
            case McLoginType.Ms:
            {
                Loader = McLoginMsLoader;
                break;
            }
            case McLoginType.Legacy:
            {
                Loader = McLoginLegacyLoader;
                break;
            }
            case McLoginType.Auth:
            {
                Loader = McLoginAuthLoader;
                break;
            }
        }

        // 尝试加载
        Loader.WaitForExit(Data.Input, McLoginLoader, Data.IsForceRestarting);
        Data.Output = (McLoginResult)((dynamic)Loader).Output;
        ModBase.RunInUi(() => ModMain.FrmLaunchLeft.RefreshPage(false)); // 刷新自动填充列表
        ModBase.Log("[Profile] 选定档案加载完成");
    }

    #endregion

    // 各个登录方式的主对象与输入构造
    public static ModLoader.LoaderTask<McLoginMs, McLoginResult> McLoginMsLoader =
        new("Loader Login Ms", McLoginMsStart) { ReloadTimeout = 1 };

    public static ModLoader.LoaderTask<McLoginLegacy, McLoginResult> McLoginLegacyLoader =
        new("Loader Login Legacy", McLoginLegacyStart);

    public static ModLoader.LoaderTask<McLoginServer, McLoginResult> McLoginAuthLoader =
        new("Loader Login Auth", McLoginServerStart) { ReloadTimeout = 1000 * 60 * 10 };

    // 主加载函数，返回所有需要的登录信息
    private static long McLoginMsRefreshTime; // 上次刷新登录的时间

    #region 正版验证

    private static void McLoginMsStart(ModLoader.LoaderTask<McLoginMs, McLoginResult> data)
    {
        var input = data.Input;
        var logUsername = input.UserName;
        var isNewProfile = true;

        ModProfile.ProfileLog($"验证方式：正版（{(string.IsNullOrEmpty(logUsername) ? "尚未登录" : logUsername)}）");
        data.Progress = 0.05d;

        // 已登录且不需要强制重启且登录未过期
        if (!data.IsForceRestarting && !string.IsNullOrEmpty(input.AccessToken) &&
            McLoginMsRefreshTime > 0L &&
            TimeUtils.GetTimeTick() - McLoginMsRefreshTime < 1000 * 60 * 10)
        {
            data.Output = new McLoginResult
            {
                AccessToken = input.AccessToken,
                Name = input.UserName,
                Uuid = input.Uuid,
                Type = "Microsoft",
                ClientToken = input.Uuid,
                ProfileJson = input.ProfileJson
            };

            McLoginMsRefreshTime = TimeUtils.GetTimeTick();
            ModProfile.ProfileLog("正版验证完成");
            return;
        }

        data.Progress = 0.1d;

        // 尝试获取 OAuthToken
        var oauthTokens = GetOAuthTokens(data, input, out var skipAuth);
        if (skipAuth)
        {
            data.Progress = 0.99d;
            var profile = ModProfile.SelectedProfile;
            data.Output = new McLoginResult
            {
                AccessToken = profile.AccessToken,
                Name = profile.Username,
                Uuid = profile.Uuid,
                Type = "Microsoft"
            };
            return;
        }

        var oauthAccessToken = oauthTokens[0];
        var oauthRefreshToken = oauthTokens[1];
        ThrowIfAborted(data);

        data.Progress = 0.25d;

        // Step 2: XBL Token
        var xblToken = MsLoginStep2(oauthAccessToken);
        if (string.IsNullOrEmpty(xblToken) || xblToken == "Ignore")
            goto SkipLogin;

        data.Progress = 0.4d;
        ThrowIfAborted(data);

        // Step 3: XSTS / Minecraft login
        var tokens = MsLoginStep3(xblToken);
        if (tokens.Length < 2 || tokens[1] == "Ignore")
            goto SkipLogin;

        data.Progress = 0.55d;
        ThrowIfAborted(data);

        // Step 4: Final access token
        var accessToken = MsLoginStep4(tokens);
        if (string.IsNullOrEmpty(accessToken) || accessToken == "Ignore")
            goto SkipLogin;

        data.Progress = 0.7d;
        ThrowIfAborted(data);

        // Step 5: Additional setup
        MsLoginStep5(accessToken);
        data.Progress = 0.85d;
        ThrowIfAborted(data);

        // Step 6: Profile info
        var result = MsLoginStep6(accessToken);
        if (result.Length < 3 || result[2] == "Ignore")
            goto SkipLogin;

        data.Progress = 0.98d;

        // 检查是否已有相同档案
        foreach (var profile in ModProfile.ProfileList)
            if (profile.Type == McLoginType.Ms &&
                string.Equals(profile.Username, result[1], StringComparison.Ordinal) &&
                string.Equals(profile.Uuid, result[0], StringComparison.Ordinal))
            {
                isNewProfile = false;
                if (ModProfile.IsCreatingProfile)
                {
                    var index = ModProfile.ProfileList.IndexOf(profile);
                    ModProfile.ProfileList[index].Username = result[1];
                    ModProfile.ProfileList[index].AccessToken = accessToken;
                    ModProfile.ProfileList[index].RefreshToken = oauthRefreshToken;
                    ModMain.Hint("你已经添加了这个档案...");
                    goto SkipLogin;
                }
            }

        // 输出登录结果
        if (isNewProfile)
        {
            var newProfile = new ModProfile.McProfile
            {
                Type = McLoginType.Ms,
                Uuid = result[0],
                Username = result[1],
                AccessToken = accessToken,
                RefreshToken = oauthRefreshToken,
                Expires = 1743779140286L,
                Desc = "",
                RawJson = result[2]
            };
            ModProfile.ProfileList.Add(newProfile);
            ModProfile.SelectedProfile = newProfile;
            ModProfile.IsCreatingProfile = false;
        }
        else
        {
            var index = ModProfile.ProfileList.IndexOf(ModProfile.SelectedProfile);
            ModProfile.ProfileList[index].Username = result[1];
            ModProfile.ProfileList[index].AccessToken = accessToken;
            ModProfile.ProfileList[index].RefreshToken = oauthRefreshToken;
        }

        ModProfile.SaveProfile();

        data.Output = new McLoginResult
        {
            AccessToken = accessToken,
            Name = result[1],
            Uuid = result[0],
            Type = "Microsoft",
            ClientToken = result[0],
            ProfileJson = result[2]
        };

        SkipLogin:
        McLoginMsRefreshTime = TimeUtils.GetTimeTick();
        ModProfile.ProfileLog("正版验证完成");
    }

    /// <summary>
    ///     获取 OAuth Tokens，处理刷新和重新登录逻辑
    /// </summary>
    private static string[] GetOAuthTokens(ModLoader.LoaderTask<McLoginMs, McLoginResult> data, McLoginMs input,
        out bool skipAuth)
    {
        skipAuth = false;
        string[] tokens;

        while (true)
        {
            if (string.IsNullOrEmpty(input.OAuthRefreshToken))
            {
                tokens = MsLoginStep1New(data);
            }
            else
            {
                tokens = MsLoginStep1Refresh(input.OAuthRefreshToken);
                if (tokens.Length > 0 && tokens[0] == "Relogin")
                    continue; // 重新登录
            }

            if (tokens.Length > 0 && tokens[0] == "Ignore")
            {
                skipAuth = true;
                return tokens;
            }

            return tokens;
        }
    }

    /// <summary>
    ///     检查是否被中断
    /// </summary>
    private static void ThrowIfAborted(ModLoader.LoaderTask<McLoginMs, McLoginResult> data)
    {
        if (data.IsAborted)
            throw new ThreadInterruptedException();
    }

    /// <summary>
    ///     正版验证步骤 1：通过设备代码流获取账号信息
    /// </summary>
    /// <returns>OAuth 验证完成的返回结果</returns>
    private static string[] MsLoginStep1New(ModLoader.LoaderTask<McLoginMs, McLoginResult> Data)
    {
        // 参考：https://learn.microsoft.com/zh-cn/entra/identity-platform/v2-oauth2-device-code

        // 初始请求
        Retry: ;

        McLaunchLog("开始正版验证 Step 1/6（原始登录）");
        JObject PrepareJson;
        var parameters = new Dictionary<string, string>
        {
            { "client_id", ModSecret.OAuthClientId },
            { "tenant", "/consumers" },
            { "scope", "XboxLive.signin offline_access" }
        };

        using (var response = HttpRequest
                   .CreatePost("https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode")
                   .WithFormContent(parameters)
                   .SendAsync()
                   .GetAwaiter()
                   .GetResult())
        {
            response.EnsureSuccessStatusCode();
            PrepareJson = (JObject)ModBase.GetJson(response.AsString());
        }

        McLaunchLog("网页登录地址：" + PrepareJson["verification_uri"]);

        // 弹窗
        var Converter = new ModMain.MyMsgBoxConverter
            { Content = PrepareJson, ForceWait = true, Type = ModMain.MyMsgBoxType.Login };
        ModMain.WaitingMyMsgBox.Add(Converter);
        while (Converter.Result is null)
            Thread.Sleep(100);
        if (Converter.Result is ModBase.RestartException)
        {
            if (ModMain.MyMsgBox(
                    $"请在登录时选择 {ModBase.vbLQ}其他登录方法{ModBase.vbRQ}，然后选择 {ModBase.vbLQ}使用我的密码{ModBase.vbRQ}。{"\r\n"}如果没有该选项，请选择 {ModBase.vbLQ}设置密码{ModBase.vbRQ}，设置完毕后再登录。",
                    "需要使用密码登录", "重新登录", "设置密码", "取消",
                    Button2Action: () => ModBase.OpenWebsite("https://account.live.com/password/Change")) ==
                1) goto Retry;

            throw new Exception("$$");
        }

        if (Converter.Result is Exception) throw (Exception)Converter.Result;

        return (string[])Converter.Result;
    }

    /// <summary>
    ///     正版验证步骤 1，刷新登录：从 OAuth Code 或 OAuth RefreshToken 获取 {OAuth accessToken, OAuth RefreshToken}
    /// </summary>
    /// <param name="Code"></param>
    /// <returns></returns>
    private static string[] MsLoginStep1Refresh(string Code)
    {
        McLaunchLog("开始正版验证 Step 1/6（刷新登录）");
        if (string.IsNullOrEmpty(Code))
            throw new ArgumentException("传入的 Code 为空", nameof(Code));
        string Result = null;
        try
        {
            var parameters = new Dictionary<string, string>
            {
                { "client_id", ModSecret.OAuthClientId },
                { "refresh_token", Code },
                { "grant_type", "refresh_token" },
                { "scope", "XboxLive.signin offline_access" }
            };

            using (var response = HttpRequest
                       .CreatePost("https://login.live.com/oauth20_token.srf")
                       .WithFormContent(parameters)
                       .SendAsync()
                       .GetAwaiter()
                       .GetResult())
            {
                response.EnsureSuccessStatusCode();
                Result = response.AsString();
            }
        }
        catch (ThreadInterruptedException ex)
        {
            ModBase.Log(ex, "加载线程已终止");
        }
        catch (Exception ex)
        {
            if (ex.Message.ContainsF("must sign in again", true) || ex.Message.ContainsF("password expired", true) ||
                (ex.Message.Contains("refresh_token") && ex.Message.Contains("is not valid"))) // #269
                return new[] { "Relogin", "" };

            ModProfile.ProfileLog("正版验证 Step 1/6 获取 OAuth Token 失败：" + ex);
            var IsIgnore = false;
            ModBase.RunInUiWait(() =>
            {
                if (!IsLaunching)
                    return;
                if (ModMain.MyMsgBox(
                        $"启动器在尝试刷新账号信息时遇到了网络错误。{"\r\n"}你可以选择取消，检查网络后再次启动，也可以选择忽略错误继续启动，但可能无法游玩部分服务器。",
                        "账号信息获取失败", "继续", "取消") == 1)
                    IsIgnore = true;
            });
            if (IsIgnore) return new[] { "Ignore", "" };
        }

        var ResultJson = (JObject)ModBase.GetJson(Result);
        var AccessToken = ResultJson["access_token"].ToString();
        var RefreshToken = ResultJson["refresh_token"].ToString();
        return new[] { AccessToken, RefreshToken };
    }


    private partial class XBLTokenRequestData
    {
        public PropertiesData Properties { get; set; }
        public string RelyingParty { get; set; }
        public string TokenType { get; set; }

        public partial class PropertiesData
        {
            public string AuthMethod { get; set; }
            public string SiteName { get; set; }
            public string RpsTicket { get; set; }
        }
    }

    /// <summary>
    ///     正版验证步骤 2：从 OAuth accessToken 获取 XBLToken
    /// </summary>
    /// <param name="accessToken">OAuth accessToken</param>
    /// <returns>XBLToken</returns>
    private static string MsLoginStep2(string accessToken)
    {
        ModProfile.ProfileLog("开始正版验证 Step 2/6: 获取 XBLToken");
        if (string.IsNullOrEmpty(accessToken))
            throw new ArgumentException("传入的 AccessToken 为空", nameof(accessToken));
        var requestData = new XBLTokenRequestData
        {
            Properties = new XBLTokenRequestData.PropertiesData
            {
                AuthMethod = "RPS",
                SiteName = "user.auth.xboxlive.com",
                RpsTicket = $"d={accessToken}"
            },
            RelyingParty = "http://auth.xboxlive.com",
            TokenType = "JWT"
        };
        string Result = null;
        try
        {
            using (var response = HttpRequest
                       .CreatePost("https://user.auth.xboxlive.com/user/authenticate")
                       .WithJsonContent(requestData)
                       .SendAsync()
                       .GetAwaiter()
                       .GetResult())
            {
                response.EnsureSuccessStatusCode();
                Result = response.AsString();
            }
        }
        catch (Exception ex)
        {
            ModProfile.ProfileLog("正版验证 Step 2/6 获取 XBLToken 失败：" + ex);
            var IsIgnore = false;
            ModBase.RunInUiWait(() =>
            {
                if (!IsLaunching)
                    return;
                if (ModMain.MyMsgBox(
                        $"启动器在尝试刷新账号信息时(Step 2)遇到了网络错误。{"\r\n"}你可以选择取消，检查网络后再次启动，也可以选择忽略错误继续启动，但可能无法游玩部分服务器。",
                        "账号信息获取失败", "继续", "取消") == 1)
                    IsIgnore = true;
            });
            if (IsIgnore) return "Ignore";
        }

        var ResultJson = (JObject)ModBase.GetJson(Result);
        var XBLToken = ResultJson["Token"].ToString();
        return XBLToken;
    }


    private partial class XSTSTokenRequestData
    {
        public PropertiesData Properties { get; set; }
        public string RelyingParty { get; set; }
        public string TokenType { get; set; }

        public partial class PropertiesData
        {
            public string SandboxId { get; set; }
            public List<string> UserTokens { get; set; }
        }
    }

    /// <summary>
    ///     正版验证步骤 3：从 XBLToken 获取 {XSTSToken, UHS}
    /// </summary>
    /// <returns>包含 XSTSToken 与 UHS 的字符串组</returns>
    private static string[] MsLoginStep3(string XBLToken)
    {
        ModProfile.ProfileLog("开始正版验证 Step 3/6: 获取 XSTSToken");
        if (string.IsNullOrEmpty(XBLToken))
            throw new ArgumentException("XBLToken 为空，无法获取数据", nameof(XBLToken));
        var requestData = new XSTSTokenRequestData
        {
            Properties = new XSTSTokenRequestData.PropertiesData
            {
                SandboxId = "RETAIL",
                UserTokens = new[] { XBLToken }.ToList()
            },
            RelyingParty = "rp://api.minecraftservices.com/",
            TokenType = "JWT"
        };
        string result;
        using (var response = HttpRequest
                   .CreatePost("https://xsts.auth.xboxlive.com/xsts/authorize")
                   .WithJsonContent(requestData)
                   .SendAsync()
                   .GetAwaiter()
                   .GetResult())
        {
            result = response.AsString();

            if (!response.IsSuccess)
            {
                // 参考 https://github.com/PrismarineJS/prismarine-auth/blob/master/src/common/Constants.js
                if (result.Contains("2148916227"))
                {
                    ModMain.MyMsgBox("该账号似乎已被微软封禁，无法登录。", "登录失败", "我知道了", IsWarn: true);
                    throw new Exception("$$");
                }

                if (result.Contains("2148916233"))
                {
                    if (ModMain.MyMsgBox("你尚未注册 Xbox 账户，请在注册后再登录。", "登录提示", "注册", "取消") == 1)
                        ModBase.OpenWebsite("https://signup.live.com/signup");
                    throw new Exception("$$");
                }

                if (result.Contains("2148916235"))
                {
                    ModMain.MyMsgBox($"你的网络所在的国家或地区无法登录微软账号。{"\r\n"}请使用加速器或 VPN。", "登录失败", "我知道了");
                    throw new Exception("$$");
                }

                if (result.Contains("2148916238"))
                {
                    if (ModMain.MyMsgBox("该账号年龄不足，你需要先修改出生日期，然后才能登录。" + "\r\n" + "该账号目前填写的年龄是否在 13 岁以上？",
                            "登录提示", "13 岁以上", "12 岁以下", "我不知道") == 1)
                    {
                        ModBase.OpenWebsite("https://account.live.com/editprof.aspx");
                        ModMain.MyMsgBox(
                            "请在打开的网页中修改账号的出生日期（至少改为 18 岁以上）。" + "\r\n" + "在修改成功后等待一分钟，然后再回到 PCL，就可以正常登录了！",
                            "登录提示");
                    }
                    else
                    {
                        ModBase.OpenWebsite(
                            "https://support.microsoft.com/zh-cn/account-billing/如何更改-microsoft-帐户上的出生日期-837badbc-999e-54d2-2617-d19206b9540a");
                        ModMain.MyMsgBox(
                            "请根据打开的网页的说明，修改账号的出生日期（至少改为 18 岁以上）。" + "\r\n" +
                            "在修改成功后等待一分钟，然后再回到 PCL，就可以正常登录了！", "登录提示");
                    }

                    throw new Exception("$$");
                }

                ModProfile.ProfileLog("正版验证 Step 3/6 获取 XSTSToken 失败：" + response.StatusCode);
                var IsIgnore = false;
                ModBase.RunInUiWait(() =>
                {
                    if (!IsLaunching)
                        return;
                    if (ModMain.MyMsgBox(
                            $"启动器在尝试刷新账号信息时(Step 3)遇到了网络错误。{"\r\n"}你可以选择取消，检查网络后再次启动，也可以选择忽略错误继续启动，但可能无法游玩部分服务器。",
                            "账号信息获取失败", "继续", "取消") == 1)
                        IsIgnore = true;
                });
                if (IsIgnore)
                {
                    return new[] { ModProfile.SelectedProfile.AccessToken, "Ignore" };
                    return default;
                }

                response.EnsureSuccessStatusCode();
            }
        }

        var ResultJson = (JObject)ModBase.GetJson(result);
        var XSTSToken = ResultJson["Token"].ToString();
        var UHS = ResultJson["DisplayClaims"]["xui"][0]["uhs"].ToString();
        return new[] { XSTSToken, UHS };
    }

    /// <summary>
    ///     正版验证步骤 4：从 {XSTSToken, UHS} 获取 Minecraft accessToken
    /// </summary>
    /// <param name="Tokens">包含 XSTSToken 与 UHS 的字符串组</param>
    /// <returns>Minecraft accessToken</returns>
    private static string MsLoginStep4(string[] Tokens)
    {
        ModProfile.ProfileLog("开始正版验证 Step 4/6: 获取 Minecraft AccessToken");
        if (Tokens.Length < 2 || string.IsNullOrEmpty(Tokens.ElementAt(0)) || string.IsNullOrEmpty(Tokens.ElementAt(1)))
            throw new ArgumentException("传入的 XSTSToken 或者 UHS 错误", nameof(Tokens));
        var requestData = new Dictionary<string, string> { { "identityToken", $"XBL3.0 x={Tokens[1]};{Tokens[0]}" } };
        string Result;
        try
        {
            using (var response = HttpRequest
                       .CreatePost("https://api.minecraftservices.com/authentication/login_with_xbox")
                       .WithJsonContent(requestData)
                       .SendAsync()
                       .GetAwaiter()
                       .GetResult())
            {
                response.EnsureSuccessStatusCode();
                Result = response.AsString();
            }
        }
        catch (HttpRequestException ex)
        {
            var Message = ex.Message;
            if (ex.StatusCode.Equals(HttpStatusCode.TooManyRequests))
            {
                ModBase.Log(ex, "正版验证 Step 4 汇报 429");
                throw new Exception("$登录尝试太过频繁，请等待几分钟后再试！");
            }

            if (ex.StatusCode is { } arg1 && arg1 == HttpStatusCode.Forbidden)
            {
                ModBase.Log(ex, "正版验证 Step 4 汇报 403");
                throw new Exception("$当前 IP 的登录尝试异常。" + "\r\n" + "如果你使用了 VPN 或加速器，请把它们关掉或更换节点后再试！");
            }

            ModProfile.ProfileLog("正版验证 Step 4/6 获取 MC AccessToken 失败：" + ex);
            var IsIgnore = false;
            ModBase.RunInUiWait(() =>
            {
                if (!IsLaunching)
                    return;
                if (ModMain.MyMsgBox(
                        $"启动器在尝试刷新账号信息时(Step 4)遇到了网络错误。{"\r\n"}你可以选择取消，检查网络后再次启动，也可以选择忽略错误继续启动，但可能无法游玩部分服务器。",
                        "账号信息获取失败", "继续", "取消") == 1)
                    IsIgnore = true;
            });
            if (IsIgnore)
            {
                return "Ignore";
                return default;
            }

            throw;
        }

        var ResultJson = (JObject)ModBase.GetJson(Result);
        var AccessToken = ResultJson["access_token"].ToString();
        if (string.IsNullOrWhiteSpace(AccessToken))
            throw new Exception("获取到的 Minecraft AccessToken 为空，登录流程异常！");
        return AccessToken;
    }

    /// <summary>
    ///     正版验证步骤 5：验证微软账号是否持有 MC，这也会刷新 XGP
    /// </summary>
    /// <param name="accessToken">Minecraft accessToken</param>
    private static void MsLoginStep5(string accessToken)
    {
        ModProfile.ProfileLog("开始正版验证 Step 5/6: 验证账户是否持有 MC");
        if (string.IsNullOrEmpty(accessToken))
            throw new ArgumentException("传入的 AccessToken 为空", nameof(accessToken));
        var result = "";
        try
        {
            using (var response = HttpRequest
                       .Create("https://api.minecraftservices.com/entitlements/mcstore")
                       .WithBearerToken(accessToken)
                       .SendAsync()
                       .GetAwaiter()
                       .GetResult())
            {
                response.EnsureSuccessStatusCode();
                result = response.AsString();
            }

            var ResultJson = (JObject)ModBase.GetJson(result);
            if (!(ResultJson.ContainsKey("items") && ResultJson["items"].Any(x =>
                    x["name"]?.ToString() == "product_minecraft" || x["name"]?.ToString() == "game_minecraft")))
            {
                switch (ModMain.MyMsgBox("暂时无法获取到此账户信息，此账户可能没有购买 Minecraft Java Edition 或者账户的 Xbox Game Pass 已过期",
                            "登录失败", "购买 Minecraft", "取消"))
                {
                    case 1:
                    {
                        ModBase.OpenWebsite(
                            "https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj");
                        break;
                    }
                }

                throw new Exception("$$");
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "正版验证 Step 5 异常：" + result);
            throw;
        }
    }

    /// <summary>
    ///     正版验证步骤 6：从 Minecraft accessToken 获取 {UUID, UserName, ProfileJson}
    /// </summary>
    /// <param name="AccessToken">Minecraft accessToken</param>
    /// <returns>包含 UUID, UserName 和 ProfileJson 的字符串组</returns>
    private static string[] MsLoginStep6(string AccessToken)
    {
        ModProfile.ProfileLog("开始正版验证 Step 6/6: 获取玩家 ID 与 UUID 等相关信息");
        if (string.IsNullOrEmpty(AccessToken))
            throw new ArgumentException("传入的 AccessToken 为空", nameof(AccessToken));
        string Result;
        try
        {
            using (var response = HttpRequest
                       .Create("https://api.minecraftservices.com/minecraft/profile")
                       .WithBearerToken(AccessToken)
                       .SendAsync()
                       .GetAwaiter()
                       .GetResult())
            {
                response.EnsureSuccessStatusCode();
                Result = response.AsString();
            }
        }
        catch (HttpRequestException ex)
        {
            var Message = ex.Message;
            if (ex.StatusCode.Equals(HttpStatusCode.TooManyRequests))
            {
                ModBase.Log(ex, "正版验证 Step 6 汇报 429");
                throw new Exception("$登录尝试太过频繁，请等待几分钟后再试！");
            }

            if (ex.StatusCode is { } arg2 && arg2 == HttpStatusCode.NotFound)
            {
                ModBase.Log(ex, "正版验证 Step 6 汇报 404");
                ModBase.RunInNewThread(() =>
                {
                    switch (ModMain.MyMsgBox("请先创建 Minecraft 玩家档案，然后再重新登录。", "登录失败", "创建档案", "取消"))
                    {
                        case 1:
                        {
                            ModBase.OpenWebsite("https://www.minecraft.net/zh-hans/msaprofile/mygames/editprofile");
                            break;
                        }
                    }
                }, "Login Failed: Create Profile");
                throw new Exception("$$");
            }

            ModProfile.ProfileLog("正版验证 Step 6/6 获取玩家档案信息失败：" + ex);
            var IsIgnore = false;
            ModBase.RunInUiWait(() =>
            {
                if (!IsLaunching)
                    return;
                if (ModMain.MyMsgBox(
                        $"启动器在尝试刷新账号信息时(Step 6)遇到了网络错误。{"\r\n"}你可以选择取消，检查网络后再次启动，也可以选择忽略错误继续启动，但可能无法游玩部分服务器。",
                        "账号信息获取失败", "继续", "取消") == 1)
                    IsIgnore = true;
            });
            if (IsIgnore)
            {
                return new[] { ModProfile.SelectedProfile.Uuid, ModProfile.SelectedProfile.Username, "Ignore" };
                return default;
            }

            throw;
        }

        var ResultJson = (JObject)ModBase.GetJson(Result);
        var UUID = ResultJson["id"].ToString();
        var UserName = ResultJson["name"].ToString();
        return new[] { UUID, UserName, Result };
    }

    #endregion

    #region 第三方验证

    private static void McLoginServerStart(ModLoader.LoaderTask<McLoginServer, McLoginResult> data)
    {
        var input = data.Input;
        var needRefresh = false;
        var wasRefreshed = false;

        ModProfile.ProfileLog("验证方式：" + input.Description);
        data.Progress = 0.05d;

        // 尝试验证登录（如果不需要重新选择档案且不是创建档案）
        if (!input.ForceReselectProfile && !ModProfile.IsCreatingProfile)
        {
            try
            {
                ThrowIfAborted(data);
                McLoginRequestValidate(ref data);
                data.Progress = 0.95d;
                return; // 登录成功，直接返回
            }
            catch (WebException ex)
            {
                HandleHttpWebException(ex, "验证登录失败");
            }
            catch (Exception ex)
            {
                HandleException(ex, "验证登录失败");
            }

            data.Progress = 0.25d;

            // 尝试刷新登录
            try
            {
                ThrowIfAborted(data);
                McLoginRequestRefresh(ref data, needRefresh);
                data.Progress = needRefresh ? 0.85d : 0.45d;
                data.Progress = 0.95d;
                return; // 刷新成功，直接返回
            }
            catch (Exception ex)
            {
                ModProfile.ProfileLog("刷新登录失败：" + ex);
                ModMain.MyMsgBox("刷新登录失败: " + ex, "第三方验证失败", IsWarn: true);
                if (wasRefreshed)
                    throw new Exception("二轮刷新登录失败", ex);
            }
        }

        // 尝试普通登录
        try
        {
            ThrowIfAborted(data);
            needRefresh = McLoginRequestLogin(ref data);
        }
        catch (WebException ex)
        {
            HandleLoginHttpException(ex);
        }
        catch (Exception ex)
        {
            HandleException(ex, "第三方验证登录失败");
        }

        // 如果需要刷新，循环刷新一次
        if (needRefresh)
        {
            ModProfile.ProfileLog("重新进行刷新登录");
            wasRefreshed = true;
            data.Progress = 0.65d;

            try
            {
                ThrowIfAborted(data);
                McLoginRequestRefresh(ref data, needRefresh);
                data.Progress = 0.95d;
                return;
            }
            catch (Exception ex)
            {
                ModProfile.ProfileLog("刷新登录失败：" + ex);
                ModMain.MyMsgBox("刷新登录失败: " + ex, "第三方验证失败", IsWarn: true);
                throw new Exception("二轮刷新登录失败", ex);
            }
        }

        // 最终完成
        data.Progress = 0.95d;
    }

    /// <summary>
    ///     检查任务是否被中断
    /// </summary>
    private static void ThrowIfAborted(ModLoader.LoaderTask<McLoginServer, McLoginResult> data)
    {
        if (data.IsAborted)
            throw new ThreadInterruptedException();
    }


    /// <summary>
    ///     处理普通登录 HttpWebException
    /// </summary>
    private static void HandleLoginHttpException(WebException ex)
    {
        ModProfile.ProfileLog("验证失败：" + ex);
        string message = null;
        var responseText = ex.InnerException;

        try
        {
            message = "登录失败：";
        }
        catch
        {
            // 忽略解析错误
        }

        if (message is null)
            message = "第三方验证登录失败，请检查你的网络状况是否良好。" + "\r\n" + "\r\n" +
                      "详细信息：" + responseText;

        ModMain.MyMsgBox("刷新登录失败: " + ex, "第三方验证失败", IsWarn: true);
        throw new Exception("$" + message);
    }

    // Server 登录：三种验证方式的请求
    private static void McLoginRequestValidate(ref ModLoader.LoaderTask<McLoginServer, McLoginResult> Data)
    {
        ModProfile.ProfileLog("验证登录开始（Validate, Authlib");
        // 提前缓存信息，否则如果在登录请求过程中退出登录，设置项目会被清空，导致输出存在空值
        var AccessToken = "";
        var ClientToken = "";
        var Uuid = "";
        var Name = "";
        if (ModProfile.SelectedProfile is not null)
        {
            AccessToken = ModProfile.SelectedProfile.AccessToken;
            ClientToken = ModProfile.SelectedProfile.ClientToken;
            Uuid = ModProfile.SelectedProfile.Uuid;
            Name = ModProfile.SelectedProfile.Username;
        }

        // 发送登录请求
        var RequestData = new JObject(new JProperty("accessToken", AccessToken),
            new JProperty("clientToken", ClientToken));
        Requester.Fetch(Data.Input.BaseUrl + "/validate",
            new FetchParam
            {
                Method = "POST",
                Content = RequestData.ToString(0),
                Headers = new Dictionary<string, string> { { "Accept-Language", "zh-CN" } },
                ContentType = "application/json"
            }); // 没有返回值的
        // 将登录结果输出
        Data.Output.AccessToken = AccessToken;
        Data.Output.ClientToken = ClientToken;
        Data.Output.Uuid = Uuid;
        Data.Output.Name = Name;
        Data.Output.Type = "Auth";
        // 不更改缓存，直接结束
        ModProfile.ProfileLog("验证登录成功（Validate, Authlib");
    }

    private static void McLoginRequestRefresh(ref ModLoader.LoaderTask<McLoginServer, McLoginResult> Data,
        bool RequestUser)
    {
        var RefreshInfo = new JObject();
        var SelectProfile = new JObject
            { { "name", ModProfile.SelectedProfile.Username }, { "id", ModProfile.SelectedProfile.Uuid } };
        RefreshInfo.Add("selectedProfile", SelectProfile);
        RefreshInfo.Add(new JProperty("accessToken", ModProfile.SelectedProfile.AccessToken));
        RefreshInfo.Add(new JProperty("requestUser", true));
        ModProfile.ProfileLog("刷新登录开始（Refresh, Authlib");
        var LoginJson = (JObject)ModBase.GetJson(Requester.Fetch(Data.Input.BaseUrl + "/refresh",
            new FetchParam
            {
                Method = "POST",
                Content = RefreshInfo.ToString(Newtonsoft.Json.Formatting.None),
                Headers = new Dictionary<string, string> { { "Accept-Language", "zh-CN" } },
                ContentType = "application/json"
            }
        ));
        // 将登录结果输出
        if (LoginJson["selectedProfile"] is null)
            throw new Exception("选择的角色 " + ModProfile.SelectedProfile.Username + " 无效！");
        Data.Output.AccessToken = LoginJson["accessToken"].ToString();
        Data.Output.ClientToken = LoginJson["clientToken"].ToString();
        Data.Output.Uuid = LoginJson["selectedProfile"]["id"].ToString();
        Data.Output.Name = LoginJson["selectedProfile"]["name"].ToString();
        Data.Output.Type = "Auth";
        // 保存缓存
        var ProfileIndex = ModProfile.ProfileList.IndexOf(ModProfile.SelectedProfile);
        ModProfile.ProfileList[ProfileIndex].Username = Data.Output.Name;
        ModProfile.ProfileList[ProfileIndex].AccessToken = Data.Output.AccessToken;
        ModProfile.ProfileList[ProfileIndex].ClientToken = Data.Output.ClientToken;
        ModProfile.ProfileList[ProfileIndex].Uuid = Data.Output.Uuid;
        ModProfile.ProfileList[ProfileIndex].Name = Data.Input.UserName;
        ModProfile.ProfileList[ProfileIndex].Password = Data.Input.Password;
        ModProfile.ProfileLog("刷新登录成功（Refresh, Authlib）");
    }

    private static bool McLoginRequestLogin(ref ModLoader.LoaderTask<McLoginServer, McLoginResult> Data)
    {
        try
        {
            var NeedRefresh = false;
            ModProfile.ProfileLog("登录开始（Login, Authlib）");
            var RequestData = new JObject(
                new JProperty("agent", new JObject(new JProperty("name", "Minecraft"), new JProperty("version", 1))),
                new JProperty("username", Data.Input.UserName), new JProperty("password", Data.Input.Password),
                new JProperty("requestUser", true));
            var LoginJson = (JObject)ModBase.GetJson(Requester.Fetch(Data.Input.BaseUrl + "/authenticate",
                new FetchParam
                {
                    Method = "POST",
                    Content = RequestData.ToString(0),
                    Headers = new Dictionary<string, string> { { "Accept-Language", "zh-CN" } },
                    ContentType = "application/json"
                }));
            // 检查登录结果
            if (LoginJson["availableProfiles"].Count() == 0)
            {
                if (Data.Input.ForceReselectProfile)
                    ModMain.Hint("你还没有创建角色，无法更换！", ModMain.HintType.Critical);
                throw new Exception("$你还没有创建角色，请在创建角色后再试！");
            }

            if (Data.Input.ForceReselectProfile && LoginJson["availableProfiles"].Count() == 1)
                ModMain.Hint("你的账户中只有一个角色，无法更换！", ModMain.HintType.Critical);
            string SelectedName = null;
            string SelectedId = null;
            if ((LoginJson["selectedProfile"] is null || Data.Input.ForceReselectProfile) &&
                LoginJson["availableProfiles"].Count() > 1)
            {
                // 要求选择档案；优先从缓存读取
                NeedRefresh = true;
                var CacheId = ModProfile.SelectedProfile is not null ? ModProfile.SelectedProfile.Uuid : "";
                foreach (var Profile in LoginJson["availableProfiles"])
                    if ((Profile["id"].ToString() ?? "") == (CacheId ?? ""))
                    {
                        SelectedName = Profile["name"].ToString();
                        SelectedId = Profile["id"].ToString();
                        ModProfile.ProfileLog("根据缓存选择的角色：" + SelectedName);
                    }

                // 缓存无效，要求玩家选择
                if (SelectedName is null)
                {
                    ModProfile.ProfileLog("要求玩家选择角色");
                    ModBase.RunInUiWait(() =>
                    {
                        var SelectionControl = new List<IMyRadio>();
                        var SelectionJson = new List<JToken>();
                        foreach (var Profile in LoginJson["availableProfiles"])
                        {
                            SelectionControl.Add(new MyRadioBox { Text = Profile["name"].ToString() });
                            SelectionJson.Add(Profile);
                        }

                        var SelectedIndex = (int)ModMain.MyMsgBoxSelect(SelectionControl, "选择使用的角色");
                        SelectedName = SelectionJson[SelectedIndex]["name"].ToString();
                        SelectedId = SelectionJson[SelectedIndex]["id"].ToString();
                    });

                    ModProfile.ProfileLog("玩家选择的角色：" + SelectedName);
                }
            }
            else
            {
                SelectedName = LoginJson["selectedProfile"]["name"].ToString();
                SelectedId = LoginJson["selectedProfile"]["id"].ToString();
            }

            // 将登录结果输出
            Data.Output.AccessToken = LoginJson["accessToken"].ToString();
            Data.Output.ClientToken = LoginJson["clientToken"].ToString();
            Data.Output.Name = SelectedName;
            Data.Output.Uuid = SelectedId;
            Data.Output.Type = "Auth";
            // 获取服务器信息
            var Response =
                Requester.FetchString(Data.Input.BaseUrl.Replace("/authserver", ""));
            var ServerName = JObject.Parse(Response)["meta"]["serverName"].ToString();
            // 保存缓存
            if (Data.Input.IsExist)
            {
                var ProfileIndex = ModProfile.ProfileList.IndexOf(ModProfile.SelectedProfile);
                ModProfile.ProfileList[ProfileIndex].Username = Data.Output.Name;
                ModProfile.ProfileList[ProfileIndex].Uuid = Data.Output.Uuid;
                ModProfile.ProfileList[ProfileIndex].ServerName = ServerName;
                ModProfile.ProfileList[ProfileIndex].AccessToken = Data.Output.AccessToken;
                ModProfile.ProfileList[ProfileIndex].ClientToken = Data.Output.ClientToken;
            }
            else
            {
                var NewProfile = new ModProfile.McProfile
                {
                    Type = McLoginType.Auth,
                    Uuid = Data.Output.Uuid,
                    Username = Data.Output.Name,
                    Server = Data.Input.BaseUrl,
                    ServerName = ServerName,
                    Name = Data.Input.UserName,
                    Password = Data.Input.Password,
                    AccessToken = Data.Output.AccessToken,
                    ClientToken = Data.Output.ClientToken,
                    Expires = 1743779140286L,
                    Desc = ""
                };
                ModProfile.ProfileList.Add(NewProfile);
                ModProfile.SelectedProfile = NewProfile;
                ModProfile.IsCreatingProfile = false;
            }

            ModProfile.SaveProfile();
            ModProfile.ProfileLog("登录成功（Login, Authlib）");
            return NeedRefresh;
        }
        catch (WebException ex)
        {
            throw;
        }
        catch (Exception ex)
        {
            var AllMessage = ex.ToString();
            ModProfile.ProfileLog("第三方验证失败: " + ex);
            if (ex.Message.StartsWithF("$")) throw;

            throw new Exception("登录失败：" + ex.Message, ex);
        }
    }

    #endregion

    #region 离线验证

    private static void McLoginLegacyStart(ModLoader.LoaderTask<McLoginLegacy, McLoginResult> Data)
    {
        var Input = Data.Input;
        ModProfile.ProfileLog($"验证方式：离线（{Input.UserName}, {Input.Uuid}）");
        Data.Progress = 0.1d;
        {
            ref var withBlock = ref Data.Output;
            withBlock.Name = Input.UserName;
            withBlock.Uuid = ModProfile.SelectedProfile.Uuid;
            withBlock.Type = "Legacy";
        }
        // 将结果扩展到所有项目中
        Data.Output.AccessToken = Data.Output.Uuid;
        Data.Output.ClientToken = Data.Output.Uuid;
    }

    #endregion

    #endregion
}

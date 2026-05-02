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
using PCL.Core.Minecraft;
using PCL.Core.Minecraft.Launch.Utils;
using PCL.Core.Utils;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.Secret;
using PCL.Network;
using PCL.Core.IO.Net.Http;
using PCL;

namespace PCL;

public static class ModLaunch
{
    #region 内存优化

    private static void McLaunchMemoryOptimize(ModLoader.LoaderTask<int, int> Loader)
    {
        McLaunchLog("内存优化开始");
        var Finished = false;
        ModBase.RunInNewThread(() =>
        {
            PageToolsTest.MemoryOptimize(false);
            Finished = true;
        }, "Launch Memory Optimize");
        while (!Finished && !Loader.IsAborted)
        {
            if (Loader.Progress < 0.7d)
                Loader.Progress += 0.007d; // 10s
            else
                Loader.Progress += (0.95d - Loader.Progress) * 0.02d; // 最快 += 0.005

            Thread.Sleep(100);
        }
    }

    #endregion

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

    #region 开始

    public static bool IsLaunching;
    public static McLaunchOptions CurrentLaunchOptions;

    public class McLaunchOptions
    {
        /// <summary>
        ///     额外的启动参数。
        /// </summary>
        public List<string> ExtraArgs = new();

        /// <summary>
        ///     强行指定启动的 MC 实例。
        ///     默认值：Nothing。使用 McInstanceCurrent。
        /// </summary>
        public ModMinecraft.McInstance Instance = null;

        /// <summary>
        ///     是否为 “测试游戏” 按钮启动的游戏。
        ///     如果是，则显示游戏实时日志。
        /// </summary>
        public bool IsTest = false;

        /// <summary>
        ///     将启动脚本保存到该地址，然后取消启动。这同时会改变启动时的提示等。
        ///     默认值：Nothing。不保存。
        /// </summary>
        public string SaveBatch = null;

        /// <summary>
        ///     强制指定在启动后进入的服务器 IP。
        ///     默认值：Nothing。使用实例设置的值。
        /// </summary>
        public string ServerIp = null;

        /// <summary>
        ///     指定在启动之后进入的存档名称。
        ///     默认值：Nothing。使用实例设置的值。
        /// </summary>
        public string WorldName = null;
    }

    /// <summary>
    ///     尝试启动 Minecraft。必须在 UI 线程调用。
    ///     返回是否实际开始了启动（如果没有，则一定弹出了错误提示）。
    /// </summary>
    public static bool McLaunchStart(McLaunchOptions Options = null)
    {
        IsLaunching = true;
        CurrentLaunchOptions = Options ?? new McLaunchOptions();
        // 预检查
        if (!ModBase.RunInUi())
            throw new Exception("McLaunchStart 必须在 UI 线程调用！");
        if (McLaunchLoader.State == ModBase.LoadState.Loading)
        {
            ModMain.Hint("已有游戏正在启动中！", ModMain.HintType.Critical);
            IsLaunching = false;
            return false;
        }

        // 强制切换需要启动的实例
        if (CurrentLaunchOptions.Instance is not null &&
            ModMinecraft.McInstanceSelected != CurrentLaunchOptions.Instance)
        {
            McLaunchLog("在启动前切换到实例 " + CurrentLaunchOptions.Instance.Name);
            // 检查实例
            CurrentLaunchOptions.Instance.Load();
            if (CurrentLaunchOptions.Instance.State == ModMinecraft.McInstanceState.Error)
            {
                ModMain.Hint("无法启动 Minecraft：" + CurrentLaunchOptions.Instance.Desc, ModMain.HintType.Critical);
                IsLaunching = false;
                return false;
            }

            // 切换实例
            ModMinecraft.McInstanceSelected = CurrentLaunchOptions.Instance;
            States.Game.SelectedInstance = ModMinecraft.McInstanceSelected.Name;
            ModMain.FrmLaunchLeft.RefreshButtonsUI();
            ModMain.FrmLaunchLeft.RefreshPage(false);
        }

        ModMain.FrmMain.AprilGiveup();
        // 禁止进入实例选择页面（否则就可以在启动中切换 McInstanceCurrent 了）
        ModMain.FrmMain.PageStack =
            ModMain.FrmMain.PageStack.Where(p => p.Page != FormMain.PageType.InstanceSelect).ToList();
        // 实际启动加载器
        McLaunchLoader.Start(Options, true);
        return true;
    }

    /// <summary>
    ///     记录启动日志。
    /// </summary>
    public static void McLaunchLog(string Text)
    {
        Text = ModMinecraft.FilterUserName(ModMinecraft.FilterAccessToken(Text, '*'), '*');
        ModBase.RunInUi(() =>
            ModMain.FrmLaunchRight.LabLog.Text += "\r\n" + "[" + TimeUtils.GetTimeNow() + "] " + Text);
        ModBase.Log("[Launch] " + Text);
    }

    // 启动状态切换
    public static ModLoader.LoaderTask<McLaunchOptions, object> McLaunchLoader = new("Loader Launch", McLaunchStart)
        { OnStateChanged = a => McLaunchState((dynamic)a) };

    public static ModLoader.LoaderCombo<object> McLaunchLoaderReal;
    public static Process McLaunchProcess;
    public static ModWatcher.Watcher McLaunchWatcher;

    private static void McLaunchState(ModLoader.LoaderTask<McLaunchOptions, object> Loader)
    {
        switch (McLaunchLoader.State)
        {
            case ModBase.LoadState.Finished:
            case ModBase.LoadState.Failed:
            case ModBase.LoadState.Waiting:
            case ModBase.LoadState.Aborted:
            {
                ModMain.FrmLaunchLeft.PageChangeToLogin();
                break;
            }
            case ModBase.LoadState.Loading:
            {
                // 在预检测结束后再触发动画
                ModMain.FrmLaunchRight.LabLog.Text = "";
                break;
            }
        }
    }

    /// <summary>
    ///     指定启动中断时的提示文本。若不为 Nothing 则会显示为绿色。
    /// </summary>
    private static string AbortHint;

    // 实际的启动方法
    private static void McLaunchStart(ModLoader.LoaderTask<McLaunchOptions, object> Loader)
    {
        // 开始动画
        ModBase.RunInUiWait(ModMain.FrmLaunchLeft.PageChangeToLaunching);
        // 预检测（预检测的错误将直接抛出）
        try
        {
            McLaunchPrecheck();
            McLaunchLog("预检测已通过");
        }
        catch (Exception ex)
        {
            if (!ex.Message.StartsWithF("$$"))
                ModMain.Hint(ex.Message, ModMain.HintType.Critical);
            throw;
        }

        // 正式加载
        try
        {
            // 构造主加载器
            var Loaders = new List<ModLoader.LoaderBase>
            {
                new ModLoader.LoaderTask<int, int>("获取 Java", McLaunchJava) { ProgressWeight = 4d, Block = false },
                McLoginLoader,
                new ModLoader.LoaderCombo<string>("补全文件",
                        ModDownload.DlClientFix(ModMinecraft.McInstanceSelected, false,
                            ModDownload.AssetsIndexExistsBehaviour.DownloadInBackground))
                    { ProgressWeight = 15d, Show = false },
                new ModLoader.LoaderTask<string, List<ModMinecraft.McLibToken>>("获取启动参数", McLaunchArgumentMain)
                    { ProgressWeight = 2d },
                new ModLoader.LoaderTask<List<ModMinecraft.McLibToken>, int>("解压文件", McLaunchNatives)
                    { ProgressWeight = 2d },
                new ModLoader.LoaderTask<int, int>("预启动处理", _ => McLaunchPrerun()) { ProgressWeight = 1d },
                new ModLoader.LoaderTask<int, int>("执行自定义命令", McLaunchCustom) { ProgressWeight = 1d },
                new ModLoader.LoaderTask<int, Process>("启动进程", McLaunchRun) { ProgressWeight = 2d },
                new ModLoader.LoaderTask<Process, int>("等待游戏窗口出现", McLaunchWait) { ProgressWeight = 1d },
                new ModLoader.LoaderTask<int, int>("结束处理", _ => McLaunchEnd()) { ProgressWeight = 1d }
            }; // .ProgressWeight = 15, .Block = False
            // 内存优化
            switch (ModBase.Setup.Get("VersionRamOptimize", ModMinecraft.McInstanceSelected))
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false): // 全局
                {
                    if (Conversions.ToBoolean(Config.Launch.OptimizeMemory)) // 使用全局设置
                    {
                        ((ModLoader.LoaderCombo<string>)Loaders[2]).Block = false;
                        Loaders.Insert(3,
                            new ModLoader.LoaderTask<int, int>("内存优化", McLaunchMemoryOptimize)
                                { ProgressWeight = 30d });
                    }

                    break;
                }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, 1, false): // 开启
                {
                    ((ModLoader.LoaderCombo<string>)Loaders[2]).Block = false;
                    Loaders.Insert(3,
                        new ModLoader.LoaderTask<int, int>("内存优化", McLaunchMemoryOptimize) { ProgressWeight = 30d });
                    break;
                }
                case var case2 when Operators.ConditionalCompareObjectEqual(case2, 2, false): // 关闭
                {
                    break;
                }
            }

            var LaunchLoader = new ModLoader.LoaderCombo<object>("Minecraft 启动", Loaders) { Show = false };
            if (McLoginLoader.State == ModBase.LoadState.Finished)
                McLoginLoader.State = ModBase.LoadState.Waiting; // 要求重启登录主加载器，它会自行决定是否启动副加载器
            // 等待加载器执行并更新 UI
            McLaunchLoaderReal = LaunchLoader;
            AbortHint = null;
            LaunchLoader.Start();
            // 任务栏进度条
            ModLoader.LoaderTaskbarAdd(LaunchLoader);
            while (LaunchLoader.State == ModBase.LoadState.Loading)
            {
                ModMain.FrmLaunchLeft.Dispatcher.Invoke(ModMain.FrmLaunchLeft.LaunchingRefresh);
                Thread.Sleep(100);
            }

            ModMain.FrmLaunchLeft.Dispatcher.Invoke(ModMain.FrmLaunchLeft.LaunchingRefresh);
            // 成功与失败处理
            switch (LaunchLoader.State)
            {
                case ModBase.LoadState.Finished:
                {
                    ModMain.Hint(ModMinecraft.McInstanceSelected.Name + " 启动成功！", ModMain.HintType.Finish);
                    break;
                }
                case ModBase.LoadState.Aborted:
                {
                    if (AbortHint is null)
                        ModMain.Hint(CurrentLaunchOptions?.SaveBatch is null ? "已取消启动！" : "已取消导出启动脚本！");
                    else
                        ModMain.Hint(AbortHint, ModMain.HintType.Finish);

                    break;
                }
                case ModBase.LoadState.Failed:
                {
                    throw LaunchLoader.Error;
                }

                default:
                {
                    throw new Exception("错误的状态改变：" + ModBase.GetStringFromEnum(LaunchLoader.State));
                }
            }

            IsLaunching = false;
        }
        catch (Exception ex)
        {
            var CurrentEx = ex;
            NextInner: ;

            if (CurrentEx.Message.StartsWithF("$"))
            {
                // 若有以 $ 开头的错误信息，则以此为准显示提示
                // 若错误信息为 $$，则不提示
                if (!(CurrentEx.Message == "$$"))
                    ModMain.MyMsgBox(CurrentEx.Message.TrimStart('$'),
                        CurrentLaunchOptions?.SaveBatch is null ? "启动失败" : "导出启动脚本失败");
                throw;
            }

            if (CurrentEx.InnerException is not null)
            {
                // 检查下一级错误
                CurrentEx = CurrentEx.InnerException;
                goto NextInner;
            }

            // 没有特殊处理过的错误信息
            McLaunchLog("错误：" + ex);
            ModBase.Log(ex, CurrentLaunchOptions?.SaveBatch is null ? "Minecraft 启动失败" : "导出启动脚本失败",
                ModBase.LogLevel.Msgbox, CurrentLaunchOptions?.SaveBatch is null ? "启动失败" : "导出启动脚本失败");
            throw;
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
    public abstract class McLoginData
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

    public class McLoginServer : McLoginData
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

    public class McLoginMs : McLoginData
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

    public class McLoginLegacy : McLoginData
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
    public struct McLoginResult
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


    private class XBLTokenRequestData
    {
        public PropertiesData Properties { get; set; }
        public string RelyingParty { get; set; }
        public string TokenType { get; set; }

        public class PropertiesData
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


    private class XSTSTokenRequestData
    {
        public PropertiesData Properties { get; set; }
        public string RelyingParty { get; set; }
        public string TokenType { get; set; }

        public class PropertiesData
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

    #region Java 处理

    public static JavaEntry McLaunchJavaSelected;

    private static void McLaunchJava(ModLoader.LoaderTask<int, int> task)
    {
        var minVer = new Version(0, 0, 0, 0);
        var maxVer = new Version(999, 999, 999, 999);

        // MC 大版本检测
        if ((!ModMinecraft.McInstanceSelected.Info.Valid &&
             ModMinecraft.McInstanceSelected.ReleaseTime >= new DateTime(2024, 4, 2)) ||
            (ModMinecraft.McInstanceSelected.Info.Valid &&
             ModMinecraft.McInstanceSelected.Info.Vanilla >= new Version(20, 0, 5)))
        {
            // 1.20.5+ (24w14a+)：至少 Java 21
            if (ModBase.ModeDebug)
                ModBase.Log("[Launch] [Debug] MC 1.20.5+ (24w14a+) 要求至少 Java 21");
            minVer = new Version(21, 0, 0, 0);
        }
        else if ((!ModMinecraft.McInstanceSelected.Info.Valid &&
                  ModMinecraft.McInstanceSelected.ReleaseTime >= new DateTime(2021, 11, 16)) ||
                 (ModMinecraft.McInstanceSelected.Info.Valid &&
                  ModMinecraft.McInstanceSelected.Info.Vanilla.Major >= 18))
        {
            // 1.18 pre2+：至少 Java 17
            if (ModBase.ModeDebug)
                ModBase.Log("[Launch] [Debug] MC 1.18 pre2+ 要求至少 Java 17");
            minVer = new Version(17, 0, 0, 0);
        }
        else if ((!ModMinecraft.McInstanceSelected.Info.Valid &&
                  ModMinecraft.McInstanceSelected.ReleaseTime >= new DateTime(2021, 5, 11)) ||
                 (ModMinecraft.McInstanceSelected.Info.Valid &&
                  ModMinecraft.McInstanceSelected.Info.Vanilla.Major >= 17))
        {
            // 1.17+ (21w19a+)：至少 Java 16
            if (ModBase.ModeDebug)
                ModBase.Log("[Launch] [Debug] MC 1.17+ (21w19a+) 要求至少 Java 16");
            minVer = new Version(16, 0, 0, 0);
        }
        else if (ModMinecraft.McInstanceSelected.ReleaseTime.Year >= 2017) // Minecraft 1.12 与 1.11 的分界线正好是 2017 年，太棒了
        {
            // 1.12+：至少 Java 8
            if (ModBase.ModeDebug)
                ModBase.Log("[Launch] [Debug] MC 1.12+ 要求至少 Java 8");
            minVer = new Version(1, 8, 0, 0);
        }
        else if (ModMinecraft.McInstanceSelected.ReleaseTime <= new DateTime(2013, 5, 1) &&
                 ModMinecraft.McInstanceSelected.ReleaseTime.Year >= 2001) // 避免某些版本写个 1960 年
        {
            // 1.5.2-：最高 Java 8
            if (ModBase.ModeDebug)
                ModBase.Log("[Launch] [Debug] MC 1.5.2- 要求最高 Java 12");
            maxVer = new Version(1, 8, 999, 999);
        }

        // 原版 26+：获取 Mojang 要求的 Java 版本
        string recommendedComponent = null;
        var recommendedCode =
            ModMinecraft.McInstanceSelected.JsonObject?["javaVersion"]?["majorVersion"]?.ToObject<int>() ??
            ModMinecraft.McInstanceSelected.JsonVersion?["java_version"]?.ToObject<int>() ?? 0;
        if (recommendedCode >= 22)
        {
            McLaunchLog("Mojang 要求至少使用 Java " + recommendedCode);
            minVer = new Version(1, recommendedCode, 0, 0);
            recommendedComponent =
                ModMinecraft.McInstanceSelected.JsonObject?["javaVersion"]?["component"]?.ToString() ??
                ModMinecraft.McInstanceSelected.JsonVersion?["java_component"]?.ToString();
            if (string.IsNullOrEmpty(recommendedComponent))
                recommendedComponent = null;
        }

        // OptiFine 检测
        if (ModMinecraft.McInstanceSelected.Info.HasOptiFine && ModMinecraft.McInstanceSelected.Info.Valid) // 不管非标准版本
        {
            if (ModMinecraft.McInstanceSelected.Info.Vanilla.Major < 7)
            {
                // <1.7：至多 Java 8
                maxVer = new Version(1, 8, 999, 999);
            }
            else if (ModMinecraft.McInstanceSelected.Info.Vanilla.Major >= 8 &&
                     ModMinecraft.McInstanceSelected.Info.Vanilla.Major < 12)
            {
                // 1.8 - 1.11：必须恰好 Java 8
                minVer = new Version(1, 8, 0, 0);
                maxVer = new Version(1, 8, 999, 999);
            }
            else if (ModMinecraft.McInstanceSelected.Info.Vanilla.Major == 12)
            {
                // 1.12：最高 Java 8
                maxVer = new Version(1, 8, 999, 999);
            }
        }

        // Forge 检测
        if (ModMinecraft.McInstanceSelected.Info.HasForge)
        {
            if (ModMinecraft.McInstanceSelected.Info.Vanilla >= new Version(6, 0, 1) &&
                ModMinecraft.McInstanceSelected.Info.Vanilla <= new Version(7, 0, 2))
            {
                // 1.6.1 - 1.7.2：必须 Java 7
                minVer = new Version(1, 7, 0, 0) > minVer ? new Version(1, 7, 0, 0) : minVer;
                maxVer = new Version(1, 7, 999, 999) < maxVer ? new Version(1, 7, 999, 999) : maxVer;
            }
            else if (ModMinecraft.McInstanceSelected.Info.Vanilla.Major <= 12 ||
                     !ModMinecraft.McInstanceSelected.Info.Valid) // 非标准版本
            {
                // <=1.12：Java 8
                maxVer = new Version(1, 8, 999, 999);
            }
            else if (ModMinecraft.McInstanceSelected.Info.Vanilla.Major <= 14)
            {
                // 1.13 - 1.14：Java 8 - 10
                minVer = new Version(1, 8, 0, 0) > minVer ? new Version(1, 8, 0, 0) : minVer;
                maxVer = new Version(1, 10, 999, 999) < maxVer ? new Version(1, 10, 999, 999) : maxVer;
            }
            else if (ModMinecraft.McInstanceSelected.Info.Vanilla.Major == 15)
            {
                // 1.15：Java 8 - 15
                minVer = new Version(1, 8, 0, 0) > minVer ? new Version(1, 8, 0, 0) : minVer;
                maxVer = new Version(1, 15, 999, 999) < maxVer ? new Version(1, 15, 999, 999) : maxVer;
            }
            else if (ModMinecraft.CompareVersionGe(ModMinecraft.McInstanceSelected.Info.Forge, "34.0.0") &&
                     ModMinecraft.CompareVersionGe("36.2.25", ModMinecraft.McInstanceSelected.Info.Forge))
            {
                // 1.16，Forge 34.X ~ 36.2.25：最高 Java 8u321
                maxVer = new Version(1, 8, 0, 320) < maxVer ? new Version(1, 8, 0, 321) : maxVer;
            }
            else if (ModMinecraft.McInstanceSelected.Info.Vanilla.Major >= 18 &&
                     ModMinecraft.McInstanceSelected.Info.Vanilla.Major < 19 &&
                     ModMinecraft.McInstanceSelected.Info.HasOptiFine) // #305
            {
                // 1.18：若安装了 OptiFine，最高 Java 18
                maxVer = new Version(1, 18, 999, 999) < maxVer ? new Version(1, 18, 999, 999) : maxVer;
            }
        }

        // Cleanroom 检测
        if (ModMinecraft.McInstanceSelected.Info.HasCleanroom)
        {
            // 需要至少 Java 21
            if (ModBase.ModeDebug)
                ModBase.Log("[Launch] [Debug] Cleanroom 要求至少 Java 21");
            minVer = new Version(21, 0, 0, 0) > minVer ? new Version(21, 0, 0, 0) : minVer;
        }

        // Fabric 检测
        if (ModMinecraft.McInstanceSelected.Info.HasFabric && ModMinecraft.McInstanceSelected.Info.Valid) // 不管非标准版本
        {
            if (ModMinecraft.McInstanceSelected.Info.Vanilla.Major >= 15 &&
                ModMinecraft.McInstanceSelected.Info.Vanilla.Major <= 16)
                // 1.15 - 1.16：Java 8+
                minVer = new Version(1, 8, 0, 0) > minVer ? new Version(1, 8, 0, 0) : minVer;
            else if (ModMinecraft.McInstanceSelected.Info.Vanilla.Major >= 18)
                // 1.18+：Java 17+
                minVer = new Version(1, 17, 0, 0) > minVer ? new Version(1, 17, 0, 0) : minVer;
        }

        // LiteLoader 检测
        if (ModMinecraft.McInstanceSelected.Info.HasLiteLoader && ModMinecraft.McInstanceSelected.Info.Valid)
        {
            // 最高 Java 8
            if (ModBase.ModeDebug)
                ModBase.Log("[Launch] [Debug] LiteLoader 要求最高 Java 8");
            maxVer = new Version(8, 999, 999, 999) < maxVer ? new Version(8, 999, 999, 999) : maxVer;
        }

        // LabyMod 检测
        if (ModMinecraft.McInstanceSelected.Info.HasLabyMod)
        {
            if (ModBase.ModeDebug)
                ModBase.Log("[Launch] [Debug] LabyMod 要求至少 Java 21");
            minVer = new Version(21, 0, 0, 0) > minVer ? new Version(21, 0, 0, 0) : minVer;
            maxVer = new Version(999, 999, 999, 999);
        }

        // JSON 中要求的版本
        if (ModMinecraft.McInstanceSelected.JsonObject["javaVersion"] is not null)
        {
            var majorVersion = ModBase.Val(ModMinecraft.McInstanceSelected.JsonObject["javaVersion"]["majorVersion"]);
            if (ModBase.ModeDebug)
                ModBase.Log("[Launch] [Debug] JSON 中参数要求至少 Java " + majorVersion);
            if (majorVersion <= 8d)
                minVer = new Version(1, (int)Math.Round(majorVersion), 0, 0) > minVer
                    ? new Version(1, (int)Math.Round(majorVersion), 0, 0)
                    : minVer;
            else
                minVer = new Version((int)Math.Round(majorVersion), 0, 0, 0) > minVer
                    ? new Version((int)Math.Round(majorVersion), 0, 0, 0)
                    : minVer;

            if (maxVer < minVer)
                maxVer = new Version(999, 999, 999, 999);
        }

        lock (ModJava.JavaLock)
        {
            // 选择 Java
            McLaunchLog("Java 版本需求：最低 " + minVer + "，最高 " + maxVer);
            McLaunchJavaSelected = ModJava.JavaSelect("$$", minVer, maxVer, ModMinecraft.McInstanceSelected);
            if (task.IsAborted)
                return;
            if (McLaunchJavaSelected is not null)
            {
                McLaunchLog("选择的 Java：" + McLaunchJavaSelected.ToString);
                return;
            }

            // 无合适的 Java
            if (task.IsAborted)
                return; // 中断加载会导致 JavaSelect 异常地返回空值，误判找不到 Java
            McLaunchLog("无合适的 Java，需要确认是否自动下载");
            string javaCode;
            if (minVer >= new Version(1, 9))
            {
                javaCode = minVer.Major.ToString();
            }
            else if (maxVer < new Version(1, 8))
            {
                if (ModMinecraft.McInstanceSelected.Info.HasForge)
                    ModMain.MyMsgBox(
                        $"你需要先安装 LegacyJavaFixer Mod，或安装 Java 7 才能启动该版本。{"\r\n"}请自行搜索并安装 Java 7，安装后在 设置 → 启动选项 → 游戏 Java 中重新搜索或导入。",
                        "未找到 Java");
                else
                    ModMain.MyMsgBox(
                        $"你需要安装 Java 7 才能启动该版本。{"\r\n"}请自行搜索并安装 Java 7，安装后在 设置 → 启动选项 → 游戏 Java 中重新搜索或导入。",
                        "未找到 Java");
                throw new Exception("$$");
            }
            else if (minVer > new Version(1, 8, 0, 140) && maxVer < new Version(1, 8, 0, 321))
            {
                ModMain.MyMsgBox(
                    $"你需要安装 Java 8u141 ~ 8u320 才能启动该版本。{"\r\n"}请自行搜索并安装，安装后在 设置 → 启动选项 → 游戏 Java 中重新搜索或导入。",
                    "未找到 Java");
                throw new Exception("$$");
            }
            else if (minVer > new Version(1, 8, 0, 140))
            {
                ModMain.MyMsgBox(
                    $"你需要安装 Java 8u141 或更高版本的 Java 8 才能启动该版本。{"\r\n"}请自行搜索并安装，安装后在 设置 → 启动选项 → 游戏 Java 中重新搜索或导入。",
                    "未找到 Java");
                throw new Exception("$$");
            }
            else
            {
                javaCode = 8.ToString();
            }

            if (!ModJava.JavaDownloadConfirm($"Java {javaCode}"))
                throw new Exception("$$");
            // 开始自动下载
            var javaLoader = ModJava.GetJavaDownloadLoader();
            try
            {
                javaLoader.Start(recommendedComponent ?? javaCode, true); // 在 Java 22+ 时优先使用 Mojang 提供的 Component 字段
                while (javaLoader.State == ModBase.LoadState.Loading && !task.IsAborted)
                {
                    task.Progress = javaLoader.Progress;
                    Thread.Sleep(10);
                }
            }
            finally
            {
                javaLoader.Abort(); // 确保取消时中止 Java 下载
            }

            // 检查下载结果
            McLaunchJavaSelected = ModJava.JavaSelect("$$", minVer, maxVer, ModMinecraft.McInstanceSelected);
            if (task.IsAborted)
                return;
            if (McLaunchJavaSelected is not null)
            {
                McLaunchLog("选择的 Java：" + McLaunchJavaSelected);
            }
            else
            {
                ModMain.Hint("没有可用的 Java，已取消启动！", ModMain.HintType.Critical);
                throw new Exception("$$");
            }
        }
    }

    #endregion

    #region 启动参数

    public class LaunchArgument
    {
        private readonly List<string> _features = new();

        public LaunchArgument(ModMinecraft.McInstance Minecraft)
        {
            var curArgu = string.Empty;
            if (Minecraft.IsOldJson)
                _features = Minecraft.JsonObject["minecraftArguments"].ToString().Split(' ').ToList();
            else
                foreach (var item in Minecraft.JsonObject["arguments"]["game"])
                    if (item.Type == JTokenType.String)
                        _features.Add(item.ToString());
                    else if (item.Type == JTokenType.Object)
                        _features.AddRange(item["value"].Select(x => x.ToString()));
        }

        public object HasArguments(string key)
        {
            return _features.Contains(key);
        }
    }

    private static string McLaunchArgument;

    /// <summary>
    ///     释放 Java Wrapper 并返回完整文件路径。
    /// </summary>
    public static string ExtractJavaWrapper()
    {
        var WrapperPath = ModBase.PathPure + "JavaWrapper.jar";
        ModBase.Log("[Java] 选定的 Java Wrapper 路径：" + WrapperPath);
        lock (ExtractJavaWrapperLock) // 避免 OptiFine 和 Forge 安装时同时释放 Java Wrapper 导致冲突
        {
            try
            {
                WriteJavaWrapper(WrapperPath);
            }
            catch (Exception ex)
            {
                if (File.Exists(WrapperPath))
                {
                    // 因为未知原因 Java Wrapper 可能变为只读文件（#4243）
                    ModBase.Log(ex, "Java Wrapper 文件释放失败，但文件已存在，将在删除后尝试重新生成", ModBase.LogLevel.Developer);
                    try
                    {
                        File.Delete(WrapperPath);
                        WriteJavaWrapper(WrapperPath);
                    }
                    catch (Exception ex2)
                    {
                        ModBase.Log(ex2, "Java Wrapper 文件重新释放失败，将尝试更换文件名重新生成", ModBase.LogLevel.Developer);
                        WrapperPath = ModBase.PathPure + "JavaWrapper2.jar";
                        try
                        {
                            WriteJavaWrapper(WrapperPath);
                        }
                        catch (Exception ex3)
                        {
                            throw new FileNotFoundException("释放 Java Wrapper 最终尝试失败", ex3);
                        }
                    }
                }
                else
                {
                    throw new FileNotFoundException("释放 Java Wrapper 失败", ex);
                }
            }
        }

        return WrapperPath;
    }

    private static readonly object ExtractJavaWrapperLock = new();

    private static void WriteJavaWrapper(string Path)
    {
        ModBase.WriteFile(Path, ModBase.GetResourceStream("Resources/java-wrapper.jar"));
    }

    /// <summary>
    ///     释放 linkd 并返回完整文件路径。
    /// </summary>
    public static string ExtractLinkD()
    {
        var LinkDPath = ModBase.PathPure + "linkd.exe";
        lock (ExtractLinkDLock) // 避免 OptiFine 和 Forge 安装时同时释放 Java Wrapper 导致冲突
        {
            try
            {
                WriteLinkD(LinkDPath);
            }
            catch (Exception ex)
            {
                if (File.Exists(LinkDPath))
                {
                    ModBase.Log(ex, "linkd 文件释放失败，但文件已存在，将在删除后尝试重新生成", ModBase.LogLevel.Developer);
                    try
                    {
                        File.Delete(LinkDPath);
                        WriteLinkD(LinkDPath);
                    }
                    catch (Exception ex2)
                    {
                        throw new FileNotFoundException("释放 linkd 失败", ex2);
                    }
                }
                else
                {
                    throw new FileNotFoundException("释放 linkd 失败", ex);
                }
            }
        }

        return LinkDPath;
    }

    private static readonly object ExtractLinkDLock = new();

    private static void WriteLinkD(string Path)
    {
        ModBase.WriteFile(Path, ModBase.GetResourceStream("Resources/linkd.exe"));
    }

    /// <summary>
    ///     判断是否使用 RetroWrapper。
    ///     TODO: 在更换为 Drop 比较版本号后可能不准确，需要测试确认。
    /// </summary>
    private static bool McLaunchNeedsRetroWrapper(ModMinecraft.McInstance Mc)
    {
        return Conversions.ToBoolean((Mc.ReleaseTime >= new DateTime(2013, 6, 25) && Mc.Info.Drop == 99) ||
                                     (Mc.Info.Drop < 60 && Mc.Info.Drop != 99 &&
                                      !(bool)Config.Launch.DisableRw &&
                                      !(bool)ModBase.Setup.Get("VersionAdvanceDisableRW", Mc))); // <1.6
    }

    /// <summary>
    /// 获取实例所依赖的 LWJGL 版本
    /// </summary>
    private static string McLaunchGetLwjglVersion(ModMinecraft.McInstance mc)
    {
        foreach (ModMinecraft.McLibToken library in ModMinecraft.McLibListGet(mc, false))
        {
            if (string.IsNullOrWhiteSpace(library.OriginalName))
                continue;

            string[] parts = library.OriginalName.Split(':');
            if (parts.Length >= 3 &&
                parts[0].Equals("org.lwjgl", StringComparison.OrdinalIgnoreCase) &&
                parts[1].Equals("lwjgl", StringComparison.OrdinalIgnoreCase))
            {
                return parts[2];
            }
        }

        return null;
    }

    /// <summary>
    /// 判断是否启用了针对 Minecraft 26.1 的性能问题补丁
    /// </summary>
    private static bool McLaunchUsesLwjglUnsafeAgent(ModMinecraft.McInstance mc)
    {
        if (McLaunchGetLwjglVersion(mc) == "3.4.1")
        {
            bool globalDisabled = Config.Launch.DisableLwjglUnsafeAgent;
            bool instanceDisabled = Config.Instance.DisableLwjglUnsafeAgent[mc];

            return !globalDisabled && !instanceDisabled;
        }
        else
        {
            return false;
        }
    }

    // 主方法，合并 Jvm、Game、Replace 三部分的参数数据
    private static void McLaunchArgumentMain(ModLoader.LoaderTask<string, List<ModMinecraft.McLibToken>> Loader)
    {
        McLaunchLog("开始获取 Minecraft 启动参数");
        // 获取基准字符串与参数信息
        string Arguments;
        if (ModMinecraft.McInstanceSelected.JsonObject["arguments"] is not null &&
            ModMinecraft.McInstanceSelected.JsonObject["arguments"]["jvm"] is not null)
        {
            McLaunchLog("获取新版 JVM 参数");
            Arguments = McLaunchArgumentsJvmNew(ModMinecraft.McInstanceSelected);
            McLaunchLog("新版 JVM 参数获取成功：");
            McLaunchLog(Arguments);
        }
        else
        {
            McLaunchLog("获取旧版 JVM 参数");
            Arguments = McLaunchArgumentsJvmOld(ModMinecraft.McInstanceSelected);
            McLaunchLog("旧版 JVM 参数获取成功：");
            McLaunchLog(Arguments);
        }

        if (!string.IsNullOrEmpty(
                (string)ModMinecraft.McInstanceSelected.JsonObject["minecraftArguments"])) // 有的实例 JSON 中是空字符串
        {
            McLaunchLog("获取旧版 Game 参数");
            Arguments += " " + McLaunchArgumentsGameOld(ModMinecraft.McInstanceSelected);
            McLaunchLog("旧版 Game 参数获取成功");
        }

        if (ModMinecraft.McInstanceSelected.JsonObject["arguments"] is not null &&
            ModMinecraft.McInstanceSelected.JsonObject["arguments"]["game"] is not null)
        {
            McLaunchLog("获取新版 Game 参数");
            Arguments += " " + McLaunchArgumentsGameNew(ModMinecraft.McInstanceSelected);
            McLaunchLog("新版 Game 参数获取成功");
        }

        // 编码参数（#4700、#5892、#5909）
        if (McLaunchJavaSelected.Installation.MajorVersion > 8)
        {
            if (!Arguments.Contains("-Dstdout.encoding="))
                Arguments = "-Dstdout.encoding=UTF-8 " + Arguments;
            if (!Arguments.Contains("-Dstderr.encoding="))
                Arguments = "-Dstderr.encoding=UTF-8 " + Arguments;
        }

        if (McLaunchJavaSelected.Installation.MajorVersion >= 18)
            if (!Arguments.Contains("-Dfile.encoding="))
                Arguments = "-Dfile.encoding=COMPAT " + Arguments;
        // MJSB
        Arguments = Arguments.Replace(" -Dos.name=Windows 10", " -Dos.name=\"Windows 10\"");
        // 全屏
        if (Conversions.ToBoolean(
                Operators.ConditionalCompareObjectEqual(Config.Launch.GameWindowMode, 0, false)))
            Arguments += " --fullscreen";
        // 由 Option 传入的额外参数
        foreach (var Arg in CurrentLaunchOptions.ExtraArgs)
            Arguments += " " + Arg.Trim();
        // 自定义参数
        var ArgumentGame =
            Conversions.ToString(ModBase.Setup.Get("VersionAdvanceGame", ModMinecraft.McInstanceSelected));
        Arguments = Conversions.ToString(Arguments + Operators.ConcatenateObject(" ",
            string.IsNullOrEmpty(ArgumentGame) ? Config.Launch.GameArgs : ArgumentGame));
        // 替换参数
        var ReplaceArguments = McLaunchArgumentsReplace(ModMinecraft.McInstanceSelected, ref Loader);
        if (string.IsNullOrWhiteSpace(ReplaceArguments["${version_type}"]))
        {
            // 若自定义信息为空，则去掉该部分
            Arguments = Arguments.Replace(" --versionType ${version_type}", "");
            ReplaceArguments["${version_type}"] = "\"\"";
        }

        var FinalArguments = "";
        foreach (var ArgumentRaw in Arguments.Split(" "))
        {
            var Argument = ArgumentRaw;
            foreach (var Entry in ReplaceArguments)
                Argument = Argument.Replace(Entry.Key, Entry.Value);
            if ((Argument.Contains(" ") || Argument.Contains(@":\")) && !Argument.EndsWithF("\""))
                Argument = $"\"{Argument}\"";
            FinalArguments += Argument + " ";
        }

        FinalArguments = FinalArguments.TrimEnd();
        // 进存档
        var WorldName = CurrentLaunchOptions.WorldName;
        if (WorldName is not null) FinalArguments += $" --quickPlaySingleplayer \"{WorldName}\"";
        // 进服
        var Server = Conversions.ToString(string.IsNullOrEmpty(CurrentLaunchOptions.ServerIp)
            ? ModBase.Setup.Get("VersionServerEnter", ModMinecraft.McInstanceSelected)
            : CurrentLaunchOptions.ServerIp);
        if (string.IsNullOrWhiteSpace(WorldName) && !string.IsNullOrWhiteSpace(Server))
        {
            if (ModMinecraft.McInstanceSelected.ReleaseTime > new DateTime(2023, 4, 4))
            {
                // QuickPlay
                FinalArguments += $" --quickPlayMultiplayer \"{Server}\"";
            }
            else
            {
                // 老版本
                if (Server.Contains(":"))
                    // 包含端口号
                    FinalArguments += " --server " + Server.Split(":")[0] + " --port " + Server.Split(":")[1];
                else
                    // 不包含端口号
                    FinalArguments += " --server " + Server + " --port 25565";
                if (ModMinecraft.McInstanceSelected.Info.HasOptiFine)
                    ModMain.Hint("OptiFine 与自动进入服务器可能不兼容，有概率导致材质丢失甚至游戏崩溃！", ModMain.HintType.Critical);
            }
        }

        // 输出
        McLaunchLog("Minecraft 启动参数：");
        McLaunchLog(FinalArguments);
        McLaunchArgument = FinalArguments;
    }

    // Jvm 部分（第一段）
    private static string McLaunchArgumentsJvmOld(ModMinecraft.McInstance instance)
    {
        // 存储以空格为间隔的启动参数列表
        var DataList = new List<string>();

        // 输出固定参数
        DataList.Add("-XX:HeapDumpPath=MojangTricksIntelDriversForPerformance_javaw.exe_minecraft.exe.heapdump");
        var ArgumentJvm = Conversions.ToString(ModBase.Setup.Get("VersionAdvanceJvm", ModMinecraft.McInstanceSelected));
        if (string.IsNullOrEmpty(ArgumentJvm))
            ArgumentJvm = Conversions.ToString(Config.Launch.JvmArgs);
        if (!ArgumentJvm.Contains("-Dlog4j2.formatMsgNoLookups=true"))
            ArgumentJvm += " -Dlog4j2.formatMsgNoLookups=true";
        ArgumentJvm = ArgumentJvm.Replace(" -XX:MaxDirectMemorySize=256M", ""); // #3511 的清理
        DataList.Insert(0, ArgumentJvm); // 可变 JVM 参数
        DataList.Add("-Xmn" +
                     Math.Floor(PageInstanceSetup.GetRam(ModMinecraft.McInstanceSelected,
                         !McLaunchJavaSelected.Installation.Is64Bit) * 1024d * 0.15d) + "m");
        DataList.Add("-Xmx" +
                     Math.Floor(PageInstanceSetup.GetRam(ModMinecraft.McInstanceSelected,
                         !McLaunchJavaSelected.Installation.Is64Bit) * 1024d) + "m");
        DataList.Add("\"-Djava.library.path=" + GetNativesFolder() + "\"");
        DataList.Add("-cp ${classpath}"); // 把支持库添加进启动参数表

        // Authlib-Injector
        if (McLoginLoader.Output.Type == "Auth")
        {
            if (McLaunchJavaSelected.Installation.MajorVersion >= 6)
                DataList.Add("-Djavax.net.ssl.trustStoreType=WINDOWS-ROOT"); // 信任系统根证书（Meloong-Git/#5252）
            var Server = McLoginAuthLoader.Input.BaseUrl.Replace("/authserver", "");
            try
            {
                var Response = Requester.FetchString(Server);
                DataList.Insert(0,
                    "-javaagent:\"" + ModBase.PathPure + "authlib-injector.jar\"=" + Server +
                    " -Dauthlibinjector.side=client" + " -Dauthlibinjector.yggdrasil.prefetched=" +
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(Response)));
            }
            catch (WebException ex)
            {
                throw new Exception(
                    $"无法连接到第三方登录服务器（{Server ?? null}）{"\r\n"}详细信息：" + ex.InnerException, ex);
            }
            catch (Exception ex)
            {
                throw new Exception($"无法连接到第三方登录服务器（{Server ?? null}）", ex);
            }
        }

        // LWJGL Unsafe Agent
        if (McLaunchUsesLwjglUnsafeAgent(ModMinecraft.McInstanceSelected))
        {
            DataList.Insert(0, $"-javaagent:\"{ModBase.PathPure}lwjgl-unsafe-agent.jar\"");
        }

        if (Config.Instance.UseDebugLof4j2Config[instance.PathIndie])
        {
            if (ModMinecraft.McInstanceSelected.ReleaseTime.Year >= 2017)
                DataList.Insert(0, "-Dlog4j.configurationFile=\"" + LaunchEnvUtils.ExtractDebugLog4j2Config() + "\"");
            else
                DataList.Insert(0,
                    "-Dlog4j.configurationFile=\"" + LaunchEnvUtils.ExtractLegacyDebugLog4j2Config() + "\"");
        }

        // 渲染器
        var Renderer = 0;
        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(
                ModBase.Setup.Get("VersionAdvanceRenderer", ModMinecraft.McInstanceSelected), 0, false)))
            Renderer = Conversions.ToInteger(
                Operators.SubtractObject(ModBase.Setup.Get("VersionAdvanceRenderer", ModMinecraft.McInstanceSelected),
                    1));
        else
            Renderer = Conversions.ToInteger(Config.Launch.Renderer);
        var MesaLoaderWindowsVersion = "25.3.5";
        var MesaLoaderWindowsTargetFile =
            ModBase.PathPure + @"\mesa-loader-windows\" + MesaLoaderWindowsVersion + @"\Loader.jar";

        if (Renderer != 0)
            DataList.Insert(0,
                "-javaagent:\"" + MesaLoaderWindowsTargetFile + "\"=" +
                (Renderer == 1 ? "llvmpipe" : Renderer == 2 ? "d3d12" : "zink"));

        // 设置代理
        if (Config.Instance.UseProxy[instance.PathIndie] && Config.Network.HttpProxy.Type.Equals(2) &&
            !string.IsNullOrWhiteSpace(Config.Network.HttpProxy.CustomAddress))
            try
            {
                var ProxyAddress = new Uri(Conversions.ToString(Config.Network.HttpProxy.CustomAddress));
                DataList.Add(
                    $"-D{(ProxyAddress.Scheme.StartsWithF("https:") ? "https" : "http")}.proxyHost={ProxyAddress.AbsoluteUri}");
                DataList.Add(
                    $"-D{(ProxyAddress.Scheme.StartsWithF("https:") ? "https" : "http")}.proxyPort={ProxyAddress.Port}");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "添加代理信息到游戏失败，放弃加入", ModBase.LogLevel.Hint);
            }

        // 添加 Java Wrapper 作为主 Jar
        if (Conversions.ToBoolean(ModBase.IsUtf8CodePage() && !(bool)Config.Launch.DisableJlw &&
                                  !(bool)ModBase.Setup.Get("VersionAdvanceDisableJLW",
                                      ModMinecraft.McInstanceSelected)))
        {
            if (McLaunchJavaSelected.Installation.MajorVersion >= 9)
                DataList.Add("--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED");
            DataList.Add("-Doolloo.jlw.tmpdir=\"" + ModBase.PathPure.TrimEnd('\\') + "\"");
            DataList.Add("-jar \"" + ExtractJavaWrapper() + "\"");
        }

        // 添加 MainClass
        if (instance.JsonObject["mainClass"] is null) throw new Exception("实例 JSON 中没有 mainClass 项！");

        DataList.Add((string)instance.JsonObject["mainClass"]);

        return DataList.Join(" ");
    }

    private static string McLaunchArgumentsJvmNew(ModMinecraft.McInstance instance)
    {
        var DataList = new List<string>();

        // 获取 Json 中的 DataList
        var currentInstance = instance;
        NextInstance: ;

        if (currentInstance.JsonObject["arguments"] is not null &&
            currentInstance.JsonObject["arguments"]["jvm"] is not null)
            foreach (var SubJson in currentInstance.JsonObject["arguments"]["jvm"])
                if (SubJson.Type == JTokenType.String)
                {
                    // 字符串类型
                    DataList.Add(SubJson.ToString());
                }
                // 非字符串类型
                else if (ModMinecraft.McJsonRuleCheck(SubJson["rules"]))
                {
                    // 满足准则
                    if (SubJson["value"].Type == JTokenType.String)
                        DataList.Add(SubJson["value"].ToString());
                    else
                        foreach (var value in SubJson["value"])
                            DataList.Add(value.ToString());
                }

        if (!string.IsNullOrEmpty(currentInstance.InheritInstanceName))
        {
            currentInstance = new ModMinecraft.McInstance(currentInstance.InheritInstanceName);
            goto NextInstance;
        }

        // 内存、Log4j 防御参数等
        ModSecret.SecretLaunchJvmArgs(ref DataList);

        // Authlib-Injector
        if (McLoginLoader.Output.Type == "Auth")
        {
            if (McLaunchJavaSelected.Installation.MajorVersion >= 6)
                DataList.Add("-Djavax.net.ssl.trustStoreType=WINDOWS-ROOT"); // 信任系统根证书（Meloong-Git/#5252）
            var Server = McLoginAuthLoader.Input.BaseUrl.Replace("/authserver", "");
            try
            {
                var Response = Conversions.ToString(ModNet.NetGetCodeByRequestRetry(Server, Encoding.UTF8));
                DataList.Insert(0,
                    "-javaagent:\"" + ModBase.PathPure + "authlib-injector.jar\"=" + Server +
                    " -Dauthlibinjector.side=client" + " -Dauthlibinjector.yggdrasil.prefetched=" +
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(Response)));
            }
            catch (Exception ex)
            {
                throw new Exception("无法连接到第三方登录服务器（" + (Server ?? null) + "）", ex);
            }
        }

        if (Config.Instance.UseDebugLof4j2Config[instance.PathIndie])
        {
            if (ModMinecraft.McInstanceSelected.ReleaseTime.Year >= 2017)
                DataList.Insert(0, "-Dlog4j.configurationFile=\"" + LaunchEnvUtils.ExtractDebugLog4j2Config() + "\"");
            else
                DataList.Insert(0,
                    "-Dlog4j.configurationFile=\"" + LaunchEnvUtils.ExtractLegacyDebugLog4j2Config() + "\"");
        }

        // 渲染器
        var Renderer = 0;
        if (Conversions.ToBoolean(Operators.ConditionalCompareObjectNotEqual(
                ModBase.Setup.Get("VersionAdvanceRenderer", ModMinecraft.McInstanceSelected), 0, false)))
            Renderer = Conversions.ToInteger(
                Operators.SubtractObject(ModBase.Setup.Get("VersionAdvanceRenderer", ModMinecraft.McInstanceSelected),
                    1));
        else
            Renderer = Conversions.ToInteger(Config.Launch.Renderer);
        var MesaLoaderWindowsVersion = "25.3.5";
        var MesaLoaderWindowsTargetFile =
            ModBase.PathPure + @"\mesa-loader-windows\" + MesaLoaderWindowsVersion + @"\Loader.jar";

        if (Renderer != 0)
            DataList.Insert(0,
                "-javaagent:\"" + MesaLoaderWindowsTargetFile + "\"=" +
                (Renderer == 1 ? "llvmpipe" : Renderer == 2 ? "d3d12" : "zink"));

        // 设置代理
        if (Config.Instance.UseProxy[instance.PathIndie] && Config.Network.HttpProxy.Type.Equals(2) &&
            !string.IsNullOrWhiteSpace(Config.Network.HttpProxy.CustomAddress))
            try
            {
                var ProxyAddress = new Uri(Conversions.ToString(Config.Network.HttpProxy.CustomAddress));
                DataList.Add(
                    $"-D{(ProxyAddress.Scheme.StartsWithF("https:") ? "https" : "http")}.proxyHost={ProxyAddress.AbsoluteUri}");
                DataList.Add(
                    $"-D{(ProxyAddress.Scheme.StartsWithF("https:") ? "https" : "http")}.proxyPort={ProxyAddress.Port}");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "添加代理信息到游戏失败，放弃加入", ModBase.LogLevel.Hint);
            }

        // 添加 RetroWrapper 相关参数
        if (McLaunchNeedsRetroWrapper(instance))
            // https://github.com/NeRdTheNed/RetroWrapper/wiki/RetroWrapper-flags
            DataList.Add("-Dretrowrapper.doUpdateCheck=false");
        // 添加 Java Wrapper 作为主 Jar
        if (Conversions.ToBoolean(ModBase.IsUtf8CodePage() && !(bool)Config.Launch.DisableJlw &&
                                  !(bool)ModBase.Setup.Get("VersionAdvanceDisableJLW",
                                      ModMinecraft.McInstanceSelected)))
        {
            if (McLaunchJavaSelected.Installation.MajorVersion >= 9)
                DataList.Add("--add-exports cpw.mods.bootstraplauncher/cpw.mods.bootstraplauncher=ALL-UNNAMED");
            DataList.Add("-Doolloo.jlw.tmpdir=\"" + ModBase.PathPure.TrimEnd('\\') + "\"");
            DataList.Add("-jar \"" + ExtractJavaWrapper() + "\"");
        }


        // 将 "-XXX" 与后面 "XXX" 合并到一起
        // 如果不合并，会导致 Forge 1.17 启动无效，它有两个 --add-exports，进一步导致其中一个在后面被去重
        var DeDuplicateDataList = new List<string>();
        for (int i = 0, loopTo = DataList.Count - 1; i <= loopTo; i++)
        {
            var CurrentEntry = DataList[i];
            if (DataList[i].StartsWithF("-"))
                while (i < DataList.Count - 1)
                {
                    if (DataList[i + 1].StartsWithF("-")) break;

                    i += 1;
                    CurrentEntry += " " + DataList[i];
                }

            DeDuplicateDataList.Add(CurrentEntry.Trim().Replace("McEmu= ", "McEmu="));
        }

        // #3511 的清理
        DeDuplicateDataList.Remove("-XX:MaxDirectMemorySize=256M");

        // 去重
        var Result = DeDuplicateDataList.Distinct().ToList().Join(" ");

        // 添加 MainClass
        if (instance.JsonObject["mainClass"] is null) throw new Exception("实例 JSON 中没有 mainClass 项！");

        Result += " " + instance.JsonObject["mainClass"];

        return Result;
    }

    // Game 部分（第二段）
    private static string McLaunchArgumentsGameOld(ModMinecraft.McInstance Version)
    {
        var DataList = new List<string>();

        // 添加 RetroWrapper 相关参数
        if (McLaunchNeedsRetroWrapper(Version)) DataList.Add("--tweakClass com.zero.retrowrapper.RetroTweaker");

        // 本地化 Minecraft 启动信息
        var BasicString = Version.JsonObject["minecraftArguments"].ToString();
        if (!BasicString.Contains("--height"))
            BasicString += " --height ${resolution_height} --width ${resolution_width}";
        DataList.Add(BasicString);

        var Result = DataList.Join(" ");

        // 特别改变 OptiFineTweaker
        if ((Version.Info.HasForge || Version.Info.HasLiteLoader) && Version.Info.HasOptiFine)
        {
            // 把 OptiFineForgeTweaker 放在最后，不然会导致崩溃！
            if (Result.Contains("--tweakClass optifine.OptiFineForgeTweaker"))
            {
                ModBase.Log("[Launch] 发现正确的 OptiFineForge TweakClass，目前参数：" + Result);
                Result = Result.Replace(" --tweakClass optifine.OptiFineForgeTweaker", "")
                             .Replace("--tweakClass optifine.OptiFineForgeTweaker ", "") +
                         " --tweakClass optifine.OptiFineForgeTweaker";
            }

            if (Result.Contains("--tweakClass optifine.OptiFineTweaker"))
            {
                ModBase.Log("[Launch] 发现错误的 OptiFineForge TweakClass，目前参数：" + Result);
                Result = Result.Replace(" --tweakClass optifine.OptiFineTweaker", "")
                             .Replace("--tweakClass optifine.OptiFineTweaker ", "") +
                         " --tweakClass optifine.OptiFineForgeTweaker";
                try
                {
                    ModBase.WriteFile(Version.PathInstance + Version.Name + ".json",
                        ModBase.ReadFile(Version.PathInstance + Version.Name + ".json")
                            .Replace("optifine.OptiFineTweaker", "optifine.OptiFineForgeTweaker"));
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "替换 OptiFineForge TweakClass 失败");
                }
            }
        }

        return Result;
    }

    private static string McLaunchArgumentsGameNew(ModMinecraft.McInstance instance)
    {
        string McLaunchArgumentsGameNewRet = default;
        var dataList = new List<string>();

        // 获取 Json 中的 DataList
        var currentInstance = instance;
        NextInstance: ;

        if (currentInstance.JsonObject["arguments"] is not null &&
            currentInstance.JsonObject["arguments"]["game"] is not null)
            foreach (var SubJson in currentInstance.JsonObject["arguments"]["game"])
                if (SubJson.Type == JTokenType.String)
                {
                    // 字符串类型
                    dataList.Add(SubJson.ToString());
                }
                // 非字符串类型
                else if (ModMinecraft.McJsonRuleCheck(SubJson["rules"]))
                {
                    // 满足准则
                    if (SubJson["value"].Type == JTokenType.String)
                        dataList.Add(SubJson["value"].ToString());
                    else
                        foreach (var value in SubJson["value"])
                            dataList.Add(value.ToString());
                }

        if (!string.IsNullOrEmpty(currentInstance.InheritInstanceName))
        {
            currentInstance = new ModMinecraft.McInstance(currentInstance.InheritInstanceName);
            goto NextInstance;
        }

        // 将 "-XXX" 与后面 "XXX" 合并到一起
        // 如果不进行合并 Impact 会启动无效，它有两个 --tweakclass
        var DeDuplicateDataList = new List<string>();
        for (int i = 0, loopTo = dataList.Count - 1; i <= loopTo; i++)
        {
            var CurrentEntry = dataList[i];
            if (dataList[i].StartsWithF("-"))
                while (i < dataList.Count - 1)
                {
                    if (dataList[i + 1].StartsWithF("-")) break;

                    i += 1;
                    CurrentEntry += " " + dataList[i];
                }

            DeDuplicateDataList.Add(CurrentEntry);
        }

        // 去重
        McLaunchArgumentsGameNewRet = DeDuplicateDataList.Distinct().ToList().Join(" ");

        // 特别改变 OptiFineTweaker
        if ((instance.Info.HasForge || instance.Info.HasLiteLoader) && instance.Info.HasOptiFine)
        {
            // 把 OptiFineForgeTweaker 放在最后，不然会导致崩溃！
            if (McLaunchArgumentsGameNewRet.Contains("--tweakClass optifine.OptiFineForgeTweaker"))
            {
                ModBase.Log("[Launch] 发现正确的 OptiFineForge TweakClass，目前参数：" + McLaunchArgumentsGameNewRet);
                McLaunchArgumentsGameNewRet =
                    McLaunchArgumentsGameNewRet.Replace(" --tweakClass optifine.OptiFineForgeTweaker", "")
                        .Replace("--tweakClass optifine.OptiFineForgeTweaker ", "") +
                    " --tweakClass optifine.OptiFineForgeTweaker";
            }

            if (McLaunchArgumentsGameNewRet.Contains("--tweakClass optifine.OptiFineTweaker"))
            {
                ModBase.Log("[Launch] 发现错误的 OptiFineForge TweakClass，目前参数：" + McLaunchArgumentsGameNewRet);
                McLaunchArgumentsGameNewRet =
                    McLaunchArgumentsGameNewRet.Replace(" --tweakClass optifine.OptiFineTweaker", "")
                        .Replace("--tweakClass optifine.OptiFineTweaker ", "") +
                    " --tweakClass optifine.OptiFineForgeTweaker";
                try
                {
                    ModBase.WriteFile(instance.PathInstance + instance.Name + ".json",
                        ModBase.ReadFile(instance.PathInstance + instance.Name + ".json")
                            .Replace("optifine.OptiFineTweaker", "optifine.OptiFineForgeTweaker"));
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "替换 OptiFineForge TweakClass 失败");
                }
            }
        }

        return McLaunchArgumentsGameNewRet;
    }

    // 替换 Arguments
    private static Dictionary<string, string> McLaunchArgumentsReplace(ModMinecraft.McInstance instance,
        ref ModLoader.LoaderTask<string, List<ModMinecraft.McLibToken>> loader)
    {
        var GameArguments = new Dictionary<string, string>();

        // 基础参数
        GameArguments.Add("${classpath_separator}", ";");
        GameArguments.Add("${natives_directory}", ModBase.ShortenPath(GetNativesFolder()));
        GameArguments.Add("${library_directory}", ModBase.ShortenPath(ModMinecraft.McFolderSelected + "libraries"));
        GameArguments.Add("${libraries_directory}", ModBase.ShortenPath(ModMinecraft.McFolderSelected + "libraries"));
        GameArguments.Add("${launcher_name}", "PCLCE");
        GameArguments.Add("${launcher_version}", ModBase.VersionCode.ToString());
        GameArguments.Add("${version_name}", instance.Name);
        var ArgumentInfo =
            Conversions.ToString(ModBase.Setup.Get("VersionArgumentInfo", ModMinecraft.McInstanceSelected));
        GameArguments.Add("${version_type}",
            Conversions.ToString(string.IsNullOrEmpty(ArgumentInfo)
                ? Config.Launch.TypeInfo
                : ArgumentInfo));
        GameArguments.Add("${game_directory}",
            ModBase.ShortenPath(Strings.Left(ModMinecraft.McInstanceSelected.PathIndie,
                ModMinecraft.McInstanceSelected.PathIndie.Count() - 1)));
        GameArguments.Add("${assets_root}", ModBase.ShortenPath(ModMinecraft.McFolderSelected + "assets"));
        GameArguments.Add("${user_properties}", "{}");
        GameArguments.Add("${auth_player_name}", McLoginLoader.Output.Name);
        GameArguments.Add("${auth_uuid}", McLoginLoader.Output.Uuid);
        GameArguments.Add("${auth_access_token}", McLoginLoader.Output.AccessToken);
        GameArguments.Add("${access_token}", McLoginLoader.Output.AccessToken);
        GameArguments.Add("${auth_session}", McLoginLoader.Output.AccessToken);
        GameArguments.Add("${user_type}", "msa"); // #1221

        // 窗口尺寸参数
        Size GameSize;
        switch (Config.Launch.GameWindowMode)
        {
            case var @case when Operators.ConditionalCompareObjectEqual(@case, 2, false): // 与启动器尺寸一致
            {
                Size Result;
                ModBase.RunInUiWait(() => Result = new Size(ModBase.GetPixelSize(ModMain.FrmMain.PanForm.ActualWidth),
                    ModBase.GetPixelSize(ModMain.FrmMain.PanForm.ActualHeight)));
                GameSize = Result;
                GameSize.Height -= 29.5d * ModBase.DPI / 96d; // 标题栏高度
                break;
            }
            case var case1 when Operators.ConditionalCompareObjectEqual(case1, 3, false): // 自定义
            {
                GameSize = new Size(Math.Max(100, (double)Config.Launch.GameWindowWidth),
                    Math.Max(100, (double)Config.Launch.GameWindowHeight));
                break;
            }

            default:
            {
                GameSize = new Size(854d, 480d);
                break;
            }
        }

        if (ModMinecraft.McInstanceSelected.Info.Drop <= 120 && McLaunchJavaSelected.Installation.MajorVersion <= 8 &&
            McLaunchJavaSelected.Installation.Version.Revision >= 200 &&
            McLaunchJavaSelected.Installation.Version.Revision <= 321 &&
            !ModMinecraft.McInstanceSelected.Info.HasOptiFine && !ModMinecraft.McInstanceSelected.Info.HasForge)
        {
            // 修复 #3463：1.12.2-，JRE 8u200~321 下窗口大小为设置大小的 DPI% 倍
            McLaunchLog($"已应用窗口大小过大修复（{McLaunchJavaSelected.Installation.Version.Revision}）");
            GameSize.Width /= ModBase.DPI / 96d;
            GameSize.Height /= ModBase.DPI / 96d;
        }

        GameArguments.Add("${resolution_width}", Math.Round(GameSize.Width).ToString());
        GameArguments.Add("${resolution_height}", Math.Round(GameSize.Height).ToString());

        // Assets 相关参数
        GameArguments.Add("${game_assets}",
            ModBase.ShortenPath(ModMinecraft.McFolderSelected +
                                @"assets\virtual\legacy")); // 1.5.2 的 pre-1.6 资源索引应与 legacy 合并
        GameArguments.Add("${assets_index_name}", ModMinecraft.McAssetsGetIndexName(instance));

        // 支持库参数
        var LibList = ModMinecraft.McLibListGet(instance, true);
        loader.Output = LibList;
        var CpStrings = new List<string>();
        string OptiFineCp = null;

        // RetroWrapper 释放
        if (McLaunchNeedsRetroWrapper(instance))
        {
            var WrapperPath = ModMinecraft.McFolderSelected + @"libraries\retrowrapper\RetroWrapper.jar";
            try
            {
                ModBase.WriteFile(WrapperPath, ModBase.GetResourceStream("Resources/retro-wrapper.jar"));
                CpStrings.Add(WrapperPath);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "RetroWrapper 释放失败");
            }
        }

        // LWJGL Unsafe Agent 释放
        if (McLaunchUsesLwjglUnsafeAgent(instance))
        {
            string AgentPath = ModBase.PathPure + "lwjgl-unsafe-agent.jar";
            try
            {
                ModBase.WriteFile(AgentPath, ModBase.GetResourceStream("Resources/lwjgl-unsafe-agent.jar"));
                CpStrings.Add(AgentPath);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "LWJGL Unsafe Agent 释放失败");
            }
        }

        foreach (var Library in LibList)
        {
            if (Library.IsNatives)
                continue;
            if (Library.Name is not null && Library.Name == "optifine:OptiFine")
                OptiFineCp = Library.LocalPath;
            else
                CpStrings.Add(Library.LocalPath);
        }

        foreach (var library in Config.Instance.ClasspathHead[instance.PathInstance].Split(";")) // 自定义 Classpath 头部
        {
            if (string.IsNullOrWhiteSpace(library))
                continue;
            CpStrings.Insert(0, library);
        }

        if (OptiFineCp is not null)
            CpStrings.Insert(CpStrings.Count - 2, OptiFineCp); // OptiFine 的总是需要放到倒数第二位
        GameArguments.Add("${classpath}", CpStrings.Select(c => ModBase.ShortenPath(c)).Join(";"));

        return GameArguments;
    }

    #endregion

    #region 解压 Natives

    private static void McLaunchNatives(ModLoader.LoaderTask<List<ModMinecraft.McLibToken>, int> Loader)
    {
        // 创建文件夹
        var Target = GetNativesFolder() + @"\";
        Directory.CreateDirectory(Target);

        // 解压文件
        McLaunchLog("正在解压 Natives 文件");
        var ExistFiles = new List<string>();
        foreach (var Native in Loader.Input)
        {
            if (!Native.IsNatives)
                continue;
            ZipArchive Zip;
            try
            {
                Zip = new ZipArchive(new FileStream(Native.LocalPath, FileMode.Open));
            }
            catch (InvalidDataException ex)
            {
                ModBase.Log(ex, "打开 Natives 文件失败（" + Native.LocalPath + "）");
                File.Delete(Native.LocalPath);
                throw new Exception("无法打开 Natives 文件（" + Native.LocalPath + "），该文件可能已损坏，请重新尝试启动游戏");
            }

            foreach (var Entry in Zip.Entries)
            {
                var FileName = Entry.FullName;
                if (FileName.EndsWithF(".dll", true))
                {
                    // 实际解压文件的步骤
                    var FilePath = Target + FileName;
                    ExistFiles.Add(FilePath);
                    var OriginalFile = new FileInfo(FilePath);
                    if (OriginalFile.Exists)
                    {
                        if (OriginalFile.Length == Entry.Length)
                        {
                            if (ModBase.ModeDebug)
                                McLaunchLog("无需解压：" + FilePath);
                            continue;
                        }

                        // 删除原文件
                        try
                        {
                            File.Delete(FilePath);
                        }
                        catch (UnauthorizedAccessException ex)
                        {
                            McLaunchLog("删除原 dll 访问被拒绝，这通常代表有一个 MC 正在运行，跳过解压：" + FilePath);
                            McLaunchLog("实际的错误信息：" + ex);
                            break;
                        }
                    }

                    // 解压新文件
                    ModBase.WriteFile(FilePath, Entry.Open());
                    McLaunchLog("已解压：" + FilePath);
                }
            }

            if (Zip is not null)
                Zip.Dispose();
        }

        // 删除多余文件
        foreach (var FileName in Directory.GetFiles(Target))
        {
            if (ExistFiles.Contains(FileName))
                continue;
            try
            {
                McLaunchLog("删除：" + FileName);
                File.Delete(FileName);
            }
            catch (UnauthorizedAccessException ex)
            {
                McLaunchLog("删除多余文件访问被拒绝，跳过删除步骤");
                McLaunchLog("实际的错误信息：" + ex);
                return;
            }
        }
    }

    /// <summary>
    ///     获取 Natives 文件夹路径，不以 \ 结尾。
    /// </summary>
    private static string GetNativesFolder()
    {
        var Result = ModMinecraft.McInstanceSelected.PathInstance + ModMinecraft.McInstanceSelected.Name + "-natives";
        if (ModBase.IsGBKEncoding || Result.IsASCII())
            return Result;
        Result = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\.minecraft\bin\natives";
        if (Result.IsASCII())
            return Result;
        return ModBase.OsDrive + @"ProgramData\PCL\natives";
    }

    #endregion

    #region 启动与前后处理

    private static void McLaunchPrerun()
    {
        // 要求 Java 使用高性能显卡
        var javaExePath = McLaunchJavaSelected.Installation.JavawExePath ??
                          McLaunchJavaSelected.Installation.JavaExePath;
        try
        {
            ModMain.SetGPUPreference(javaExePath, Config.Launch.SetGpuPreference);
        }
        catch (Exception ex)
        {
            if (ProcessInterop.IsAdmin() || !Config.Launch.SetGpuPreference)
            {
                ModBase.Log(ex, "直接调整显卡设置失败");
            }
            else
            {
                ModBase.Log(ex, "直接调整显卡设置失败，将以管理员权限重启 PCL 再次尝试");
                try
                {
                    if (ProcessInterop.StartAsAdmin($"--gpu \"{javaExePath}\"").ExitCode ==
                        (int)ModBase.ProcessReturnValues.TaskDone)
                        McLaunchLog("以管理员权限重启 PCL 并调整显卡设置成功");
                    else
                        throw new Exception("调整过程中出现异常");
                }
                catch (Exception exx)
                {
                    ModBase.Log(exx, "调整显卡设置失败，Minecraft 可能会使用默认显卡运行", ModBase.LogLevel.Hint);
                }
            }
        }

        // 更新 launcher_profiles.json
        do
        {
            try
            {
                // 确保可用
                if (!(McLoginLoader.Output.Type == "Microsoft"))
                    break;
                ModMinecraft.McFolderLauncherProfilesJsonCreate(ModMinecraft.McFolderSelected);
                // 构建需要替换的 Json 对象
                var ReplaceJsonString = @"
            {
              ""authenticationDatabase"": {
                ""00000111112222233333444445555566"": {
                  ""username"": """ + McLoginLoader.Output.Name.Replace("\"", "-") + @""",
                  ""profiles"": {
                    ""66666555554444433333222221111100"": {
                        ""displayName"": """ + McLoginLoader.Output.Name + @"""
                    }
                  }
                }
              },
              ""clientToken"": """ + McLoginLoader.Output.ClientToken + @""",
              ""selectedUser"": {
                ""account"": ""00000111112222233333444445555566"", 
                ""profile"": ""66666555554444433333222221111100""
              }
            }";
                var ReplaceJson = (JObject)ModBase.GetJson(ReplaceJsonString);
                // 更新文件
                var Profiles =
                    (JObject)ModBase.GetJson(
                        ModBase.ReadFile(ModMinecraft.McFolderSelected + "launcher_profiles.json"));
                Profiles.Merge(ReplaceJson);
                ModBase.WriteFile(ModMinecraft.McFolderSelected + "launcher_profiles.json", Profiles.ToString(),
                    Encoding: Encoding.GetEncoding("GB18030"));
                McLaunchLog("已更新 launcher_profiles.json");
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "更新 launcher_profiles.json 失败，将在删除文件后重试");
                try
                {
                    File.Delete(ModMinecraft.McFolderSelected + "launcher_profiles.json");
                    ModMinecraft.McFolderLauncherProfilesJsonCreate(ModMinecraft.McFolderSelected);
                    // 构建需要替换的 Json 对象
                    var ReplaceJsonString = @"
                    {
                      ""authenticationDatabase"": {
                        ""00000111112222233333444445555566"": {
                          ""username"": """ + McLoginLoader.Output.Name.Replace("\"", "-") + @""",
                          ""profiles"": {
                            ""66666555554444433333222221111100"": {
                                ""displayName"": """ + McLoginLoader.Output.Name + @"""
                            }
                          }
                        }
                      },
                      ""clientToken"": """ + McLoginLoader.Output.ClientToken + @""",
                      ""selectedUser"": {
                        ""account"": ""00000111112222233333444445555566"", 
                        ""profile"": ""66666555554444433333222221111100""
                      }
                    }";
                    var ReplaceJson = (JObject)ModBase.GetJson(ReplaceJsonString);
                    // 更新文件
                    var Profiles =
                        (JObject)ModBase.GetJson(
                            ModBase.ReadFile(ModMinecraft.McFolderSelected + "launcher_profiles.json"));
                    Profiles.Merge(ReplaceJson);
                    ModBase.WriteFile(ModMinecraft.McFolderSelected + "launcher_profiles.json", Profiles.ToString(),
                        Encoding: Encoding.GetEncoding("GB18030"));
                    McLaunchLog("已在删除后更新 launcher_profiles.json");
                }
                catch (Exception exx)
                {
                    ModBase.Log(exx, "更新 launcher_profiles.json 失败", ModBase.LogLevel.Feedback);
                }
            }
        } while (false);

        // 更新 options.txt
        var SetupFileAddress = ModMinecraft.McInstanceSelected.PathIndie + "options.txt";

        // 辅助切换游戏语言
        if (Config.Tool.AutoChangeLanguage)
        {
            if (!File.Exists(SetupFileAddress))
            {
                // Yosbr Mod 兼容（#2385）：https://www.curseforge.com/minecraft/mc-mods/yosbr
                var YosbrFileAddress = ModMinecraft.McInstanceSelected.PathIndie + @"config\yosbr\options.txt";
                if (File.Exists(YosbrFileAddress))
                {
                    McLaunchLog("将修改 Yosbr Mod 中的 options.txt");
                    SetupFileAddress = YosbrFileAddress;
                    ModBase.WriteIni(SetupFileAddress, "lang", "none"); // 忽略默认语言
                }
            }

            try
            {
                // 语言
                // 1.0-     ：没有语言选项
                // 1.1 ~ 5  ：zh_CN 时正常，zh_cn 时崩溃（最后两位字母必须大写，否则将会 NPE 崩溃）
                // 1.6 ~ 10 ：zh_CN 时正常，zh_cn 时自动切换为英文
                // 1.11 ~ 12：zh_cn 时正常，zh_CN 时虽然显示了中文但语言设置会错误地显示选择英文
                // 1.13+    ：zh_cn 时正常，zh_CN 时自动切换为英文
                var CurrentLang = ModBase.ReadIni(SetupFileAddress, "lang", "none");
                string RequiredLang; // 需要的语言
                var hasExistingSaves = Directory.Exists(ModMinecraft.McInstanceSelected.PathIndie + "saves");
                var shouldUseDefault = CurrentLang == "none" || !hasExistingSaves;

                // 获取 Minecraft 版本信息
                DateTime? mcReleaseTime = ModMinecraft.McInstanceSelected.ReleaseTime;
                var isUnder1dot1 =
                    (bool)((new DateTime(2000, 1, 1) is var arg3 && mcReleaseTime.HasValue
                            ? mcReleaseTime.Value > arg3
                            : (bool?)null) is var arg5 && arg5.HasValue && !arg5.Value ? false :
                        !((new DateTime(2011, 11, 18) is var arg4 && mcReleaseTime.HasValue
                            ? mcReleaseTime.Value <= arg4
                            : (bool?)null) is { } arg6) ? null :
                        arg6 ? arg5 : false); // 1.11 发布日期

                // 对于 1.0 及以下版本，没有语言选项，返回 "none"
                if (isUnder1dot1)
                {
                    RequiredLang = "none";
                }
                else
                {
                    // 根据配置确定默认语言
                    var defaultLang = "zh_cn";
                    RequiredLang = shouldUseDefault ? defaultLang : CurrentLang.ToLower();

                    // 应用版本特定的语言格式规则
                    if (((new DateTime(2012, 1, 12) is var arg7 && mcReleaseTime.HasValue
                                ? mcReleaseTime.Value >= arg7
                                : (bool?)null) is var arg9 && arg9.HasValue && !arg9.Value ? false :
                            !((new DateTime(2016, 6, 8) is var arg8 && mcReleaseTime.HasValue
                                ? mcReleaseTime.Value <= arg8
                                : (bool?)null) is { } arg10) ? null :
                            arg10 ? arg9 : false) == true)
                        // 1.1~1.10：最后两位字母必须大写（zh_CN）
                        RequiredLang = "zh_CN";
                }

                if ((CurrentLang ?? "") == (RequiredLang ?? ""))
                {
                    McLaunchLog($"需要的语言为 {RequiredLang}，当前语言为 {CurrentLang}，无需修改");
                }
                else
                {
                    ModBase.WriteIni(SetupFileAddress, "lang", "-"); // 触发缓存更改，避免删除后重新下载残留缓存
                    ModBase.WriteIni(SetupFileAddress, "lang", RequiredLang);
                    McLaunchLog($"已将语言从 {CurrentLang} 修改为 {RequiredLang}");
                }

                // 如果是初次设置，一并修改 forceUnicodeFont，确保中文能正常显示
                if (CurrentLang == "none" || !Directory.Exists(ModMinecraft.McInstanceSelected.PathIndie + "saves"))
                {
                    ModBase.WriteIni(SetupFileAddress, "forceUnicodeFont", "true");
                    McLaunchLog("已开启 forceUnicodeFont，确保中文字体正常显示");
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "更新 options.txt 失败", ModBase.LogLevel.Hint);
            }
        }

        // 窗口
        switch (Config.Launch.GameWindowMode)
        {
            case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false): // 全屏
            {
                ModBase.WriteIni(SetupFileAddress, "fullscreen", "true");
                break;
            }
            case var case1 when Operators.ConditionalCompareObjectEqual(case1, 1, false): // 默认
                // 其他
            {
                break;
            }

            default:
            {
                ModBase.WriteIni(SetupFileAddress, "fullscreen", "false");
                break;
            }
        }
    }

    private static void McLaunchCustom(ModLoader.LoaderTask<int, int> Loader)
    {
        // 获取自定义命令
        var CustomCommandGlobal = Conversions.ToString(Config.Launch.PreLaunchCommand);
        if (!string.IsNullOrEmpty(CustomCommandGlobal))
            CustomCommandGlobal = ArgumentReplace(CustomCommandGlobal, true);
        var CustomCommandVersion =
            Conversions.ToString(ModBase.Setup.Get("VersionAdvanceRun", ModMinecraft.McInstanceSelected));
        if (!string.IsNullOrEmpty(CustomCommandVersion))
            CustomCommandVersion = ArgumentReplace(CustomCommandVersion, true);

        // 输出 bat
        try
        {
            var CmdString =
                $"{(McLaunchJavaSelected.Installation.MajorVersion > 8 ? "chcp 65001>nul" + "\r\n" : "")}" +
                "@echo off" + "\r\n" + $"title 启动 - {ModMinecraft.McInstanceSelected.Name}" +
                "\r\n" + "echo 游戏正在启动，请稍候。" + "\r\n" +
                $"cd /D \"{ModBase.ShortenPath(ModMinecraft.McInstanceSelected.PathIndie)}\"" + "\r\n" +
                CustomCommandGlobal + "\r\n" + CustomCommandVersion + "\r\n" +
                $"\"{McLaunchJavaSelected.Installation.JavaExePath}\" {McLaunchArgument}" + "\r\n" +
                "echo 游戏已退出。" + "\r\n" + "pause";
            ModBase.WriteFile(CurrentLaunchOptions.SaveBatch ?? ModBase.ExePath + @"PCL\LatestLaunch.bat",
                ModMinecraft.FilterAccessToken(CmdString, 'F'),
                Encoding: McLaunchJavaSelected.Installation.MajorVersion > 8 ? Encoding.UTF8 : Encoding.Default);
            if (CurrentLaunchOptions.SaveBatch is not null)
            {
                McLaunchLog("导出启动脚本完成，强制结束启动过程");
                AbortHint = "导出启动脚本成功！";
                ModBase.OpenExplorer(CurrentLaunchOptions.SaveBatch);
                Loader.Parent.Abort();
                return; // 导出脚本完成
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "输出启动脚本失败");
            if (CurrentLaunchOptions.SaveBatch is not null)
                throw; // 直接触发启动失败
        }

        // 执行自定义命令
        if (!string.IsNullOrEmpty(CustomCommandGlobal))
        {
            McLaunchLog("正在执行全局自定义命令：" + CustomCommandGlobal);
            var CustomProcess = new Process();
            try
            {
                CustomProcess.StartInfo.FileName = "cmd.exe";
                CustomProcess.StartInfo.Arguments = "/c \"" + CustomCommandGlobal + "\"";
                CustomProcess.StartInfo.WorkingDirectory = ModBase.ShortenPath(ModMinecraft.McFolderSelected);
                CustomProcess.StartInfo.UseShellExecute = false;
                CustomProcess.StartInfo.CreateNoWindow = true;
                CustomProcess.Start();
                if (Conversions.ToBoolean(Config.Launch.PreLaunchCommandWait))
                    while (!CustomProcess.HasExited && !Loader.IsAborted)
                        Thread.Sleep(10);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "执行全局自定义命令失败", ModBase.LogLevel.Hint);
            }
            finally
            {
                if (!CustomProcess.HasExited && Loader.IsAborted)
                {
                    McLaunchLog("由于取消启动，已强制结束自定义命令 CMD 进程"); // #1183
                    CustomProcess.Kill();
                }
            }
        }

        if (!string.IsNullOrEmpty(CustomCommandVersion))
        {
            McLaunchLog("正在执行实例自定义命令：" + CustomCommandVersion);
            var CustomProcess = new Process();
            try
            {
                CustomProcess.StartInfo.FileName = "cmd.exe";
                CustomProcess.StartInfo.Arguments = "/c \"" + CustomCommandVersion + "\"";
                CustomProcess.StartInfo.WorkingDirectory = ModBase.ShortenPath(ModMinecraft.McFolderSelected);
                CustomProcess.StartInfo.UseShellExecute = false;
                CustomProcess.StartInfo.CreateNoWindow = true;
                CustomProcess.Start();
                if (Conversions.ToBoolean(ModBase.Setup.Get("VersionAdvanceRunWait", ModMinecraft.McInstanceSelected)))
                    while (!CustomProcess.HasExited && !Loader.IsAborted)
                        Thread.Sleep(10);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "执行实例自定义命令失败", ModBase.LogLevel.Hint);
            }
            finally
            {
                if (!CustomProcess.HasExited && Loader.IsAborted)
                {
                    McLaunchLog("由于取消启动，已强制结束自定义命令 CMD 进程"); // #1183
                    CustomProcess.Kill();
                }
            }
        }
    }

    private static void McLaunchRun(ModLoader.LoaderTask<int, Process> Loader)
    {
        var noJavaw = Conversions.ToBoolean((bool)Config.Launch.NoJavaw &&
                                            McLaunchJavaSelected.Installation.JavawExePath is not null);

        // 启动信息
        var GameProcess = new Process();
        var StartInfo = new ProcessStartInfo(noJavaw
            ? McLaunchJavaSelected.Installation.JavaExePath
            : McLaunchJavaSelected.Installation.JavawExePath);

        // 设置环境变量
        var Paths = new List<string>(StartInfo.EnvironmentVariables["Path"].Split(";"));
        Paths.Add(ModBase.ShortenPath(McLaunchJavaSelected.Installation.JavaFolder));
        StartInfo.EnvironmentVariables["Path"] = Paths.Distinct().ToList().Join(";");
        StartInfo.EnvironmentVariables["appdata"] = ModBase.ShortenPath(ModMinecraft.McFolderSelected);

        // 设置其他参数
        StartInfo.WorkingDirectory = ModBase.ShortenPath(ModMinecraft.McInstanceSelected.PathIndie);
        StartInfo.UseShellExecute = false;
        StartInfo.RedirectStandardOutput = true;
        StartInfo.RedirectStandardError = true;
        StartInfo.CreateNoWindow = noJavaw;
        StartInfo.Arguments = McLaunchArgument;
        GameProcess.StartInfo = StartInfo;

        // 开始进程
        GameProcess.Start();
        McLaunchLog("已启动游戏进程：" + StartInfo.FileName);
        if (Loader.IsAborted)
        {
            McLaunchLog("由于取消启动，已强制结束游戏进程"); // #1631
            GameProcess.Kill();
            return;
        }

        Loader.Output = GameProcess;
        McLaunchProcess = GameProcess;
        // 进程优先级处理
        try
        {
            GameProcess.PriorityBoostEnabled = true;
            switch (Config.Launch.ProcessPriority)
            {
                case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false): // 高
                {
                    GameProcess.PriorityClass = ProcessPriorityClass.AboveNormal;
                    break;
                }
                case var case1 when Operators.ConditionalCompareObjectEqual(case1, 2, false): // 低
                {
                    GameProcess.PriorityClass = ProcessPriorityClass.BelowNormal; // 中
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "设置进程优先级失败", ModBase.LogLevel.Feedback);
        }
    }

    private static void McLaunchWait(ModLoader.LoaderTask<Process, int> Loader)
    {
        // 输出信息
        McLaunchLog("");
        McLaunchLog("~ 基础参数 ~");
        McLaunchLog("PCL 版本：" + ModBase.VersionBaseName + " (" + ModBase.VersionCode + ")");
        McLaunchLog(
            $"游戏版本：{ModMinecraft.McInstanceSelected.Info.VanillaName}（{ModMinecraft.McInstanceSelected.Info.Vanilla}，Drop {ModMinecraft.McInstanceSelected.Info.Drop}{(ModMinecraft.McInstanceSelected.Info.Reliable ? "" : "，无法完全确定")}）");
        McLaunchLog("资源版本：" + ModMinecraft.McAssetsGetIndexName(ModMinecraft.McInstanceSelected));
        McLaunchLog("实例继承：" + (string.IsNullOrEmpty(ModMinecraft.McInstanceSelected.InheritInstanceName)
            ? "无"
            : ModMinecraft.McInstanceSelected.InheritInstanceName));
        McLaunchLog("分配的内存：" +
                    PageInstanceSetup.GetRam(ModMinecraft.McInstanceSelected,
                        !McLaunchJavaSelected.Installation.Is64Bit) + " GB（" +
                    Math.Round(PageInstanceSetup.GetRam(ModMinecraft.McInstanceSelected,
                        !McLaunchJavaSelected.Installation.Is64Bit) * 1024d) + " MB）");
        McLaunchLog("MC 文件夹：" + ModMinecraft.McFolderSelected);
        McLaunchLog("实例文件夹：" + ModMinecraft.McInstanceSelected.PathInstance);
        McLaunchLog("版本隔离：" + ((ModMinecraft.McInstanceSelected.PathIndie ?? "") ==
                               (ModMinecraft.McInstanceSelected.PathInstance ?? "")));
        McLaunchLog("HMCL 格式：" + ModMinecraft.McInstanceSelected.IsHmclFormatJson);
        McLaunchLog("Java 信息：" + (McLaunchJavaSelected is not null ? McLaunchJavaSelected.ToString : "无可用 Java"));
        // McLaunchLog("环境变量：" & If(McLaunchJavaSelected IsNot Nothing, If(McLaunchJavaSelected.HasEnvironment, "已设置", "未设置"), "未设置"))
        McLaunchLog("Natives 文件夹：" + GetNativesFolder());
        McLaunchLog("");
        McLaunchLog("~ 档案参数 ~");
        McLaunchLog("玩家用户名：" + McLoginLoader.Output.Name);
        McLaunchLog("AccessToken：" + McLoginLoader.Output.AccessToken);
        McLaunchLog("ClientToken：" + McLoginLoader.Output.ClientToken);
        McLaunchLog("UUID：" + McLoginLoader.Output.Uuid);
        McLaunchLog("验证方式：" + McLoginLoader.Output.Type);
        McLaunchLog("");

        // 获取窗口标题
        var WindowTitle = (string?)ModBase.Setup.Get("VersionArgumentTitle", ModMinecraft.McInstanceSelected);
        if (string.IsNullOrEmpty(WindowTitle) &&
            !(bool)ModBase.Setup.Get("VersionArgumentTitleEmpty", ModMinecraft.McInstanceSelected))
            WindowTitle = Conversions.ToString(Config.Launch.Title);
        WindowTitle = ArgumentReplace(WindowTitle, false);

        // JStack 路径
        var JStackPath = McLaunchJavaSelected.Installation.JavaFolder + @"\jstack.exe";

        // 初始化等待
        var Watcher = new ModWatcher.Watcher(Loader, ModMinecraft.McInstanceSelected, WindowTitle,
            File.Exists(JStackPath) ? JStackPath : "", CurrentLaunchOptions.IsTest);
        McLaunchWatcher = Watcher;

        // 显示实时日志
        if (CurrentLaunchOptions.IsTest)
        {
            if (ModMain.FrmLogLeft is null)
                ModBase.RunInUiWait(() => ModMain.FrmLogLeft = new PageLogLeft());
            if (ModMain.FrmLogRight is null)
                ModBase.RunInUiWait(() =>
                {
                    ModAnimation.AniControlEnabled += 1;
                    ModMain.FrmLogRight = new PageLogRight();
                    ModAnimation.AniControlEnabled -= 1;
                });
            ModMain.FrmLogLeft.Add(Watcher);
            McLaunchLog("已显示游戏实时日志");
        }

        // 等待
        while (Watcher.State == ModWatcher.Watcher.MinecraftState.Loading)
            Thread.Sleep(100);
        if (Watcher.State == ModWatcher.Watcher.MinecraftState.Crashed) throw new Exception("$$");
    }

    private static void McLaunchEnd()
    {
        McLaunchLog("开始启动结束处理");

        // 暂停或开始音乐播放
        if (Conversions.ToBoolean(Config.Preference.Music.StopInGame))
            ModBase.RunInUi(() =>
            {
                if (ModMusic.MusicPause()) ModBase.Log("[Music] 已根据设置，在启动后暂停音乐播放");
            });
        else if (Conversions.ToBoolean(Config.Preference.Music.StartInGame))
            ModBase.RunInUi(() =>
            {
                if (ModMusic.MusicResume()) ModBase.Log("[Music] 已根据设置，在启动后开始音乐播放");
            });
        // 暂停视频背景播放
        ModVideoBack.IsGaming = true;
        ModVideoBack.VideoPause();
        // 启动器可见性
        McLaunchLog(
            Conversions.ToString(Operators.ConcatenateObject("启动器可见性：", Config.Launch.LauncherVisibility)));
        switch (Config.Launch.LauncherVisibility)
        {
            case var @case when Operators.ConditionalCompareObjectEqual(@case, 0, false):
            {
                // 直接关闭
                McLaunchLog("已根据设置，在启动后关闭启动器");
                ModBase.RunInUi(() => ModMain.FrmMain.EndProgram(false));
                break;
            }
            case var case1 when Operators.ConditionalCompareObjectEqual(case1, 2, false):
            case var case2 when Operators.ConditionalCompareObjectEqual(case2, 3, false):
            {
                // 隐藏
                McLaunchLog("已根据设置，在启动后隐藏启动器");
                ModBase.RunInUi(() => ModMain.FrmMain.Hidden = true);
                break;
            }
            case var case3 when Operators.ConditionalCompareObjectEqual(case3, 4, false):
            {
                // 最小化
                McLaunchLog("已根据设置，在启动后最小化启动器");
                ModBase.RunInUi(() => ModMain.FrmMain.WindowState = WindowState.Minimized);
                break;
            }
            case var case4 when Operators.ConditionalCompareObjectEqual(case4, 5, false):
            {
                break;
            }
            // 啥都不干
        }

        // 启动计数
        States.System.LaunchCount += 1;

        ModBase.Setup.Set("VersionLaunchCount",
            Operators.AddObject(ModBase.Setup.Get("VersionLaunchCount", ModMinecraft.McInstanceSelected), 1),
            instance: ModMinecraft.McInstanceSelected);
    }

    /// <summary>
    ///     对替换标记进行处理。会对替换内容使用 EscapeHandler 进行转义。
    /// </summary>
    private static string ArgumentReplace(string text, bool replaceTime, Func<string, string> escapeHandler = null)
    {
        // 预处理
        if (text is null)
            return null;

        string replacer(string s)
        {
            if (s is null)
                return "";
            if (escapeHandler is null)
                return s;
            if (s.Contains(@":\"))
                s = ModBase.ShortenPath(s);
            return escapeHandler(s);
        }

        ;
        // 基础
        text = text.Replace("{pcl_version}", replacer(ModBase.VersionBaseName));
        text = text.Replace("{pcl_version_code}", replacer(ModBase.VersionCode.ToString()));
        text = text.Replace("{pcl_version_branch}", replacer(ModBase.VersionBranchName));
        text = text.Replace("{identify}", replacer(Identify.LauncherId));
        text = text.Replace("{path}", replacer(Basics.CurrentDirectory));
        text = text.Replace("{path_with_name}", replacer(Basics.ExecutablePath));
        text = text.Replace("{path_temp}", replacer(ModBase.PathTemp));
        // 时间
        if (replaceTime) // 在窗口标题中，时间会被后续动态替换，所以此时不应该替换
        {
            text = text.Replace("{date}", replacer(DateTime.Now.ToString("yyyy'/'M'/'d")));
            text = text.Replace("{time}", replacer(DateTime.Now.ToString("HH':'mm':'ss")));
        }

        // Minecraft
        text = text.Replace("{java}", replacer(McLaunchJavaSelected?.Installation.JavaFolder));
        text = text.Replace("{minecraft}", replacer(ModMinecraft.McFolderSelected));
        if (ModMinecraft.McInstanceSelected?.IsLoaded == true)
        {
            text = text.Replace("{version_path}", replacer(ModMinecraft.McInstanceSelected.PathInstance));
            text = text.Replace("{verpath}", replacer(ModMinecraft.McInstanceSelected.PathInstance));
            text = text.Replace("{version_indie}", replacer(ModMinecraft.McInstanceSelected.PathIndie));
            text = text.Replace("{verindie}", replacer(ModMinecraft.McInstanceSelected.PathIndie));
            text = text.Replace("{name}", replacer(ModMinecraft.McInstanceSelected.Name));
            if (new[] { "unknown", "old", "pending" }.Contains(
                    ModMinecraft.McInstanceSelected.Info.VanillaName.ToLower()))
                text = text.Replace("{version}", replacer(ModMinecraft.McInstanceSelected.Name));
            else
                text = text.Replace("{version}", replacer(ModMinecraft.McInstanceSelected.Info.VanillaName));
        }
        else
        {
            text = text.Replace("{version_path}", replacer(null));
            text = text.Replace("{verpath}", replacer(null));
            text = text.Replace("{version_indie}", replacer(null));
            text = text.Replace("{verindie}", replacer(null));
            text = text.Replace("{name}", replacer(null));
            text = text.Replace("{version}", replacer(null));
        }

        // 登录信息
        if (McLoginLoader.State == ModBase.LoadState.Finished)
        {
            text = text.Replace("{user}", replacer(McLoginLoader.Output.Name));
            text = text.Replace("{uuid}", replacer(McLoginLoader.Output.Uuid?.ToLower()));
            switch (McLoginLoader.Input.Type)
            {
                case McLoginType.Legacy:
                {
                    text = text.Replace("{login}", replacer("离线"));
                    break;
                }
                case McLoginType.Ms:
                {
                    text = text.Replace("{login}", replacer("正版"));
                    break;
                }
                case McLoginType.Auth:
                {
                    text = text.Replace("{login}", replacer("Authlib-Injector"));
                    break;
                }
            }
        }
        else
        {
            text = text.Replace("{user}", replacer(null));
            text = text.Replace("{uuid}", replacer(null));
            text = text.Replace("{login}", replacer(null));
        }

        return text;
    }

    #endregion
}
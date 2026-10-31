using System.Diagnostics;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.UI.Dialogs;
using PCL.Core.Utils;

namespace PCL;

public static partial class GameLaunch
{
    public const string MesaLoaderWindowsVersion = "26.0.4";

    private static void McLaunchPrecheck()
    {
        if (Config.Debug.AddRandomDelay)
            Thread.Sleep(RandomUtils.NextInt(100, 2000));

        _ValidateLaunchPath();
        _ValidateLaunchInstance();

#if BETA
        _ShowDonateHint();
#endif

#if DEBUG || DEBUGCI
        return;
#endif
        _ShowPurchaseHint();
    }

    private static void _ValidateLaunchPath()
    {
        var instance = InstanceList.McMcInstanceSelected;
        if (instance is null)
            return;

        if (instance.PathIndie.Contains('!') || instance.PathIndie.Contains(';'))
            throw new Exception(
                Lang.Text("Minecraft.Launch.Precheck.InvalidPathChars", instance.PathIndie));
        if (instance.PathInstance.Contains('!') || instance.PathInstance.Contains(';'))
            throw new Exception(
                Lang.Text("Minecraft.Launch.Precheck.InvalidPathChars", instance.PathInstance));

        if (!Common.IsUtf8CodePage()
            || States.Hint.NonAsciiGamePath
            || instance.PathInstance.IsAscii())
            return;

        var userChoice = DialogService.Instance.Show(new TextDialogRequest
        {
            Title = Lang.Text("Minecraft.Launch.Precheck.NonAsciiPath.Title"),
            Content = Lang.Text("Minecraft.Launch.Precheck.NonAsciiPath.Message", instance.Name),
            Buttons = DialogButtons.From(
                DialogButton.Create(Lang.Text("Minecraft.Launch.Precheck.NonAsciiPath.Continue")),
                DialogButton.Create(Lang.Text("Minecraft.Launch.Precheck.NonAsciiPath.Back")),
                DialogButton.Create(Lang.Text("Common.Hint.DoNotShowAgain"))),
            ForceWait = true
        }).Result?.Button;
        States.Hint.NonAsciiGamePath = userChoice switch
        {
            2 => throw new Exception("$$"),
            3 => true,
            _ => States.Hint.NonAsciiGamePath
        };
    }

    private static void _ValidateLaunchInstance()
    {
        var instance = InstanceList.McMcInstanceSelected;
        if (instance is null)
            throw new Exception(Lang.Text("Minecraft.Launch.Precheck.NoInstance"));

        instance.Load();
        if (instance.state == McInstanceState.Error)
            throw new Exception(
                Lang.Text("Minecraft.Launch.Precheck.InstanceError", instance.Desc));

        _ValidateLaunchProfile();
    }

    private static void _ValidateLaunchProfile()
    {
        var checkResult = "";
        Common.RunInUiWait(() => checkResult = ProfileUi.IsProfileValid());
        var selectedProfile = ProfileService.Current;

        if (selectedProfile is null)
            checkResult = Lang.Text("Minecraft.Launch.Precheck.NoProfile");
        else if (InstanceList.McMcInstanceSelected!.Info.HasLabyMod)
            checkResult = _ValidateLabyModProfile(selectedProfile, checkResult);
        else
            checkResult = _ValidateLoginRequirementProfile(selectedProfile, checkResult);

        if (!string.IsNullOrEmpty(checkResult))
            throw new ArgumentException(checkResult);
    }

    private static string _ValidateLabyModProfile(McProfile profile, string checkResult)
    {
        return profile.ProfileType != ProfileType.Microsoft
            ? Lang.Text("Minecraft.Launch.Precheck.RequireMicrosoft")
            : checkResult;
    }

    private static string _ValidateLoginRequirementProfile(
        McProfile selectedProfile,
        string checkResult)
    {
        var instance = InstanceList.McMcInstanceSelected!;
        var loginRequirementSolution = Config.InstanceAuth.LoginRequirementSolution[
            instance.PathInstance
        ];
        return loginRequirementSolution switch
        {
            1 => selectedProfile.ProfileType != ProfileType.Microsoft
                ? Lang.Text("Minecraft.Launch.Precheck.RequireMicrosoft")
                : checkResult,

            2 => selectedProfile.ProfileType is not (ProfileType.Authlib
                or ProfileType.YggdrasilConnect)
                ? Lang.Text("Minecraft.Launch.Precheck.RequireThirdParty")
                : selectedProfile.Server?.BeforeLast("/authserver")
                != Config.InstanceAuth.AuthServerAddress[instance.PathInstance]
                    ? Lang.Text("Minecraft.Launch.Precheck.AuthServerMismatch")
                    : checkResult,

            3 => selectedProfile.ProfileType switch
            {
                ProfileType.Offline => Lang.Text(
                    "Minecraft.Launch.Precheck.RequireMicrosoftOrThirdParty"),

                ProfileType.Authlib or ProfileType.YggdrasilConnect when
                    (selectedProfile.Server?.BeforeLast("/authserver")
                        != Config.InstanceAuth.AuthServerAddress[instance.PathInstance]) =>
                    Lang.Text("Minecraft.Launch.Precheck.AuthServerMismatch"),

                _ => checkResult
            },
            _ => checkResult
        };
    }

#if BETA
    private static readonly HashSet<int> DonateHintLaunchCounts =
    [
        10,
        20,
        40,
        60,
        80,
        100,
        120,
        150,
        200,
        250,
        300,
        350,
        400,
        500,
        600,
        700,
        800,
        900,
        1000,
        1200,
        1400,
        1600,
        1800,
        2000,
    ];

    private static void _ShowDonateHint()
    {
        if (currentLaunchOptions?.SaveBatch is not null)
            return;

        Common.RunInNewThread(
            () =>
            {
                if (!DonateHintLaunchCounts.Contains(States.System.LaunchCount))
                    return;

                if (DialogService.Instance.Show(new TextDialogRequest
                    {
                        Title = Lang.Text("Minecraft.Launch.Donate.Title", States.System.LaunchCount),
                        Content = Lang.Text("Minecraft.Launch.Donate.Message", States.System.LaunchCount),
                        Buttons = DialogButtons.From(
                            DialogButton.Create(Lang.Text("Minecraft.Launch.Donate.Support")),
                            DialogButton.Create(Lang.Text("Minecraft.Launch.Donate.Decline"))),
                        ForceWait = true
                    }).Result?.Button == 1)
                {
                    Common.OpenWebsite("https://afdian.com/a/LTCat");
                }
            },
            "Donate");
    }
#endif

    private static void _ShowPurchaseHint()
    {
        if (ProfileService.HasMicrosoftProfile)
            return;

        if (Lang.IsFeaturesUnrestricted)
        {
            if (DialogService.Instance.Show(new TextDialogRequest
                {
                    Title = Lang.Text("Minecraft.Launch.PurchaseHint.Title"),
                    Content = Lang.Text("Minecraft.Launch.PurchaseHint.Message"),
                    Buttons = DialogButtons.From(
                        DialogButton.Create(Lang.Text("Minecraft.Launch.PurchaseHint.Purchase")),
                        DialogButton.Create(Lang.Text("Minecraft.Launch.PurchaseHint.Later"))),
                    ForceWait = true
                }).Result
                ?.Button == 1)
                Common.OpenWebsite(
                    "https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj");
            return;
        }

        switch (DialogService.Instance.Show(new TextDialogRequest
                {
                    Title = Lang.Text("Minecraft.Launch.AccountVerification.Title"),
                    Content = Lang.Text("Minecraft.Launch.AccountVerification.Message"),
                    Buttons = DialogButtons.From(
                        DialogButton.Create(Lang.Text("Minecraft.Launch.AccountVerification.Purchase"),
                            () =>
                                Common.OpenWebsite(
                                    "https://www.xbox.com/zh-cn/games/store/minecraft-java-bedrock-edition-for-pc/9nxp44l49shj")),
                        DialogButton.Create(Lang.Text("Minecraft.Launch.AccountVerification.Demo")),
                        DialogButton.Create(Lang.Text("Minecraft.Launch.AccountVerification.Back"))),
                    ForceWait = true
                }).Result?.Button)
        {
            case 2:
                HintService.Show(Hints.Error(Lang.Text("Minecraft.Launch.DemoMode")));
                currentLaunchOptions!.ExtraArgs.Add("--demo");
                break;

            case 3:
                throw new Exception("$$");
        }
    }

    public static bool isLaunching;
    public static McLaunchOptions? currentLaunchOptions;

    public class McLaunchOptions
    {
        /// <summary>
        ///     额外的启动参数。
        /// </summary>
        public readonly List<string> ExtraArgs = [];

        /// <summary>
        ///     是否为 “测试游戏” 按钮启动的游戏。
        ///     如果是，则显示游戏实时日志。
        /// </summary>
        public bool IsTest = false;

        /// <summary>
        ///     将启动脚本保存到该地址，然后取消启动。这同时会改变启动时的提示等。
        ///     默认值：Nothing。不保存。
        /// </summary>
        public string? SaveBatch = null;

        /// <summary>
        ///     强制指定在启动后进入的服务器 IP。
        ///     默认值：Nothing。使用实例设置的值。
        /// </summary>
        public string? ServerIp = null;

        /// <summary>
        ///     指定在启动之后进入的存档名称。
        ///     默认值：Nothing。使用实例设置的值。
        /// </summary>
        public string? WorldName = null;

        /// <summary>
        ///     强行指定启动的 MC 实例。
        ///     默认值：Nothing。使用 McInstanceCurrent。
        /// </summary>
        public McInstance? instance = null;
    }

    /// <summary>
    ///     尝试启动 Minecraft。必须在 UI 线程调用。
    ///     返回是否实际开始了启动（如果没有，则一定弹出了错误提示）。
    /// </summary>
    public static bool McLaunchStart(McLaunchOptions? options = null)
    {
        isLaunching = true;
        currentLaunchOptions = options ?? new McLaunchOptions();
        if (!Common.RunInUi())
            throw new Exception("McLaunchStart 必须在 UI 线程调用！");
        if (mcLaunchLoader.State == Common.LoadState.Loading)
        {
            HintService.Show(Hints.Error(Lang.Text("Minecraft.Launch.Error.AlreadyLaunching")));
            isLaunching = false;
            return false;
        }

        if (!_TrySwitchLaunchInstance())
            return false;

        Launcher.frmMain!.AprilGiveup();
        Launcher.frmMain.pageStack =
        [
            .. Launcher.frmMain.pageStack.Where(p => p.page != FormMain.PageType.InstanceSelect)
        ];
        mcLaunchLoader.Start(options, true);
        return true;
    }

    private static bool _TrySwitchLaunchInstance()
    {
        if (currentLaunchOptions!.instance is null
            || InstanceList.McMcInstanceSelected == currentLaunchOptions.instance)
            return true;

        McLaunchLog($"在启动前切换到实例 {currentLaunchOptions.instance.Name}");
        currentLaunchOptions.instance.Load();
        if (currentLaunchOptions.instance.state == McInstanceState.Error)
        {
            HintService.Show(Hints.Error(Lang.Text(
                    "Minecraft.Launch.Error.CannotLaunch",
                    currentLaunchOptions.instance.Desc
                )));
            isLaunching = false;
            return false;
        }

        InstanceList.McMcInstanceSelected = currentLaunchOptions.instance;
        States.Game.SelectedInstance = InstanceList.McMcInstanceSelected.Name;
        Launcher.frmLaunchLeft!.RefreshButtonsUI();
        Launcher.frmLaunchLeft.RefreshPage(false);
        return true;
    }

    /// <summary>
    ///     记录启动日志。
    /// </summary>
    public static void McLaunchLog(string text)
    {
        text = McLogFilter.FilterUserName(McLogFilter.FilterAccessToken(text, '*'), '*');
        Common.RunInUi(() =>
            Launcher.frmLaunchRight!.LabLog.Text += $"\r\n[{TimeUtils.GetTimeNow()}] {text}");
        Common.Log($"[Launch] {text}");
    }

    public static Loading.LoaderTask<McLaunchOptions, object> mcLaunchLoader = new(
        "Loader Launch",
        McLaunchStart)
    {
        OnStateChanged = a => McLaunchState((dynamic)a)
    };

    public static Loading.LoaderCombo<object> mcLaunchLoaderReal = null!;
    public static Process? mcLaunchProcess;
    public static GameWatcher.Watcher? mcLaunchWatcher;

    private static void McLaunchState(Loading.LoaderTask<McLaunchOptions, object> loader)
    {
        switch (mcLaunchLoader.State)
        {
            case Common.LoadState.Finished:
            case Common.LoadState.Failed:
            case Common.LoadState.Waiting:
            case Common.LoadState.Aborted:
                Launcher.frmLaunchLeft!.PageChangeToLogin();
                break;

            case Common.LoadState.Loading:
                Launcher.frmLaunchRight!.LabLog.Text = "";
                break;
        }
    }

    /// <summary>
    ///     指定启动中断时的提示文本。若不为 Nothing 则会显示为绿色。
    /// </summary>
    private static string? _abortHint;

    private static void McLaunchStart(Loading.LoaderTask<McLaunchOptions, object> loader)
    {
        Common.RunInUiWait(Launcher.frmLaunchLeft!.PageChangeToLaunching);
        _RunLaunchPrecheck();

        try
        {
            var skipClientDownloads = RestrictedFeaturePolicy.IsRestricted;
            if (skipClientDownloads
                && !GameDownload.McClientFilesAvailableLocally(InstanceList.McMcInstanceSelected!))
            {
                RestrictedFeaturePolicy.Check("minecraft-launch-missing-files");
                throw new Common.CancelledException();
            }

            var launchLoader = _CreateLaunchLoader(skipClientDownloads);
            if (mcLoginLoader.State == Common.LoadState.Finished)
                mcLoginLoader.State = Common.LoadState.Waiting;
            mcLaunchLoaderReal = launchLoader;
            _abortHint = null;
            launchLoader.Start();
            Loading.LoaderTaskbarAdd(launchLoader);
            _WaitForLaunchLoader(launchLoader);
            _HandleLaunchResult(launchLoader);

            isLaunching = false;
        }
        catch (Exception ex)
        {
            _LogLaunchFailure(ex);
            throw;
        }
    }

    private static void _RunLaunchPrecheck()
    {
        try
        {
            McLaunchPrecheck();
            McLaunchLog("预检测已通过");
        }
        catch (Exception ex)
        {
            if (!ex.Message.StartsWithF("$$"))
                HintService.Show(Hints.Error(Lang.Text("Minecraft.Launch.Precheck.Failed.WithDetail", ex.Message)));
            throw;
        }
    }

    private static Loading.LoaderCombo<object> _CreateLaunchLoader(bool skipClientDownloads)
    {
        var loaders = new List<Loading.LoaderBase>();
        loaders.AddRange(_CreateGameFileLoaders(skipClientDownloads));
        loaders.AddRange(_CreateProcessLoaders());

        return new Loading.LoaderCombo<object>(Lang.Text("Minecraft.Launch.Stage.Root"), loaders)
        {
            show = false
        };
    }

    private static List<Loading.LoaderBase> _CreateGameFileLoaders(bool skipClientDownloads)
    {
        return
        [
            new Loading.LoaderTask<int, int>(
                Lang.Text("Minecraft.Launch.Stage.GetJava"),
                McLaunchJava)
            {
                ProgressWeight = 4d,
                block = false
            },
            mcLoginLoader,
            new Loading.LoaderCombo<string>(
                Lang.Text("Minecraft.Launch.Stage.CompleteFiles"),
                skipClientDownloads
                    ? []
                    : GameDownload.DlClientFix(
                        InstanceList.McMcInstanceSelected!,
                        false,
                        GameDownload.AssetsIndexExistsBehaviour.DownloadInBackground))
            {
                ProgressWeight = 15d,
                show = false
            },
            new Loading.LoaderTask<string, List<Libraries.McLibToken>>(
                Lang.Text("Minecraft.Launch.Stage.GetArguments"),
                McLaunchArgumentMain)
            {
                ProgressWeight = 2d
            }
        ];
    }

    private static List<Loading.LoaderBase> _CreateProcessLoaders()
    {
        return
        [
            new Loading.LoaderTask<List<Libraries.McLibToken>, int>(
                Lang.Text("Minecraft.Launch.Stage.ExtractNatives"),
                McLaunchNatives)
            {
                ProgressWeight = 2d
            },
            new Loading.LoaderTask<int, int>(
                Lang.Text("Minecraft.Launch.Stage.PreLaunch"),
                _ => McLaunchPrerun())
            {
                ProgressWeight = 1d
            },
            new Loading.LoaderTask<int, int>(
                Lang.Text("Minecraft.Launch.Stage.CustomCommand"),
                McLaunchCustom)
            {
                ProgressWeight = 1d
            },
            new Loading.LoaderTask<int, Process>(
                Lang.Text("Minecraft.Launch.Stage.StartProcess"),
                McLaunchRun)
            {
                ProgressWeight = 2d
            },
            new Loading.LoaderTask<Process, int>(
                Lang.Text("Minecraft.Launch.Stage.WaitWindow"),
                McLaunchWait)
            {
                ProgressWeight = 1d
            },
            new Loading.LoaderTask<int, int>(
                Lang.Text("Minecraft.Launch.Stage.End"),
                _ => McLaunchEnd())
            {
                ProgressWeight = 1d
            }
        ];
    }

    private static void _WaitForLaunchLoader(Loading.LoaderCombo<object> launchLoader)
    {
        while (launchLoader.State == Common.LoadState.Loading)
        {
            Launcher.frmLaunchLeft!.Dispatcher.Invoke(Launcher.frmLaunchLeft.LaunchingRefresh);
            Thread.Sleep(100);
        }

        Launcher.frmLaunchLeft!.Dispatcher.Invoke(Launcher.frmLaunchLeft.LaunchingRefresh);
    }

    private static void _HandleLaunchResult(Loading.LoaderCombo<object> launchLoader)
    {
        switch (launchLoader.State)
        {
            case Common.LoadState.Finished:
                HintService.Show(Hints.Success(Lang.Text("Minecraft.Launch.Success", InstanceList.McMcInstanceSelected!.Name)));
                break;

            case Common.LoadState.Aborted:
                if (_abortHint is null)
                    HintService.Show(Hints.Info(currentLaunchOptions?.SaveBatch is null
                            ? Lang.Text("Minecraft.Launch.Cancelled")
                            : Lang.Text("Minecraft.Launch.ExportScript.Cancelled")));
                else
                    HintService.Show(Hints.Success(_abortHint));
                break;

            case Common.LoadState.Failed:
                throw launchLoader.Error;

            case Common.LoadState.Waiting:
            case Common.LoadState.Loading:
            default:
                throw new Exception(
                    Lang.Text(
                        "Minecraft.Launch.Error.InvalidState",
                        Common.GetStringFromEnum(launchLoader.State)));
        }
    }

    private static void _LogLaunchFailure(Exception ex)
    {
        var currentEx = ex;
        while (currentEx is not null)
        {
            if (currentEx.Message.StartsWithF("$"))
            {
                if (currentEx.Message != "$$")
                    DialogService.Instance.Show(new TextDialogRequest
                    {
                        Title = currentLaunchOptions?.SaveBatch is null
                            ? Lang.Text("Launch.Error.Title")
                            : Lang.Text("Launch.Error.ExportScriptTitle"),
                        Content = Lang.Text(
                            "Minecraft.Launch.Error.SpecialMessage.WithDetail",
                            currentEx.Message.TrimStart('$')
                        ),
                        Buttons = DialogButtons.Ok,
                        ForceWait = true
                    });
                return;
            }

            if (currentEx.InnerException is null)
                break;

            currentEx = currentEx.InnerException;
        }

        McLaunchLog($"错误：{ex}");
        Common.Log(
            ex,
            currentLaunchOptions?.SaveBatch is null
                ? "Minecraft launch failed"
                : "Export script failed",
            Common.LogLevel.Msgbox,
            currentLaunchOptions?.SaveBatch is null
                ? Lang.Text("Launch.Error.Title")
                : Lang.Text("Launch.Error.ExportScriptTitle"),
            currentLaunchOptions?.SaveBatch is null
                ? Lang.Text("Minecraft.Launch.Error.LaunchFailed")
                : Lang.Text("Minecraft.Launch.Error.ExportScriptFailed"));
    }
}
using System.Windows;
using PCL.Core.App.Localization;

namespace PCL;

public static partial class Launcher
{
    private const int MainTimerSleepMs = 49;
    private const int AprilTimerSleepMs = 1;
    private const int LongTimerInterval = 150;
    public static FormMain? frmMain;
    public static SplashScreen? frmStart;
    public static PageLaunchLeft? frmLaunchLeft;
    public static PageLaunchRight? frmLaunchRight;
    public static PageLogLeft? frmLogLeft;
    public static PageLogRight? frmLogRight;
    public static PageSelectLeft? frmSelectLeft;
    public static PageSelectRight? frmSelectRight;
    public static PageSpeedLeft? frmSpeedLeft;
    public static PageSpeedRight? frmSpeedRight;
    public static PageToolsLeft? frmToolsLeft;
    public static PageToolsGameLink? frmToolsGameLink;
    public static PageToolsTest? frmToolsTest;
    public static PageDownloadLeft? frmDownloadLeft;
    public static PageDownloadInstall? frmDownloadInstall;
    public static PageDownloadClient? frmDownloadClient;
    public static PageDownloadOptiFine? frmDownloadOptiFine;
    public static PageDownloadLiteLoader? frmDownloadLiteLoader;
    public static PageDownloadForge? frmDownloadForge;
    public static PageDownloadNeoForge? frmDownloadNeoForge;
    public static PageDownloadCleanroom? frmDownloadCleanroom;
    public static PageDownloadFabric? frmDownloadFabric;
    public static PageDownloadLabyMod? frmDownloadLabyMod;
    public static PageDownloadLegacyFabric? frmDownloadLegacyFabric;
    public static PageDownloadMod? frmDownloadMod;
    public static PageDownloadPack? frmDownloadPack;
    public static PageDownloadDataPack? frmDownloadDataPack;
    public static PageDownloadShader? frmDownloadShader;
    public static PageDownloadResourcePack? frmDownloadResourcePack;
    public static PageDownloadWorld? frmDownloadWorld;
    public static PageDownloadCompFavorites? frmDownloadCompFavorites;
    public static PageDownloadCompDetail? frmDownloadCompDetail;
    public static PageSetupLeft? frmSetupLeft;
    public static PageSetupLaunch? frmSetupLaunch;
    public static PageSetupUI? frmSetupUi;
    public static PageSetupGameManage? frmSetupGameManage;
    public static PageSetupUpdate? frmSetupUpdate;
    public static PageSetupJava? frmSetupJava;
    public static PageSetupAbout? frmSetupAbout;
    public static PageSetupLog? frmSetupLog;
    public static PageSetupFeedback? frmSetupFeedback;
    public static PageSetupGameLink? frmSetupGameLink;
    public static PageSetupLauncherLanguage? frmSetupLauncherLanguage;
    public static PageSetupLauncherMisc? frmSetupLauncherMisc;
    public static PageLoginAuth? frmLoginAuth;
    public static PageLoginMs? frmLoginMs;
    public static PageLoginProfile? frmLoginProfile;
    public static PageLoginProfileSkin? frmLoginProfileSkin;
    public static PageLoginOffline? frmLoginOffline;
    public static PageInstanceLeft? frmInstanceLeft;
    public static PageInstanceOverall? frmInstanceOverall;
    public static PageInstanceCompResource? frmInstanceMod;
    public static PageInstanceModDisabled? frmInstanceModDisabled;
    public static PageInstanceCompJarInJar? frmInstanceModJarInJar;
    public static PageInstanceScreenshot? frmInstanceScreenshot;
    public static PageInstanceSaves? frmInstanceSaves;
    public static PageInstanceCompResource? frmInstanceShader;
    public static PageInstanceCompResource? frmInstanceSchematic;
    public static PageInstanceCompResource? frmInstanceResourcePack;
    public static PageInstanceSetup? frmInstanceSetup;
    public static PageInstanceInstall? frmInstanceInstall;
    public static PageInstanceExport? frmInstanceExport;
    public static PageInstanceServer? frmInstanceServer;
    public static PageInstanceSavesLeft? frmInstanceSavesLeft;
    public static PageInstanceSavesInfo? frmInstanceSavesInfo;
    public static PageInstanceSavesDatapack? frmInstanceSavesDatapack;
    public static PageHomepageNewsView? frmHomepageNews;

    public static MySlider? dragControl;

    private static int _timer150Count;

    /// <summary>
    ///     主时钟回调，每约 50ms 执行一次。
    /// </summary>
    private static void TimerMain()
    {
        try
        {
            HintService.Tick();
            frmMain!.DragTick();
            Loading.LoaderTaskbarProgressRefresh();
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "短程主时钟执行异常",
                Common.LogLevel.Critical,
                userSummary: Lang.Text("Main.Error.OperationFailed"));
        }

        if (++_timer150Count < LongTimerInterval)
            return;

        _timer150Count = 0;
        TimerMainLong();
    }

    /// <summary>
    ///     长程主时钟回调，每约 7.5 秒执行一次。
    /// </summary>
    private static void TimerMainLong()
    {
        try
        {
            if (frmMain!.BtnExtraApril_ShowCheck() && _aprilDistance != 0)
                frmMain.BtnExtraApril.Ribble();

            Common.RunInUi(() =>
            {
                var main = frmMain!;
                if (main.Hidden)
                    return;

                if (main.Top < -9000d)
                    main.Top = 100d;

                if (main.Left < -9000d)
                    main.Left = 100d;
            });
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "长程主时钟执行异常",
                Common.LogLevel.Critical,
                userSummary: Lang.Text("Main.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     启动主时钟，以及愚人节专用时钟。
    /// </summary>
    public static void TimerMainStart()
    {
        Common.RunInNewThread(RunMainTimerLoop, "Timer Main");

        if (isAprilEnabled)
            Common.RunInNewThread(RunAprilTimerLoop, "Timer Main Fool");
    }

    /// <summary>
    ///     主时钟线程。
    /// </summary>
    private static void RunMainTimerLoop()
    {
        try
        {
            while (true)
            {
                Common.RunInUiWait(TimerMain);
                Thread.Sleep(MainTimerSleepMs);
            }
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "程序主时钟出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Main.Error.OperationFailed"));
        }
    }

    /// <summary>
    ///     愚人节专用时钟线程。
    /// </summary>
    private static void RunAprilTimerLoop()
    {
        try
        {
            var lastTick = Environment.TickCount;

            while (true)
            {
                var currentTick = Environment.TickCount;

                if (currentTick != lastTick)
                {
                    lastTick = currentTick;
                    Common.RunInUiWait(TimerFool);
                }

                Thread.Sleep(AprilTimerSleepMs);
            }
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "愚人节主时钟出错",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Main.Error.OperationFailed"));
        }
    }
}

using System.Collections;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FluentValidation;
using Microsoft.VisualBasic;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using PCL.Core.App;
using PCL.Core.UI;
using PCL.Core.Utils;


namespace PCL;

public static partial class ModMain
{
    public static FormMain? FrmMain;
    public static SplashScreen? FrmStart;
    public static PageLaunchLeft? FrmLaunchLeft;
    public static PageLaunchRight? FrmLaunchRight;
    public static PageLogLeft? FrmLogLeft;
    public static PageLogRight? FrmLogRight;
    public static PageSelectLeft? FrmSelectLeft;
    public static PageSelectRight? FrmSelectRight;
    public static PageSpeedLeft? FrmSpeedLeft;
    public static PageSpeedRight? FrmSpeedRight;
    public static PageToolsLeft? FrmToolsLeft;
    public static PageToolsGameLink? FrmToolsGameLink;
    public static PageToolsHelp? FrmToolsHelp;
    public static PageToolsTest? FrmToolsTest;
    public static PageDownloadLeft? FrmDownloadLeft;
    public static PageDownloadInstall? FrmDownloadInstall;
    public static PageDownloadClient? FrmDownloadClient;
    public static PageDownloadOptiFine? FrmDownloadOptiFine;
    public static PageDownloadLiteLoader? FrmDownloadLiteLoader;
    public static PageDownloadForge? FrmDownloadForge;
    public static PageDownloadNeoForge? FrmDownloadNeoForge;
    public static PageDownloadCleanroom? FrmDownloadCleanroom;
    public static PageDownloadFabric? FrmDownloadFabric;
    public static PageDownloadQuilt? FrmDownloadQuilt;
    public static PageDownloadLabyMod? FrmDownloadLabyMod;
    public static PageDownloadLegacyFabric? FrmDownloadLegacyFabric;
    public static PageDownloadMod? FrmDownloadMod;
    public static PageDownloadPack? FrmDownloadPack;
    public static PageDownloadDataPack? FrmDownloadDataPack;
    public static PageDownloadShader? FrmDownloadShader;
    public static PageDownloadResourcePack? FrmDownloadResourcePack;
    public static PageDownloadWorld? FrmDownloadWorld;
    public static PageDownloadCompFavorites? FrmDownloadCompFavorites;
    public static PageSetupLeft? FrmSetupLeft;
    public static PageSetupLaunch? FrmSetupLaunch;
    public static PageSetupUI? FrmSetupUI;
    public static PageSetupGameManage? FrmSetupGameManage;
    public static PageSetupUpdate? FrmSetupUpdate;
    public static PageSetupJava? FrmSetupJava;
    public static PageHomePageMarket? FrmHomePageMarket;
    public static PageSetupAbout? FrmSetupAbout;
    public static PageSetupLog? FrmSetupLog;
    public static PageSetupFeedback? FrmSetupFeedback;
    public static PageSetupGameLink? FrmSetupGameLink;
    public static PageSetupLauncherMisc? FrmSetupLauncherMisc;
    public static PageLoginAuth? FrmLoginAuth;
    public static PageLoginMs? FrmLoginMs;
    public static PageLoginProfile? FrmLoginProfile;
    public static PageLoginProfileSkin? FrmLoginProfileSkin;
    public static PageLoginOffline? FrmLoginOffline;
    public static PageInstanceLeft? FrmInstanceLeft;
    public static PageInstanceOverall? FrmInstanceOverall;
    public static PageInstanceCompResource? FrmInstanceMod;
    public static PageInstanceModDisabled? FrmInstanceModDisabled;
    public static PageInstanceScreenshot? FrmInstanceScreenshot;
    public static PageInstanceSaves? FrmInstanceSaves;
    public static PageInstanceCompResource? FrmInstanceShader;
    public static PageInstanceCompResource? FrmInstanceSchematic;
    public static PageInstanceCompResource? FrmInstanceResourcePack;
    public static PageInstanceSetup? FrmInstanceSetup;
    public static PageInstanceInstall? FrmInstanceInstall;
    public static PageInstanceExport? FrmInstanceExport;
    public static PageInstanceServer? FrmInstanceServer;
    public static PageInstanceSavesLeft? FrmInstanceSavesLeft;
    public static PageInstanceSavesInfo? FrmInstanceSavesInfo;
    public static PageInstanceSavesBackup? FrmInstanceSavesBackup;
    public static PageInstanceSavesDatapack? FrmInstanceSavesDatapack;
    public static PageDownloadCompDetail? FrmDownloadCompDetail;
    public static PageHomepageNewsView? FrmHomepageNews;

    public static ModLoader.LoaderTask<int, List<HelpEntry>> HelpLoader = new("Help Page", HelpLoad, null,
        ThreadPriority.BelowNormal);


    #region 页面声明

    // 在最后进行页面声明，避免颜色尚未加载完毕

    // 窗体声明


    // 页面声明（出于单元测试考虑，初始化页面已转入 FormMain 中）


    // 工具页面声明


    // 下载页面声明


    // 设置页面声明


    // 登录页面声明


    // 实例设置页面声明


    // 实例存档页面


    // 资源信息分页声明
    
    #endregion
}

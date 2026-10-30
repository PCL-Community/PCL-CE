using System.Windows;
using System.Windows.Controls;
using PCL.Core.App;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageInstanceLeft : IRefreshable
{
    /// <summary>
    ///     当前显示设置的 MC 实例。
    /// </summary>
    public static McInstance? mcInstance = null;

    public PageInstanceLeft()
    {
        InitializeComponent();
        Loaded += (_, _) => RefreshModDisabled();
    }

    public void Refresh()
    {
        Refresh(Launcher.frmMain.PageCurrentSub);
    }

    public void RefreshModDisabled()
    {
        var hide = Config.Preference.Hide;

        if (mcInstance is not null && mcInstance.Modable)
        {
            ItemMod.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceMod
                ? Visibility.Collapsed
                : Visibility.Visible;
            ItemModDisabled.Visibility = Visibility.Collapsed;
        }
        else
        {
            ItemMod.Visibility = Visibility.Collapsed;
            ItemModDisabled.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceMod
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        // 功能隐藏
        if (!PageSetupUI.HiddenForceShow)
        {
            var disableCount = 0;
            if (hide.InstanceSave)
                disableCount += 1;
            if (hide.InstanceScreenshot)
                disableCount += 1;
            if (hide.InstanceMod)
                disableCount += 1;
            if (hide.InstanceResourcePack)
                disableCount += 1;
            if (hide.InstanceShader)
                disableCount += 1;
            if (hide.InstanceSchematic)
                disableCount += 1;
            if (hide.InstanceServer)
                disableCount += 1;
            if (disableCount == 7)
                TextResource.Visibility = Visibility.Collapsed;
            else
                TextResource.Visibility = Visibility.Visible;
        }
        else
        {
            TextResource.Visibility = Visibility.Visible;
        }

        ItemInstall.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceEdit
            ? Visibility.Collapsed
            : Visibility.Visible;
        ItemExport.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceExport
            ? Visibility.Collapsed
            : Visibility.Visible;
        ItemWorld.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceSave
            ? Visibility.Collapsed
            : Visibility.Visible;
        ItemScreenshot.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceScreenshot
            ? Visibility.Collapsed
            : Visibility.Visible;
        ItemResourcePack.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceResourcePack
            ? Visibility.Collapsed
            : Visibility.Visible;
        ItemShader.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceShader
            ? Visibility.Collapsed
            : Visibility.Visible;
        ItemSchematic.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceSchematic
            ? Visibility.Collapsed
            : Visibility.Visible;
        ItemServer.Visibility = !PageSetupUI.HiddenForceShow && hide.InstanceServer
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void RefreshButton_Click(object sender, EventArgs e) // 由边栏按钮匿名调用
    {
        Refresh((FormMain.PageSubType)Common.Val(((MyIconButton)sender).Tag));
    }

    public void Refresh(FormMain.PageSubType subType)
    {
        switch (subType)
        {
            case FormMain.PageSubType.VersionMod:
            {
                PageInstanceCompResource.Refresh(Comp.CompType.Mod);
                break;
            }
            case FormMain.PageSubType.VersionScreenshot:
            {
                var ignore= PageInstanceScreenshot.RefreshAsync();
                break;
            }
            case FormMain.PageSubType.VersionWorld:
            {
                PageInstanceSaves.Refresh();
                break;
            }
            case FormMain.PageSubType.VersionResourcePack:
            {
                PageInstanceCompResource.Refresh(Comp.CompType.ResourcePack);
                break;
            }
            case FormMain.PageSubType.VersionShader:
            {
                PageInstanceCompResource.Refresh(Comp.CompType.Shader);
                break;
            }
            case FormMain.PageSubType.VersionSchematic:
            {
                PageInstanceCompResource.Refresh(Comp.CompType.Schematic);
                break;
            }
            case FormMain.PageSubType.VersionInstall:
            {
                GameDownload.dlClientListLoader.Start(isForceRestart: true);
                GameDownload.DlOptiFineListLoader.Start(isForceRestart: true);
                GameDownload.DlForgeListLoader.Start(isForceRestart: true);
                GameDownload.DlNeoForgeListLoader.Start(isForceRestart: true);
                GameDownload.DlCleanroomListLoader.Start(isForceRestart: true);
                GameDownload.DlLiteLoaderListLoader.Start(isForceRestart: true);
                GameDownload.DlFabricListLoader.Start(isForceRestart: true);
                GameDownload.DlFabricApiLoader.Start(isForceRestart: true);
                GameDownload.DlLegacyFabricListLoader.Start(isForceRestart: true);
                GameDownload.DlLegacyFabricApiLoader.Start(isForceRestart: true);
                GameDownload.DlOptiFabricLoader.Start(isForceRestart: true);
                GameDownload.DlLabyModListLoader.Start(isForceRestart: true);
                ItemInstall.Checked = true;
                Launcher.frmInstanceInstall.GetCurrentInfo();
                break;
            }
            case FormMain.PageSubType.VersionExport:
            {
                if (Launcher.frmInstanceExport is not null)
                    Launcher.frmInstanceExport.RefreshAll();
                ItemExport.Checked = true;
                break;
            }
            case FormMain.PageSubType.VersionServer:
            {
                if (Launcher.frmInstanceServer is not null)
                    Launcher.frmInstanceServer.RefreshServers();
                ItemServer.Checked = true;
                break;
            }
        }
    }

    public void Reset(object sender, EventArgs e)
    {
        if (Launcher.MyMsgBox(Lang.Text("Instance.Left.InitializeSettings.ConfirmMessage"),
                Lang.Text("Instance.Left.InitializeSettings.ConfirmTitle"),
                button2: Lang.Text("Common.Action.Cancel"),
                isWarn: true)
            == 1)
        {
            if (Launcher.frmInstanceSetup is null)
                Launcher.frmInstanceSetup = new PageInstanceSetup();
            Launcher.frmInstanceSetup.Reset();
            ItemSetup.Checked = true;
        }
    }

    #region 页面切换

    /// <summary>
    ///     当前页面的编号。从 0 开始计算。
    /// </summary>
    public FormMain.PageSubType PageId = FormMain.PageSubType.Default;

    /// <summary>
    ///     勾选事件改变页面。
    /// </summary>
    private void PageCheck(object sender, Common.RouteEventArgs e)
    {
        if (sender is MyListItem item && item.Tag is not null)
            PageChange((FormMain.PageSubType)Common.Val(item.Tag));
    }

    public object PageGet(FormMain.PageSubType id)
    {
        if ((int)id == -1)
            id = PageId;
        switch (id)
        {
            case FormMain.PageSubType.VersionOverall:
            {
                if (Launcher.frmInstanceOverall is null)
                    Launcher.frmInstanceOverall = new PageInstanceOverall();
                return Launcher.frmInstanceOverall;
            }
            case FormMain.PageSubType.VersionMod:
            {
                if (Launcher.frmInstanceMod is null)
                    Launcher.frmInstanceMod = new PageInstanceCompResource(Comp.CompType.Mod);
                return Launcher.frmInstanceMod;
            }
            case FormMain.PageSubType.VersionModDisabled:
            {
                if (Launcher.frmInstanceModDisabled is null)
                    Launcher.frmInstanceModDisabled = new PageInstanceModDisabled();
                return Launcher.frmInstanceModDisabled;
            }
            case FormMain.PageSubType.VersionModJarInJar:
            {
                if (Launcher.frmInstanceModJarInJar is null)
                    Launcher.frmInstanceModJarInJar = new PageInstanceCompJarInJar();
                return Launcher.frmInstanceModJarInJar;
            }
            case FormMain.PageSubType.VersionSetup:
            {
                if (Launcher.frmInstanceSetup is null)
                    Launcher.frmInstanceSetup = new PageInstanceSetup();
                return Launcher.frmInstanceSetup;
            }
            case FormMain.PageSubType.VersionWorld:
            {
                if (Launcher.frmInstanceSaves is null)
                    Launcher.frmInstanceSaves = new PageInstanceSaves();
                return Launcher.frmInstanceSaves;
            }
            case FormMain.PageSubType.VersionScreenshot:
            {
                if (Launcher.frmInstanceScreenshot is null)
                    Launcher.frmInstanceScreenshot = new PageInstanceScreenshot();
                return Launcher.frmInstanceScreenshot;
            }
            case FormMain.PageSubType.VersionResourcePack:
            {
                if (Launcher.frmInstanceResourcePack is null)
                    Launcher.frmInstanceResourcePack =
                        new PageInstanceCompResource(Comp.CompType.ResourcePack);
                return Launcher.frmInstanceResourcePack;
            }
            case FormMain.PageSubType.VersionShader:
            {
                if (Launcher.frmInstanceShader is null)
                    Launcher.frmInstanceShader = new PageInstanceCompResource(Comp.CompType.Shader);
                return Launcher.frmInstanceShader;
            }
            case FormMain.PageSubType.VersionSchematic:
            {
                if (Launcher.frmInstanceSchematic is null)
                    Launcher.frmInstanceSchematic =
                        new PageInstanceCompResource(Comp.CompType.Schematic);
                return Launcher.frmInstanceSchematic;
            }
            case FormMain.PageSubType.VersionInstall:
            {
                if (Launcher.frmInstanceInstall is null)
                    Launcher.frmInstanceInstall = new PageInstanceInstall();
                return Launcher.frmInstanceInstall;
            }
            case FormMain.PageSubType.VersionExport:
            {
                if (Launcher.frmInstanceExport is null)
                    Launcher.frmInstanceExport = new PageInstanceExport();
                return Launcher.frmInstanceExport;
            }
            case FormMain.PageSubType.VersionServer:
            {
                if (Launcher.frmInstanceServer is null)
                    Launcher.frmInstanceServer = new PageInstanceServer();
                return Launcher.frmInstanceServer;
            }

            default:
            {
                throw new Exception("未知的实例设置子页面种类：" + (int)id);
            }
        }
    }

    /// <summary>
    ///     切换现有页面。
    /// </summary>
    public void PageChange(FormMain.PageSubType id)
    {
        if (PageId == id)
            return;
        Animations.AniBeginControl();
        try
        {
            PageChangeRun((MyPageRight)PageGet(id));
            PageId = id;
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "切换分页面失败（ID " + (int)id + "）",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Error.OperationFailed"));
        }
        finally
        {
            Animations.AniEndControl();
        }
    }

    private static void PageChangeRun(MyPageRight target)
    {
        Animations.AniStop("FrmMain PageChangeRight"); // 停止主页面的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
        if (target.Parent is not null)
            target.SetValue(ContentPresenter.ContentProperty, null);
        Launcher.frmMain.pageRight = target;
        ((MyPageRight)Launcher.frmMain.PanMainRight.Child).PageOnExit();
        Animations.AniStart(new[]
        {
            Animations.AaCode(() =>
            {
                ((MyPageRight)Launcher.frmMain.PanMainRight.Child).PageOnForceExit();
                Launcher.frmMain.PanMainRight.Child = Launcher.frmMain.pageRight;
                Launcher.frmMain.pageRight.Opacity = 0d;
            }, 130),
            Animations.AaCode(() =>
            {
                // 延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                Launcher.frmMain.pageRight.Opacity = 1d;
                Launcher.frmMain.pageRight.PageOnEnter();
            }, 30, true)
        }, "PageLeft PageChange");
    }

    #endregion
}

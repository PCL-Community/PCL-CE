using System.Windows;
using System.Windows.Controls;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageInstanceLeft : IRefreshable, IPageView
{
    public static McInstance McInstance = null;

    private readonly PageInstanceLeftViewModel _vm = new();
    private bool _refreshed;

    public PageInstanceLeft()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += (_, _) => RefreshOnNavigate();
        Unloaded += (_, _) => _refreshed = false;
    }

    public PageInstanceLeftViewModel ViewModel => _vm;

    public FormMain.PageSubType pageID = FormMain.PageSubType.Default;

    public void OnNavigatedTo(object? parameter)
    {
        RefreshOnNavigate();
    }

    public void OnNavigatedFrom()
    {
    }

    private void RefreshOnNavigate()
    {
        if (_refreshed)
            return;
        _refreshed = true;
        RefreshModDisabled();
    }

    public void Refresh()
    {
        Refresh(Launcher.frmMain.PageCurrentSub);
    }

    public void RefreshModDisabled()
    {
        _vm.RefreshModDisabled(McInstance);
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
                _ = PageInstanceScreenshot.RefreshAsync();
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

    public async void Reset(object sender, EventArgs e)
    {
        var control = sender as FrameworkElement;
        if (control is not null)
            control.IsEnabled = false;
        try
        {
            var choice = await Launcher.MyMsgBoxAsync(
                PageInstanceLeftViewModel.GetResetConfirmMessage(),
                PageInstanceLeftViewModel.GetResetConfirmTitle(),
                button2: PageInstanceLeftViewModel.GetCancelText(),
                isWarn: true);
            if (choice != 1)
                return;
            if (Launcher.frmInstanceSetup is null)
                Launcher.frmInstanceSetup = new PageInstanceSetup();
            Launcher.frmInstanceSetup.Reset();
            ItemSetup.Checked = true;
        }
        catch (Exception ex)
        {
            Common.Log(ex, "初始化实例设置失败", Common.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Error.OperationFailed"));
        }
        finally
        {
            if (control is not null)
                control.IsEnabled = true;
        }
    }

    private void PageCheck(object sender, Common.RouteEventArgs e)
    {
        if (sender is MyListItem item && item.Tag is not null)
            PageChange((FormMain.PageSubType)Common.Val(item.Tag));
    }

    public object PageGet(FormMain.PageSubType id)
    {
        if ((int)id == -1)
            id = pageID;
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
                    Launcher.frmInstanceResourcePack = new PageInstanceCompResource(Comp.CompType.ResourcePack);
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
                    Launcher.frmInstanceSchematic = new PageInstanceCompResource(Comp.CompType.Schematic);
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
                throw new Exception(PageInstanceLeftViewModel.GetUnknownSubPageMessage(id));
            }
        }
    }

    public void PageChange(FormMain.PageSubType id)
    {
        if (pageID == id)
            return;
        Animations.AniBeginControl();
        try
        {
            PageChangeRun((MyPageRight)PageGet(id));
            pageID = id;
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                PageInstanceLeftViewModel.GetSwitchFailedMessage(id),
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
        Animations.AniStop("FrmMain PageChangeRight");
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
                Launcher.frmMain.pageRight.Opacity = 1d;
                Launcher.frmMain.pageRight.PageOnEnter();
            }, 30, true)
        }, "PageLeft PageChange");
    }
}

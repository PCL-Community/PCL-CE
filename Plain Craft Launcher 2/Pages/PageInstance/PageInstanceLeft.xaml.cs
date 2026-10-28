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
        Refresh(ModMain.frmMain.PageCurrentSub);
    }

    public void RefreshModDisabled()
    {
        _vm.RefreshModDisabled(McInstance);
    }

    private void RefreshButton_Click(object sender, EventArgs e)
    {
        Refresh((FormMain.PageSubType)ModBase.Val(((MyIconButton)sender).Tag));
    }

    public void Refresh(FormMain.PageSubType subType)
    {
        switch (subType)
        {
            case FormMain.PageSubType.VersionMod:
            {
                PageInstanceCompResource.Refresh(ModComp.CompType.Mod);
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
                PageInstanceCompResource.Refresh(ModComp.CompType.ResourcePack);
                break;
            }
            case FormMain.PageSubType.VersionShader:
            {
                PageInstanceCompResource.Refresh(ModComp.CompType.Shader);
                break;
            }
            case FormMain.PageSubType.VersionSchematic:
            {
                PageInstanceCompResource.Refresh(ModComp.CompType.Schematic);
                break;
            }
            case FormMain.PageSubType.VersionInstall:
            {
                ModDownload.dlClientListLoader.Start(isForceRestart: true);
                ModDownload.dlOptiFineListLoader.Start(isForceRestart: true);
                ModDownload.dlForgeListLoader.Start(isForceRestart: true);
                ModDownload.dlNeoForgeListLoader.Start(isForceRestart: true);
                ModDownload.dlLiteLoaderListLoader.Start(isForceRestart: true);
                ModDownload.dlFabricListLoader.Start(isForceRestart: true);
                ModDownload.dlFabricApiLoader.Start(isForceRestart: true);
                ModDownload.dlOptiFabricLoader.Start(isForceRestart: true);
                ModDownload.dlLabyModListLoader.Start(isForceRestart: true);
                ItemInstall.Checked = true;
                ModMain.frmInstanceInstall.GetCurrentInfo();
                break;
            }
            case FormMain.PageSubType.VersionExport:
            {
                if (ModMain.frmInstanceExport is not null)
                    ModMain.frmInstanceExport.RefreshAll();
                ItemExport.Checked = true;
                break;
            }
            case FormMain.PageSubType.VersionServer:
            {
                if (ModMain.frmInstanceServer is not null)
                    ModMain.frmInstanceServer.RefreshServers();
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
            var choice = await ModMain.MyMsgBoxAsync(
                PageInstanceLeftViewModel.GetResetConfirmMessage(),
                PageInstanceLeftViewModel.GetResetConfirmTitle(),
                button2: PageInstanceLeftViewModel.GetCancelText(),
                isWarn: true);
            if (choice != 1)
                return;
            if (ModMain.frmInstanceSetup is null)
                ModMain.frmInstanceSetup = new PageInstanceSetup();
            ModMain.frmInstanceSetup.Reset();
            ItemSetup.Checked = true;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "初始化实例设置失败", ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Instance.Error.OperationFailed"));
        }
        finally
        {
            if (control is not null)
                control.IsEnabled = true;
        }
    }

    private void PageCheck(object sender, ModBase.RouteEventArgs e)
    {
        if (sender is MyListItem item && item.Tag is not null)
            PageChange((FormMain.PageSubType)ModBase.Val(item.Tag));
    }

    public object PageGet(FormMain.PageSubType id)
    {
        if ((int)id == -1)
            id = pageID;
        switch (id)
        {
            case FormMain.PageSubType.VersionOverall:
            {
                if (ModMain.frmInstanceOverall is null)
                    ModMain.frmInstanceOverall = new PageInstanceOverall();
                return ModMain.frmInstanceOverall;
            }
            case FormMain.PageSubType.VersionMod:
            {
                if (ModMain.frmInstanceMod is null)
                    ModMain.frmInstanceMod = new PageInstanceCompResource(ModComp.CompType.Mod);
                return ModMain.frmInstanceMod;
            }
            case FormMain.PageSubType.VersionModDisabled:
            {
                if (ModMain.frmInstanceModDisabled is null)
                    ModMain.frmInstanceModDisabled = new PageInstanceModDisabled();
                return ModMain.frmInstanceModDisabled;
            }
            case FormMain.PageSubType.VersionModJarInJar:
            {
                if (ModMain.frmInstanceModJarInJar is null)
                    ModMain.frmInstanceModJarInJar = new PageInstanceCompJarInJar();
                return ModMain.frmInstanceModJarInJar;
            }
            case FormMain.PageSubType.VersionSetup:
            {
                if (ModMain.frmInstanceSetup is null)
                    ModMain.frmInstanceSetup = new PageInstanceSetup();
                return ModMain.frmInstanceSetup;
            }
            case FormMain.PageSubType.VersionWorld:
            {
                if (ModMain.frmInstanceSaves is null)
                    ModMain.frmInstanceSaves = new PageInstanceSaves();
                return ModMain.frmInstanceSaves;
            }
            case FormMain.PageSubType.VersionScreenshot:
            {
                if (ModMain.frmInstanceScreenshot is null)
                    ModMain.frmInstanceScreenshot = new PageInstanceScreenshot();
                return ModMain.frmInstanceScreenshot;
            }
            case FormMain.PageSubType.VersionResourcePack:
            {
                if (ModMain.frmInstanceResourcePack is null)
                    ModMain.frmInstanceResourcePack = new PageInstanceCompResource(ModComp.CompType.ResourcePack);
                return ModMain.frmInstanceResourcePack;
            }
            case FormMain.PageSubType.VersionShader:
            {
                if (ModMain.frmInstanceShader is null)
                    ModMain.frmInstanceShader = new PageInstanceCompResource(ModComp.CompType.Shader);
                return ModMain.frmInstanceShader;
            }
            case FormMain.PageSubType.VersionSchematic:
            {
                if (ModMain.frmInstanceSchematic is null)
                    ModMain.frmInstanceSchematic = new PageInstanceCompResource(ModComp.CompType.Schematic);
                return ModMain.frmInstanceSchematic;
            }
            case FormMain.PageSubType.VersionInstall:
            {
                if (ModMain.frmInstanceInstall is null)
                    ModMain.frmInstanceInstall = new PageInstanceInstall();
                return ModMain.frmInstanceInstall;
            }
            case FormMain.PageSubType.VersionExport:
            {
                if (ModMain.frmInstanceExport is null)
                    ModMain.frmInstanceExport = new PageInstanceExport();
                return ModMain.frmInstanceExport;
            }
            case FormMain.PageSubType.VersionServer:
            {
                if (ModMain.frmInstanceServer is null)
                    ModMain.frmInstanceServer = new PageInstanceServer();
                return ModMain.frmInstanceServer;
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
        ModAnimation.AniBeginControl();
        try
        {
            PageChangeRun((MyPageRight)PageGet(id));
            pageID = id;
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                PageInstanceLeftViewModel.GetSwitchFailedMessage(id),
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Instance.Error.OperationFailed"));
        }
        finally
        {
            ModAnimation.AniEndControl();
        }
    }

    private static void PageChangeRun(MyPageRight target)
    {
        ModAnimation.AniStop("FrmMain PageChangeRight");
        if (target.Parent is not null)
            target.SetValue(ContentPresenter.ContentProperty, null);
        ModMain.frmMain.pageRight = target;
        ((MyPageRight)ModMain.frmMain.PanMainRight.Child).PageOnExit();
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() =>
            {
                ((MyPageRight)ModMain.frmMain.PanMainRight.Child).PageOnForceExit();
                ModMain.frmMain.PanMainRight.Child = ModMain.frmMain.pageRight;
                ModMain.frmMain.pageRight.Opacity = 0d;
            }, 130),
            ModAnimation.AaCode(() =>
            {
                ModMain.frmMain.pageRight.Opacity = 1d;
                ModMain.frmMain.pageRight.PageOnEnter();
            }, 30, true)
        }, "PageLeft PageChange");
    }
}

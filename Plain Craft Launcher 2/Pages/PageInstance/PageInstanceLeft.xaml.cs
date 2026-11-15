using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using System.Windows;
using System.Windows.Controls;

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
                    CompDownloadLoaders.DlFabricApiLoader.Start(isForceRestart: true);
                    GameDownload.DlLegacyFabricListLoader.Start(isForceRestart: true);
                    CompDownloadLoaders.DlLegacyFabricApiLoader.Start(isForceRestart: true);
                    CompDownloadLoaders.DlOptiFabricLoader.Start(isForceRestart: true);
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
            var choice = (await DialogService.Instance.ShowAsync(new TextDialogRequest
            {
                Title = PageInstanceLeftViewModel.GetResetConfirmTitle(),
                Content = PageInstanceLeftViewModel.GetResetConfirmMessage(),
                Buttons = DialogButtons.From(DialogButton.Create(Lang.Text("Common.Action.Confirm")), DialogButton.Create(PageInstanceLeftViewModel.GetCancelText())),
                IsWarn = true,
                ForceWait = false
            }).ConfigureAwait(true)).Button;
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
                    Launcher.frmInstanceOverall ??= new PageInstanceOverall();
                    return Launcher.frmInstanceOverall;
                }
            case FormMain.PageSubType.VersionMod:
                {
                    Launcher.frmInstanceMod ??= new PageInstanceCompResource(Comp.CompType.Mod);
                    return Launcher.frmInstanceMod;
                }
            case FormMain.PageSubType.VersionModDisabled:
                {
                    Launcher.frmInstanceModDisabled ??= new PageInstanceModDisabled();
                    return Launcher.frmInstanceModDisabled;
                }
            case FormMain.PageSubType.VersionModJarInJar:
                {
                    Launcher.frmInstanceModJarInJar ??= new PageInstanceCompJarInJar();
                    return Launcher.frmInstanceModJarInJar;
                }
            case FormMain.PageSubType.VersionSetup:
                {
                    Launcher.frmInstanceSetup ??= new PageInstanceSetup();
                    return Launcher.frmInstanceSetup;
                }
            case FormMain.PageSubType.VersionWorld:
                {
                    Launcher.frmInstanceSaves ??= new PageInstanceSaves();
                    return Launcher.frmInstanceSaves;
                }
            case FormMain.PageSubType.VersionScreenshot:
                {
                    Launcher.frmInstanceScreenshot ??= new PageInstanceScreenshot();
                    return Launcher.frmInstanceScreenshot;
                }
            case FormMain.PageSubType.VersionResourcePack:
                {
                    Launcher.frmInstanceResourcePack ??= new PageInstanceCompResource(Comp.CompType.ResourcePack);
                    return Launcher.frmInstanceResourcePack;
                }
            case FormMain.PageSubType.VersionShader:
                {
                    Launcher.frmInstanceShader ??= new PageInstanceCompResource(Comp.CompType.Shader);
                    return Launcher.frmInstanceShader;
                }
            case FormMain.PageSubType.VersionSchematic:
                {
                    Launcher.frmInstanceSchematic ??= new PageInstanceCompResource(Comp.CompType.Schematic);
                    return Launcher.frmInstanceSchematic;
                }
            case FormMain.PageSubType.VersionInstall:
                {
                    Launcher.frmInstanceInstall ??= new PageInstanceInstall();
                    return Launcher.frmInstanceInstall;
                }
            case FormMain.PageSubType.VersionExport:
                {
                    Launcher.frmInstanceExport ??= new PageInstanceExport();
                    return Launcher.frmInstanceExport;
                }
            case FormMain.PageSubType.VersionServer:
                {
                    Launcher.frmInstanceServer ??= new PageInstanceServer();
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

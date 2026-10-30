using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageDownloadLeft : IRefreshable
{
    public void Refresh()
    {
        Refresh(Launcher.frmMain.PageCurrentSub);
    }

    // 强制刷新
    public void RefreshButton_Click(object sender, EventArgs e) // 由边栏按钮匿名调用
    {
        Refresh((FormMain.PageSubType)Common.Val(((MyIconButton)sender).Tag));
    }

    public void Refresh(FormMain.PageSubType subType)
    {
        switch (subType)
        {
            case FormMain.PageSubType.DownloadInstall:
            {
                GameDownload.dlClientListLoader.Start(isForceRestart: true);
                GameDownload.DlOptiFineListLoader.Start(isForceRestart: true);
                GameDownload.DlForgeListLoader.Start(isForceRestart: true);
                GameDownload.DlNeoForgeListLoader.Start(isForceRestart: true);
                GameDownload.DlCleanroomListLoader.Start(isForceRestart: true);
                GameDownload.DlLiteLoaderListLoader.Start(isForceRestart: true);
                GameDownload.DlFabricListLoader.Start(isForceRestart: true);
                GameDownload.DlLegacyFabricListLoader.Start(isForceRestart: true);
                GameDownload.DlFabricApiLoader.Start(isForceRestart: true);
                GameDownload.DlLegacyFabricApiLoader.Start(isForceRestart: true);
                GameDownload.DlOptiFabricLoader.Start(isForceRestart: true);
                GameDownload.DlLabyModListLoader.Start(isForceRestart: true);
                ItemInstall.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadMod:
            {
                Comp.compProjectCache.Clear();
                Comp.compFilesCache.Clear();
                if (Launcher.frmDownloadMod is not null)
                {
                    Launcher.frmDownloadMod.Content.storage = new Comp.CompProjectStorage();
                    Launcher.frmDownloadMod.Content.page = 0;
                    Launcher.frmDownloadMod.PageLoaderRestart();
                }

                ItemMod.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadPack:
            {
                Comp.compProjectCache.Clear();
                Comp.compFilesCache.Clear();
                if (Launcher.frmDownloadPack is not null)
                {
                    Launcher.frmDownloadPack.Content.storage = new Comp.CompProjectStorage();
                    Launcher.frmDownloadPack.Content.page = 0;
                    Launcher.frmDownloadPack.PageLoaderRestart();
                }

                ItemPack.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadDataPack:
            {
                Comp.compProjectCache.Clear();
                Comp.compFilesCache.Clear();
                if (Launcher.frmDownloadDataPack is not null)
                {
                    Launcher.frmDownloadDataPack.Content.storage = new Comp.CompProjectStorage();
                    Launcher.frmDownloadDataPack.Content.page = 0;
                    Launcher.frmDownloadDataPack.PageLoaderRestart();
                }

                ItemDataPack.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadResourcePack:
            {
                Comp.compProjectCache.Clear();
                Comp.compFilesCache.Clear();
                if (Launcher.frmDownloadResourcePack is not null)
                {
                    Launcher.frmDownloadResourcePack.Content.storage =
                        new Comp.CompProjectStorage();
                    Launcher.frmDownloadResourcePack.Content.page = 0;
                    Launcher.frmDownloadResourcePack.PageLoaderRestart();
                }

                ItemResourcePack.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadShader:
            {
                Comp.compProjectCache.Clear();
                Comp.compFilesCache.Clear();
                if (Launcher.frmDownloadShader is not null)
                {
                    Launcher.frmDownloadShader.Content.storage = new Comp.CompProjectStorage();
                    Launcher.frmDownloadShader.Content.page = 0;
                    Launcher.frmDownloadShader.PageLoaderRestart();
                }

                ItemShader.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadWorld:
            {
                Comp.compProjectCache.Clear();
                Comp.compFilesCache.Clear();
                if (Launcher.frmDownloadWorld is not null)
                {
                    Launcher.frmDownloadWorld.Content.storage = new Comp.CompProjectStorage();
                    Launcher.frmDownloadWorld.Content.page = 0;
                    Launcher.frmDownloadWorld.PageLoaderRestart();
                }

                ItemWorld.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadClient:
            {
                GameDownload.dlClientListLoader.Start(isForceRestart: true);
                ItemClient.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadOptiFine:
            {
                GameDownload.DlOptiFineListLoader.Start(isForceRestart: true);
                ItemOptiFine.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadForge:
            {
                GameDownload.DlForgeListLoader.Start(isForceRestart: true);
                ItemForge.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadNeoForge:
            {
                GameDownload.DlNeoForgeListLoader.Start(isForceRestart: true);
                ItemNeoForge.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadCleanroom:
            {
                GameDownload.DlCleanroomListLoader.Start(isForceRestart: true);
                ItemCleanroom.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadLiteLoader:
            {
                GameDownload.DlLiteLoaderListLoader.Start(isForceRestart: true);
                ItemLiteLoader.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadFabric:
            {
                GameDownload.DlFabricListLoader.Start(isForceRestart: true);
                ItemFabric.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadLabyMod:
            {
                GameDownload.DlLabyModListLoader.Start(isForceRestart: true);
                ItemLabyMod.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadLegacyFabric:
            {
                GameDownload.DlLegacyFabricListLoader.Start(isForceRestart: true);
                ItemLegacyFabric.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadCompFavorites:
            {
                if (Launcher.frmDownloadCompFavorites is not null)
                    Launcher.frmDownloadCompFavorites.PageLoaderRestart();
                ItemFavorites.Checked = true;
                break;
            }
        }

        HintService.Hint(Lang.Text("Download.Left.Hint.Refreshing"), log: false);
    }

    // 点击返回
    private void ItemInstall_Click(object sender, MouseButtonEventArgs e)
    {
        if (!ItemInstall.Checked)
            return;
        Launcher.frmDownloadInstall.ExitSelectPage();
    }

    #region 页面切换

    /// <summary>
    ///     当前页面的编号。
    /// </summary>
    public FormMain.PageSubType PageId = FormMain.PageSubType.DownloadInstall;

    public PageDownloadLeft()
    {
        InitializeComponent();
        ItemInstall.Check += PageCheck;
        ItemMod.Check += PageCheck;
        ItemPack.Check += PageCheck;
        ItemDataPack.Check += PageCheck;
        ItemResourcePack.Check += PageCheck;
        ItemShader.Check += PageCheck;
        ItemWorld.Check += PageCheck;
        ItemFavorites.Check += PageCheck;
        ItemClient.Check += PageCheck;
        ItemOptiFine.Check += PageCheck;
        ItemForge.Check += PageCheck;
        ItemNeoForge.Check += PageCheck;
        ItemLiteLoader.Check += PageCheck;
        ItemFabric.Check += PageCheck;
        ItemLegacyFabric.Check += PageCheck;
        ItemLabyMod.Check += PageCheck;

        AnimatedControl = PanItem;
    }

    /// <summary>
    ///     勾选事件改变页面。
    /// </summary>
    private void PageCheck(object sender, Common.RouteEventArgs e)
    {
        if (sender is MyListItem { Tag: { } tag })
            PageChange((FormMain.PageSubType)Common.Val(tag));
    }

    public object PageGet(FormMain.PageSubType id)
    {
        if (id == default)
            id = PageId;
        switch (id)
        {
            case FormMain.PageSubType.DownloadInstall:
            {
                if (Launcher.frmDownloadInstall is null)
                    Launcher.frmDownloadInstall = new PageDownloadInstall();
                return Launcher.frmDownloadInstall;
            }
            case FormMain.PageSubType.DownloadMod:
            {
                if (Launcher.frmDownloadMod is null)
                    Launcher.frmDownloadMod = new PageDownloadMod();
                return Launcher.frmDownloadMod;
            }
            case FormMain.PageSubType.DownloadPack:
            {
                if (Launcher.frmDownloadPack is null)
                    Launcher.frmDownloadPack = new PageDownloadPack();
                return Launcher.frmDownloadPack;
            }
            case FormMain.PageSubType.DownloadDataPack:
            {
                if (Launcher.frmDownloadDataPack is null)
                    Launcher.frmDownloadDataPack = new PageDownloadDataPack();
                return Launcher.frmDownloadDataPack;
            }
            case FormMain.PageSubType.DownloadResourcePack:
            {
                if (Launcher.frmDownloadResourcePack is null)
                    Launcher.frmDownloadResourcePack = new PageDownloadResourcePack();
                return Launcher.frmDownloadResourcePack;
            }
            case FormMain.PageSubType.DownloadShader:
            {
                if (Launcher.frmDownloadShader is null)
                    Launcher.frmDownloadShader = new PageDownloadShader();
                return Launcher.frmDownloadShader;
            }
            case FormMain.PageSubType.DownloadWorld:
            {
                if (Launcher.frmDownloadWorld is null)
                    Launcher.frmDownloadWorld = new PageDownloadWorld();
                return Launcher.frmDownloadWorld;
            }
            case FormMain.PageSubType.DownloadCompFavorites:
            {
                if (Launcher.frmDownloadCompFavorites is null)
                    Launcher.frmDownloadCompFavorites = new PageDownloadCompFavorites();
                return Launcher.frmDownloadCompFavorites;
            }
            case FormMain.PageSubType.DownloadClient:
            {
                if (Launcher.frmDownloadClient is null)
                    Launcher.frmDownloadClient = new PageDownloadClient();
                return Launcher.frmDownloadClient;
            }
            case FormMain.PageSubType.DownloadOptiFine:
            {
                if (Launcher.frmDownloadOptiFine is null)
                    Launcher.frmDownloadOptiFine = new PageDownloadOptiFine();
                return Launcher.frmDownloadOptiFine;
            }
            case FormMain.PageSubType.DownloadForge:
            {
                if (Launcher.frmDownloadForge is null)
                    Launcher.frmDownloadForge = new PageDownloadForge();
                return Launcher.frmDownloadForge;
            }
            case FormMain.PageSubType.DownloadNeoForge:
            {
                if (Launcher.frmDownloadNeoForge is null)
                    Launcher.frmDownloadNeoForge = new PageDownloadNeoForge();
                return Launcher.frmDownloadNeoForge;
            }
            case FormMain.PageSubType.DownloadCleanroom:
            {
                if (Launcher.frmDownloadCleanroom is null)
                    Launcher.frmDownloadCleanroom = new PageDownloadCleanroom();
                return Launcher.frmDownloadCleanroom;
            }
            case FormMain.PageSubType.DownloadLiteLoader:
            {
                if (Launcher.frmDownloadLiteLoader is null)
                    Launcher.frmDownloadLiteLoader = new PageDownloadLiteLoader();
                return Launcher.frmDownloadLiteLoader;
            }
            case FormMain.PageSubType.DownloadFabric:
            {
                if (Launcher.frmDownloadFabric is null)
                    Launcher.frmDownloadFabric = new PageDownloadFabric();
                return Launcher.frmDownloadFabric;
            }
            case FormMain.PageSubType.DownloadLabyMod:
            {
                if (Launcher.frmDownloadLabyMod is null)
                    Launcher.frmDownloadLabyMod = new PageDownloadLabyMod();
                return Launcher.frmDownloadLabyMod;
            }
            case FormMain.PageSubType.DownloadLegacyFabric:
            {
                if (Launcher.frmDownloadLegacyFabric is null)
                    Launcher.frmDownloadLegacyFabric = new PageDownloadLegacyFabric();
                return Launcher.frmDownloadLegacyFabric;
            }

            default:
            {
                throw new Exception(Lang.Text("Download.Left.Error.UnknownSubPageType", (int)id));
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
                userSummary: Lang.Text("Download.Error.OperationFailed"));
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

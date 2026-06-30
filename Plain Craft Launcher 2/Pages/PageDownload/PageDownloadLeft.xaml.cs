using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageDownloadLeft : IRefreshable
{
    public void Refresh()
    {
        Refresh(ModMain.frmMain.PageCurrentSub);
    }

    // 强制刷新
    public void RefreshButton_Click(object sender, EventArgs e) // 由边栏按钮匿名调用
    {
        Refresh((FormMain.PageSubType)ModBase.Val(((MyIconButton)sender).Tag));
    }

    public void Refresh(FormMain.PageSubType subType)
    {
        switch (subType)
        {
            case FormMain.PageSubType.DownloadInstall:
            {
                ModDownload.dlClientListLoader.Start(isForceRestart: true);
                ModDownload.dlOptiFineListLoader.Start(isForceRestart: true);
                ModDownload.dlForgeListLoader.Start(isForceRestart: true);
                ModDownload.dlNeoForgeListLoader.Start(isForceRestart: true);
                ModDownload.dlCleanroomListLoader.Start(isForceRestart: true);
                ModDownload.dlLiteLoaderListLoader.Start(isForceRestart: true);
                ModDownload.dlFabricListLoader.Start(isForceRestart: true);
                ModDownload.dlLegacyFabricListLoader.Start(isForceRestart: true);
                ModDownload.dlFabricApiLoader.Start(isForceRestart: true);
                ModDownload.dlLegacyFabricApiLoader.Start(isForceRestart: true);
                ModDownload.dlQuiltListLoader.Start(isForceRestart: true);
                ModDownload.dlQSLLoader.Start(isForceRestart: true);
                ModDownload.dlOptiFabricLoader.Start(isForceRestart: true);
                ModDownload.dlLabyModListLoader.Start(isForceRestart: true);
                ItemInstall.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadMod:
            {
                ModComp.compProjectCache.Clear();
                ModComp.compFilesCache.Clear();
                if (ModMain.frmDownloadMod is not null)
                {
                    ModMain.frmDownloadMod.Content.storage = new ModComp.CompProjectStorage();
                    ModMain.frmDownloadMod.Content.page = 0;
                    ModMain.frmDownloadMod.PageLoaderRestart();
                }

                ItemMod.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadPack:
            {
                ModComp.compProjectCache.Clear();
                ModComp.compFilesCache.Clear();
                if (ModMain.frmDownloadPack is not null)
                {
                    ModMain.frmDownloadPack.Content.storage = new ModComp.CompProjectStorage();
                    ModMain.frmDownloadPack.Content.page = 0;
                    ModMain.frmDownloadPack.PageLoaderRestart();
                }

                ItemPack.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadDataPack:
            {
                ModComp.compProjectCache.Clear();
                ModComp.compFilesCache.Clear();
                if (ModMain.frmDownloadDataPack is not null)
                {
                    ModMain.frmDownloadDataPack.Content.storage = new ModComp.CompProjectStorage();
                    ModMain.frmDownloadDataPack.Content.page = 0;
                    ModMain.frmDownloadDataPack.PageLoaderRestart();
                }

                ItemDataPack.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadResourcePack:
            {
                ModComp.compProjectCache.Clear();
                ModComp.compFilesCache.Clear();
                if (ModMain.frmDownloadResourcePack is not null)
                {
                    ModMain.frmDownloadResourcePack.Content.storage = new ModComp.CompProjectStorage();
                    ModMain.frmDownloadResourcePack.Content.page = 0;
                    ModMain.frmDownloadResourcePack.PageLoaderRestart();
                }

                ItemResourcePack.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadShader:
            {
                ModComp.compProjectCache.Clear();
                ModComp.compFilesCache.Clear();
                if (ModMain.frmDownloadShader is not null)
                {
                    ModMain.frmDownloadShader.Content.storage = new ModComp.CompProjectStorage();
                    ModMain.frmDownloadShader.Content.page = 0;
                    ModMain.frmDownloadShader.PageLoaderRestart();
                }

                ItemShader.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadWorld:
            {
                ModComp.compProjectCache.Clear();
                ModComp.compFilesCache.Clear();
                if (ModMain.frmDownloadWorld is not null)
                {
                    ModMain.frmDownloadWorld.Content.storage = new ModComp.CompProjectStorage();
                    ModMain.frmDownloadWorld.Content.page = 0;
                    ModMain.frmDownloadWorld.PageLoaderRestart();
                }

                ItemWorld.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadClient:
            {
                ModDownload.dlClientListLoader.Start(isForceRestart: true);
                ItemClient.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadOptiFine:
            {
                ModDownload.dlOptiFineListLoader.Start(isForceRestart: true);
                ItemOptiFine.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadForge:
            {
                ModDownload.dlForgeListLoader.Start(isForceRestart: true);
                ItemForge.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadNeoForge:
            {
                ModDownload.dlNeoForgeListLoader.Start(isForceRestart: true);
                ItemNeoForge.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadCleanroom:
            {
                ModDownload.dlCleanroomListLoader.Start(isForceRestart: true);
                ItemCleanroom.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadLiteLoader:
            {
                ModDownload.dlLiteLoaderListLoader.Start(isForceRestart: true);
                ItemLiteLoader.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadFabric:
            {
                ModDownload.dlFabricListLoader.Start(isForceRestart: true);
                ItemFabric.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadQuilt:
            {
                ModDownload.dlQuiltListLoader.Start(isForceRestart: true);
                ItemQuilt.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadLabyMod:
            {
                ModDownload.dlLabyModListLoader.Start(isForceRestart: true);
                ItemLabyMod.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadLegacyFabric:
            {
                ModDownload.dlLegacyFabricListLoader.Start(isForceRestart: true);
                ItemLegacyFabric.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadCompFavorites:
            {
                if (ModMain.frmDownloadCompFavorites is not null)
                    ModMain.frmDownloadCompFavorites.PageLoaderRestart();
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
        ModMain.frmDownloadInstall.ExitSelectPage();
    }

    #region 页面切换

    /// <summary>
    ///     当前页面的编号。
    /// </summary>
    public FormMain.PageSubType pageID = FormMain.PageSubType.DownloadInstall;

    public PageDownloadLeft()
    {
        AnimatedControl = PanItem;
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
        ItemQuilt.Check += PageCheck;
        ItemLabyMod.Check += PageCheck;
    }

    /// <summary>
    ///     勾选事件改变页面。
    /// </summary>
    private void PageCheck(object sender, ModBase.RouteEventArgs e)
    {
        if (sender is MyListItem { Tag: { } tag })
            PageChange((FormMain.PageSubType)ModBase.Val(tag));
    }

    public object PageGet(FormMain.PageSubType id)
    {
        if (id == default)
            id = pageID;
        switch (id)
        {
            case FormMain.PageSubType.DownloadInstall:
            {
                if (ModMain.frmDownloadInstall is null)
                    ModMain.frmDownloadInstall = new PageDownloadInstall();
                return ModMain.frmDownloadInstall;
            }
            case FormMain.PageSubType.DownloadMod:
            {
                if (ModMain.frmDownloadMod is null)
                    ModMain.frmDownloadMod = new PageDownloadMod();
                return ModMain.frmDownloadMod;
            }
            case FormMain.PageSubType.DownloadPack:
            {
                if (ModMain.frmDownloadPack is null)
                    ModMain.frmDownloadPack = new PageDownloadPack();
                return ModMain.frmDownloadPack;
            }
            case FormMain.PageSubType.DownloadDataPack:
            {
                if (ModMain.frmDownloadDataPack is null)
                    ModMain.frmDownloadDataPack = new PageDownloadDataPack();
                return ModMain.frmDownloadDataPack;
            }
            case FormMain.PageSubType.DownloadResourcePack:
            {
                if (ModMain.frmDownloadResourcePack is null)
                    ModMain.frmDownloadResourcePack = new PageDownloadResourcePack();
                return ModMain.frmDownloadResourcePack;
            }
            case FormMain.PageSubType.DownloadShader:
            {
                if (ModMain.frmDownloadShader is null)
                    ModMain.frmDownloadShader = new PageDownloadShader();
                return ModMain.frmDownloadShader;
            }
            case FormMain.PageSubType.DownloadWorld:
            {
                if (ModMain.frmDownloadWorld is null)
                    ModMain.frmDownloadWorld = new PageDownloadWorld();
                return ModMain.frmDownloadWorld;
            }
            case FormMain.PageSubType.DownloadCompFavorites:
            {
                if (ModMain.frmDownloadCompFavorites is null)
                    ModMain.frmDownloadCompFavorites = new PageDownloadCompFavorites();
                return ModMain.frmDownloadCompFavorites;
            }
            case FormMain.PageSubType.DownloadClient:
            {
                if (ModMain.frmDownloadClient is null)
                    ModMain.frmDownloadClient = new PageDownloadClient();
                return ModMain.frmDownloadClient;
            }
            case FormMain.PageSubType.DownloadOptiFine:
            {
                if (ModMain.frmDownloadOptiFine is null)
                    ModMain.frmDownloadOptiFine = new PageDownloadOptiFine();
                return ModMain.frmDownloadOptiFine;
            }
            case FormMain.PageSubType.DownloadForge:
            {
                if (ModMain.frmDownloadForge is null)
                    ModMain.frmDownloadForge = new PageDownloadForge();
                return ModMain.frmDownloadForge;
            }
            case FormMain.PageSubType.DownloadNeoForge:
            {
                if (ModMain.frmDownloadNeoForge is null)
                    ModMain.frmDownloadNeoForge = new PageDownloadNeoForge();
                return ModMain.frmDownloadNeoForge;
            }
            case FormMain.PageSubType.DownloadCleanroom:
            {
                if (ModMain.frmDownloadCleanroom is null)
                    ModMain.frmDownloadCleanroom = new PageDownloadCleanroom();
                return ModMain.frmDownloadCleanroom;
            }
            case FormMain.PageSubType.DownloadLiteLoader:
            {
                if (ModMain.frmDownloadLiteLoader is null)
                    ModMain.frmDownloadLiteLoader = new PageDownloadLiteLoader();
                return ModMain.frmDownloadLiteLoader;
            }
            case FormMain.PageSubType.DownloadFabric:
            {
                if (ModMain.frmDownloadFabric is null)
                    ModMain.frmDownloadFabric = new PageDownloadFabric();
                return ModMain.frmDownloadFabric;
            }
            case FormMain.PageSubType.DownloadQuilt:
            {
                if (ModMain.frmDownloadQuilt is null)
                    ModMain.frmDownloadQuilt = new PageDownloadQuilt();
                return ModMain.frmDownloadQuilt;
            }
            case FormMain.PageSubType.DownloadLabyMod:
            {
                if (ModMain.frmDownloadLabyMod is null)
                    ModMain.frmDownloadLabyMod = new PageDownloadLabyMod();
                return ModMain.frmDownloadLabyMod;
            }
            case FormMain.PageSubType.DownloadLegacyFabric:
            {
                if (ModMain.frmDownloadLegacyFabric is null)
                    ModMain.frmDownloadLegacyFabric = new PageDownloadLegacyFabric();
                return ModMain.frmDownloadLegacyFabric;
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
        if (pageID == id)
            return;
        ModAnimation.AniControlEnabled += 1;
        try
        {
            PageChangeRun((MyPageRight)PageGet(id));
            pageID = id;
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "切换分页面失败（ID " + (int)id + "）",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Download.Error.OperationFailed"));
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    private static void PageChangeRun(MyPageRight target)
    {
        ModAnimation.AniStop("FrmMain PageChangeRight"); // 停止主页面的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
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
                // 延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                ModMain.frmMain.pageRight.Opacity = 1d;
                ModMain.frmMain.pageRight.PageOnEnter();
            }, 30, true)
        }, "PageLeft PageChange");
    }

    #endregion
}
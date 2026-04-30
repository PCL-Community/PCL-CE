using System.Windows.Controls;
using System.Windows.Input;

namespace PCL;

public partial class PageDownloadLeft : IRefreshable
{
    public void Refresh()
    {
        Refresh(ModMain.FrmMain.PageCurrentSub);
    }

    // 强制刷新
    public void RefreshButton_Click(object sender, EventArgs e) // 由边栏按钮匿名调用
    {
        Refresh((FormMain.PageSubType)ModBase.Val(((MyIconButton)sender).Tag));
    }

    public void Refresh(FormMain.PageSubType SubType)
    {
        switch (SubType)
        {
            case FormMain.PageSubType.DownloadInstall:
            {
                ModDownload.DlClientListLoader.Start(IsForceRestart: true);
                ModDownload.DlOptiFineListLoader.Start(IsForceRestart: true);
                ModDownload.DlForgeListLoader.Start(IsForceRestart: true);
                ModDownload.DlNeoForgeListLoader.Start(IsForceRestart: true);
                ModDownload.DlCleanroomListLoader.Start(IsForceRestart: true);
                ModDownload.DlLiteLoaderListLoader.Start(IsForceRestart: true);
                ModDownload.DlFabricListLoader.Start(IsForceRestart: true);
                ModDownload.DlLegacyFabricListLoader.Start(IsForceRestart: true);
                ModDownload.DlFabricApiLoader.Start(IsForceRestart: true);
                ModDownload.DlLegacyFabricApiLoader.Start(IsForceRestart: true);
                ModDownload.DlQuiltListLoader.Start(IsForceRestart: true);
                ModDownload.DlQSLLoader.Start(IsForceRestart: true);
                ModDownload.DlOptiFabricLoader.Start(IsForceRestart: true);
                ModDownload.DlLabyModListLoader.Start(IsForceRestart: true);
                ItemInstall.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadMod:
            {
                ModComp.CompProjectCache.Clear();
                ModComp.CompFilesCache.Clear();
                if (ModMain.FrmDownloadMod is not null)
                {
                    ModMain.FrmDownloadMod.Content.Storage = new ModComp.CompProjectStorage();
                    ModMain.FrmDownloadMod.Content.Page = 0;
                    ModMain.FrmDownloadMod.PageLoaderRestart();
                }

                ItemMod.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadPack:
            {
                ModComp.CompProjectCache.Clear();
                ModComp.CompFilesCache.Clear();
                if (ModMain.FrmDownloadPack is not null)
                {
                    ModMain.FrmDownloadPack.Content.Storage = new ModComp.CompProjectStorage();
                    ModMain.FrmDownloadPack.Content.Page = 0;
                    ModMain.FrmDownloadPack.PageLoaderRestart();
                }

                ItemPack.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadDataPack:
            {
                ModComp.CompProjectCache.Clear();
                ModComp.CompFilesCache.Clear();
                if (ModMain.FrmDownloadDataPack is not null)
                {
                    ModMain.FrmDownloadDataPack.Content.Storage = new ModComp.CompProjectStorage();
                    ModMain.FrmDownloadDataPack.Content.Page = 0;
                    ModMain.FrmDownloadDataPack.PageLoaderRestart();
                }

                ItemDataPack.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadResourcePack:
            {
                ModComp.CompProjectCache.Clear();
                ModComp.CompFilesCache.Clear();
                if (ModMain.FrmDownloadResourcePack is not null)
                {
                    ModMain.FrmDownloadResourcePack.Content.Storage = new ModComp.CompProjectStorage();
                    ModMain.FrmDownloadResourcePack.Content.Page = 0;
                    ModMain.FrmDownloadResourcePack.PageLoaderRestart();
                }

                ItemResourcePack.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadShader:
            {
                ModComp.CompProjectCache.Clear();
                ModComp.CompFilesCache.Clear();
                if (ModMain.FrmDownloadShader is not null)
                {
                    ModMain.FrmDownloadShader.Content.Storage = new ModComp.CompProjectStorage();
                    ModMain.FrmDownloadShader.Content.Page = 0;
                    ModMain.FrmDownloadShader.PageLoaderRestart();
                }

                ItemShader.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadWorld:
            {
                ModComp.CompProjectCache.Clear();
                ModComp.CompFilesCache.Clear();
                if (ModMain.FrmDownloadWorld is not null)
                {
                    ModMain.FrmDownloadWorld.Content.Storage = new ModComp.CompProjectStorage();
                    ModMain.FrmDownloadWorld.Content.Page = 0;
                    ModMain.FrmDownloadWorld.PageLoaderRestart();
                }

                ItemWorld.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadClient:
            {
                ModDownload.DlClientListLoader.Start(IsForceRestart: true);
                ItemClient.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadOptiFine:
            {
                ModDownload.DlOptiFineListLoader.Start(IsForceRestart: true);
                ItemOptiFine.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadForge:
            {
                ModDownload.DlForgeListLoader.Start(IsForceRestart: true);
                ItemForge.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadNeoForge:
            {
                ModDownload.DlNeoForgeListLoader.Start(IsForceRestart: true);
                ItemNeoForge.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadCleanroom:
            {
                ModDownload.DlCleanroomListLoader.Start(IsForceRestart: true);
                ItemCleanroom.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadLiteLoader:
            {
                ModDownload.DlLiteLoaderListLoader.Start(IsForceRestart: true);
                ItemLiteLoader.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadFabric:
            {
                ModDownload.DlFabricListLoader.Start(IsForceRestart: true);
                ItemFabric.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadQuilt:
            {
                ModDownload.DlQuiltListLoader.Start(IsForceRestart: true);
                ItemQuilt.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadLabyMod:
            {
                ModDownload.DlLabyModListLoader.Start(IsForceRestart: true);
                ItemLabyMod.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadLegacyFabric:
            {
                ModDownload.DlLegacyFabricListLoader.Start(IsForceRestart: true);
                ItemLegacyFabric.Checked = true;
                break;
            }
            case FormMain.PageSubType.DownloadCompFavorites:
            {
                if (ModMain.FrmDownloadCompFavorites is not null)
                    ModMain.FrmDownloadCompFavorites.PageLoaderRestart();
                ItemFavorites.Checked = true;
                break;
            }
        }

        ModMain.Hint("正在刷新……", Log: false);
    }

    // 点击返回
    private void ItemInstall_Click(object sender, MouseButtonEventArgs e)
    {
        if (!ItemInstall.Checked)
            return;
        ModMain.FrmDownloadInstall.ExitSelectPage();
    }

    #region 页面切换

    /// <summary>
    ///     当前页面的编号。
    /// </summary>
    public FormMain.PageSubType PageID = FormMain.PageSubType.DownloadInstall;

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

    public object PageGet(FormMain.PageSubType ID)
    {
        if (ID == default)
            ID = PageID;
        switch (ID)
        {
            case FormMain.PageSubType.DownloadInstall:
            {
                if (ModMain.FrmDownloadInstall is null)
                    ModMain.FrmDownloadInstall = new PageDownloadInstall();
                return ModMain.FrmDownloadInstall;
            }
            case FormMain.PageSubType.DownloadMod:
            {
                if (ModMain.FrmDownloadMod is null)
                    ModMain.FrmDownloadMod = new PageDownloadMod();
                return ModMain.FrmDownloadMod;
            }
            case FormMain.PageSubType.DownloadPack:
            {
                if (ModMain.FrmDownloadPack is null)
                    ModMain.FrmDownloadPack = new PageDownloadPack();
                return ModMain.FrmDownloadPack;
            }
            case FormMain.PageSubType.DownloadDataPack:
            {
                if (ModMain.FrmDownloadDataPack is null)
                    ModMain.FrmDownloadDataPack = new PageDownloadDataPack();
                return ModMain.FrmDownloadDataPack;
            }
            case FormMain.PageSubType.DownloadResourcePack:
            {
                if (ModMain.FrmDownloadResourcePack is null)
                    ModMain.FrmDownloadResourcePack = new PageDownloadResourcePack();
                return ModMain.FrmDownloadResourcePack;
            }
            case FormMain.PageSubType.DownloadShader:
            {
                if (ModMain.FrmDownloadShader is null)
                    ModMain.FrmDownloadShader = new PageDownloadShader();
                return ModMain.FrmDownloadShader;
            }
            case FormMain.PageSubType.DownloadWorld:
            {
                if (ModMain.FrmDownloadWorld is null)
                    ModMain.FrmDownloadWorld = new PageDownloadWorld();
                return ModMain.FrmDownloadWorld;
            }
            case FormMain.PageSubType.DownloadCompFavorites:
            {
                if (ModMain.FrmDownloadCompFavorites is null)
                    ModMain.FrmDownloadCompFavorites = new PageDownloadCompFavorites();
                return ModMain.FrmDownloadCompFavorites;
            }
            case FormMain.PageSubType.DownloadClient:
            {
                if (ModMain.FrmDownloadClient is null)
                    ModMain.FrmDownloadClient = new PageDownloadClient();
                return ModMain.FrmDownloadClient;
            }
            case FormMain.PageSubType.DownloadOptiFine:
            {
                if (ModMain.FrmDownloadOptiFine is null)
                    ModMain.FrmDownloadOptiFine = new PageDownloadOptiFine();
                return ModMain.FrmDownloadOptiFine;
            }
            case FormMain.PageSubType.DownloadForge:
            {
                if (ModMain.FrmDownloadForge is null)
                    ModMain.FrmDownloadForge = new PageDownloadForge();
                return ModMain.FrmDownloadForge;
            }
            case FormMain.PageSubType.DownloadNeoForge:
            {
                if (ModMain.FrmDownloadNeoForge is null)
                    ModMain.FrmDownloadNeoForge = new PageDownloadNeoForge();
                return ModMain.FrmDownloadNeoForge;
            }
            case FormMain.PageSubType.DownloadCleanroom:
            {
                if (ModMain.FrmDownloadCleanroom is null)
                    ModMain.FrmDownloadCleanroom = new PageDownloadCleanroom();
                return ModMain.FrmDownloadCleanroom;
            }
            case FormMain.PageSubType.DownloadLiteLoader:
            {
                if (ModMain.FrmDownloadLiteLoader is null)
                    ModMain.FrmDownloadLiteLoader = new PageDownloadLiteLoader();
                return ModMain.FrmDownloadLiteLoader;
            }
            case FormMain.PageSubType.DownloadFabric:
            {
                if (ModMain.FrmDownloadFabric is null)
                    ModMain.FrmDownloadFabric = new PageDownloadFabric();
                return ModMain.FrmDownloadFabric;
            }
            case FormMain.PageSubType.DownloadQuilt:
            {
                if (ModMain.FrmDownloadQuilt is null)
                    ModMain.FrmDownloadQuilt = new PageDownloadQuilt();
                return ModMain.FrmDownloadQuilt;
            }
            case FormMain.PageSubType.DownloadLabyMod:
            {
                if (ModMain.FrmDownloadLabyMod is null)
                    ModMain.FrmDownloadLabyMod = new PageDownloadLabyMod();
                return ModMain.FrmDownloadLabyMod;
            }
            case FormMain.PageSubType.DownloadLegacyFabric:
            {
                if (ModMain.FrmDownloadLegacyFabric is null)
                    ModMain.FrmDownloadLegacyFabric = new PageDownloadLegacyFabric();
                return ModMain.FrmDownloadLegacyFabric;
            }

            default:
            {
                throw new Exception("未知的下载子页面种类：" + (int)ID);
            }
        }
    }

    /// <summary>
    ///     切换现有页面。
    /// </summary>
    public void PageChange(FormMain.PageSubType ID)
    {
        if (PageID == ID)
            return;
        ModAnimation.AniControlEnabled += 1;
        try
        {
            PageChangeRun((MyPageRight)PageGet(ID));
            PageID = ID;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "切换分页面失败（ID " + (int)ID + "）", ModBase.LogLevel.Feedback);
        }
        finally
        {
            ModAnimation.AniControlEnabled -= 1;
        }
    }

    private static void PageChangeRun(MyPageRight Target)
    {
        ModAnimation.AniStop("FrmMain PageChangeRight"); // 停止主页面的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
        if (Target.Parent is not null)
            Target.SetValue(ContentPresenter.ContentProperty, null);
        ModMain.FrmMain.PageRight = Target;
        ((MyPageRight)ModMain.FrmMain.PanMainRight.Child).PageOnExit();
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() =>
            {
                ((MyPageRight)ModMain.FrmMain.PanMainRight.Child).PageOnForceExit();
                ModMain.FrmMain.PanMainRight.Child = ModMain.FrmMain.PageRight;
                ModMain.FrmMain.PageRight.Opacity = 0d;
            }, 130),
            ModAnimation.AaCode(() =>
            {
                // 延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                ModMain.FrmMain.PageRight.Opacity = 1d;
                ModMain.FrmMain.PageRight.PageOnEnter();
            }, 30, true)
        }, "PageLeft PageChange");
    }

    #endregion
}
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageCommunityLeft : IRefreshable
{
    public void Refresh()
    {
        Refresh(ModMain.frmMain.pageCurrentSub);
    }

    public void RefreshButton_Click(object sender, EventArgs e)
    {
        Refresh((FormMain.PageSubType)ModBase.Val(((MyIconButton)sender).Tag));
    }

    public void Refresh(FormMain.PageSubType SubType)
    {
        switch (SubType)
        {
            case FormMain.PageSubType.DownloadMod:
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
            case FormMain.PageSubType.DownloadPack:
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
            case FormMain.PageSubType.DownloadDataPack:
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
            case FormMain.PageSubType.DownloadShader:
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
            case FormMain.PageSubType.DownloadResourcePack:
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
            case FormMain.PageSubType.DownloadWorld:
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
            case FormMain.PageSubType.DownloadCompFavorites:
                if (ModMain.frmDownloadCompFavorites is not null)
                    ModMain.frmDownloadCompFavorites.PageLoaderRestart();
                ItemFavorites.Checked = true;
                break;
            case FormMain.PageSubType.DownloadClient:
                ModDownload.dlClientListLoader.Start(isForceRestart: true);
                ItemClient.Checked = true;
                break;
            case FormMain.PageSubType.DownloadOptiFine:
                ModDownload.dlOptiFineListLoader.Start(isForceRestart: true);
                ItemOptiFine.Checked = true;
                break;
            case FormMain.PageSubType.DownloadForge:
                ModDownload.dlForgeListLoader.Start(isForceRestart: true);
                ItemForge.Checked = true;
                break;
            case FormMain.PageSubType.DownloadNeoForge:
                ModDownload.dlNeoForgeListLoader.Start(isForceRestart: true);
                ItemNeoForge.Checked = true;
                break;
            case FormMain.PageSubType.DownloadCleanroom:
                ModDownload.dlCleanroomListLoader.Start(isForceRestart: true);
                ItemCleanroom.Checked = true;
                break;
            case FormMain.PageSubType.DownloadLiteLoader:
                ModDownload.dlLiteLoaderListLoader.Start(isForceRestart: true);
                ItemLiteLoader.Checked = true;
                break;
            case FormMain.PageSubType.DownloadFabric:
                ModDownload.dlFabricListLoader.Start(isForceRestart: true);
                ItemFabric.Checked = true;
                break;
            case FormMain.PageSubType.DownloadQuilt:
                ModDownload.dlQuiltListLoader.Start(isForceRestart: true);
                ItemQuilt.Checked = true;
                break;
            case FormMain.PageSubType.DownloadLabyMod:
                ModDownload.dlLabyModListLoader.Start(isForceRestart: true);
                ItemLabyMod.Checked = true;
                break;
            case FormMain.PageSubType.DownloadLegacyFabric:
                ModDownload.dlLegacyFabricListLoader.Start(isForceRestart: true);
                ItemLegacyFabric.Checked = true;
                break;
        }
        ModMain.Hint(Lang.Text("Download.Left.Hint.Refreshing"), log: false);
    }

    public FormMain.PageSubType PageID = FormMain.PageSubType.DownloadMod;

    public PageCommunityLeft()
    {
        AnimatedControl = PanItem;
        InitializeComponent();
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
        ItemCleanroom.Check += PageCheck;
        ItemLiteLoader.Check += PageCheck;
        ItemFabric.Check += PageCheck;
        ItemQuilt.Check += PageCheck;
        ItemLabyMod.Check += PageCheck;
        ItemLegacyFabric.Check += PageCheck;
    }

    private void PageCheck(object sender, ModBase.RouteEventArgs e)
    {
        if (sender is MyListItem { Tag: { } tag })
            PageChange((FormMain.PageSubType)ModBase.Val(tag));
    }

    public object PageGet(FormMain.PageSubType ID)
    {
        if (ID == default) ID = PageID;
        switch (ID)
        {
            case FormMain.PageSubType.DownloadMod:
                ModMain.frmDownloadMod ??= new PageDownloadMod();
                return ModMain.frmDownloadMod;
            case FormMain.PageSubType.DownloadPack:
                ModMain.frmDownloadPack ??= new PageDownloadPack();
                return ModMain.frmDownloadPack;
            case FormMain.PageSubType.DownloadDataPack:
                ModMain.frmDownloadDataPack ??= new PageDownloadDataPack();
                return ModMain.frmDownloadDataPack;
            case FormMain.PageSubType.DownloadResourcePack:
                ModMain.frmDownloadResourcePack ??= new PageDownloadResourcePack();
                return ModMain.frmDownloadResourcePack;
            case FormMain.PageSubType.DownloadShader:
                ModMain.frmDownloadShader ??= new PageDownloadShader();
                return ModMain.frmDownloadShader;
            case FormMain.PageSubType.DownloadWorld:
                ModMain.frmDownloadWorld ??= new PageDownloadWorld();
                return ModMain.frmDownloadWorld;
            case FormMain.PageSubType.DownloadCompFavorites:
                ModMain.frmDownloadCompFavorites ??= new PageDownloadCompFavorites();
                return ModMain.frmDownloadCompFavorites;
            case FormMain.PageSubType.DownloadClient:
                ModMain.frmDownloadClient ??= new PageDownloadClient();
                return ModMain.frmDownloadClient;
            case FormMain.PageSubType.DownloadOptiFine:
                ModMain.frmDownloadOptiFine ??= new PageDownloadOptiFine();
                return ModMain.frmDownloadOptiFine;
            case FormMain.PageSubType.DownloadForge:
                ModMain.frmDownloadForge ??= new PageDownloadForge();
                return ModMain.frmDownloadForge;
            case FormMain.PageSubType.DownloadNeoForge:
                ModMain.frmDownloadNeoForge ??= new PageDownloadNeoForge();
                return ModMain.frmDownloadNeoForge;
            case FormMain.PageSubType.DownloadCleanroom:
                ModMain.frmDownloadCleanroom ??= new PageDownloadCleanroom();
                return ModMain.frmDownloadCleanroom;
            case FormMain.PageSubType.DownloadLiteLoader:
                ModMain.frmDownloadLiteLoader ??= new PageDownloadLiteLoader();
                return ModMain.frmDownloadLiteLoader;
            case FormMain.PageSubType.DownloadFabric:
                ModMain.frmDownloadFabric ??= new PageDownloadFabric();
                return ModMain.frmDownloadFabric;
            case FormMain.PageSubType.DownloadQuilt:
                ModMain.frmDownloadQuilt ??= new PageDownloadQuilt();
                return ModMain.frmDownloadQuilt;
            case FormMain.PageSubType.DownloadLabyMod:
                ModMain.frmDownloadLabyMod ??= new PageDownloadLabyMod();
                return ModMain.frmDownloadLabyMod;
            case FormMain.PageSubType.DownloadLegacyFabric:
                ModMain.frmDownloadLegacyFabric ??= new PageDownloadLegacyFabric();
                return ModMain.frmDownloadLegacyFabric;
            default:
                throw new Exception(Lang.Text("Download.Left.Error.UnknownSubPageType", (int)ID));
        }
    }

    public void PageChange(FormMain.PageSubType ID)
    {
        if (PageID == ID) return;
        ModAnimation.AniControlEnabled += 1;
        try
        {
            PageChangeRun((MyPageRight)PageGet(ID));
            PageID = ID;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "switch sub-page failed (ID " + (int)ID + ")", ModBase.LogLevel.Feedback);
        }
        finally { ModAnimation.AniControlEnabled -= 1; }
    }

    private static void PageChangeRun(MyPageRight Target)
    {
        ModAnimation.AniStop("FrmMain PageChangeRight");
        if (Target.Parent is not null) Target.SetValue(ContentPresenter.ContentProperty, null);
        ModMain.frmMain.pageRight = Target;
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

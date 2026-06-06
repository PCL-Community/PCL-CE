using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageCommunityLeft : IRefreshable
{
    public void Refresh()
    {
        Refresh(ModMain.FrmMain.PageCurrentSub);
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
            case FormMain.PageSubType.DownloadPack:
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
            case FormMain.PageSubType.DownloadDataPack:
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
            case FormMain.PageSubType.DownloadShader:
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
            case FormMain.PageSubType.DownloadResourcePack:
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
            case FormMain.PageSubType.DownloadWorld:
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
            case FormMain.PageSubType.DownloadCompFavorites:
                if (ModMain.FrmDownloadCompFavorites is not null)
                    ModMain.FrmDownloadCompFavorites.PageLoaderRestart();
                ItemFavorites.Checked = true;
                break;
            case FormMain.PageSubType.DownloadClient:
                ModDownload.DlClientListLoader.Start(IsForceRestart: true);
                ItemClient.Checked = true;
                break;
            case FormMain.PageSubType.DownloadOptiFine:
                ModDownload.DlOptiFineListLoader.Start(IsForceRestart: true);
                ItemOptiFine.Checked = true;
                break;
            case FormMain.PageSubType.DownloadForge:
                ModDownload.DlForgeListLoader.Start(IsForceRestart: true);
                ItemForge.Checked = true;
                break;
            case FormMain.PageSubType.DownloadNeoForge:
                ModDownload.DlNeoForgeListLoader.Start(IsForceRestart: true);
                ItemNeoForge.Checked = true;
                break;
            case FormMain.PageSubType.DownloadCleanroom:
                ModDownload.DlCleanroomListLoader.Start(IsForceRestart: true);
                ItemCleanroom.Checked = true;
                break;
            case FormMain.PageSubType.DownloadLiteLoader:
                ModDownload.DlLiteLoaderListLoader.Start(IsForceRestart: true);
                ItemLiteLoader.Checked = true;
                break;
            case FormMain.PageSubType.DownloadFabric:
                ModDownload.DlFabricListLoader.Start(IsForceRestart: true);
                ItemFabric.Checked = true;
                break;
            case FormMain.PageSubType.DownloadQuilt:
                ModDownload.DlQuiltListLoader.Start(IsForceRestart: true);
                ItemQuilt.Checked = true;
                break;
            case FormMain.PageSubType.DownloadLabyMod:
                ModDownload.DlLabyModListLoader.Start(IsForceRestart: true);
                ItemLabyMod.Checked = true;
                break;
            case FormMain.PageSubType.DownloadLegacyFabric:
                ModDownload.DlLegacyFabricListLoader.Start(IsForceRestart: true);
                ItemLegacyFabric.Checked = true;
                break;
        }
        ModMain.Hint(Lang.Text("Download.Left.Hint.Refreshing"), Log: false);
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
                ModMain.FrmDownloadMod ??= new PageDownloadMod();
                return ModMain.FrmDownloadMod;
            case FormMain.PageSubType.DownloadPack:
                ModMain.FrmDownloadPack ??= new PageDownloadPack();
                return ModMain.FrmDownloadPack;
            case FormMain.PageSubType.DownloadDataPack:
                ModMain.FrmDownloadDataPack ??= new PageDownloadDataPack();
                return ModMain.FrmDownloadDataPack;
            case FormMain.PageSubType.DownloadResourcePack:
                ModMain.FrmDownloadResourcePack ??= new PageDownloadResourcePack();
                return ModMain.FrmDownloadResourcePack;
            case FormMain.PageSubType.DownloadShader:
                ModMain.FrmDownloadShader ??= new PageDownloadShader();
                return ModMain.FrmDownloadShader;
            case FormMain.PageSubType.DownloadWorld:
                ModMain.FrmDownloadWorld ??= new PageDownloadWorld();
                return ModMain.FrmDownloadWorld;
            case FormMain.PageSubType.DownloadCompFavorites:
                ModMain.FrmDownloadCompFavorites ??= new PageDownloadCompFavorites();
                return ModMain.FrmDownloadCompFavorites;
            case FormMain.PageSubType.DownloadClient:
                ModMain.FrmDownloadClient ??= new PageDownloadClient();
                return ModMain.FrmDownloadClient;
            case FormMain.PageSubType.DownloadOptiFine:
                ModMain.FrmDownloadOptiFine ??= new PageDownloadOptiFine();
                return ModMain.FrmDownloadOptiFine;
            case FormMain.PageSubType.DownloadForge:
                ModMain.FrmDownloadForge ??= new PageDownloadForge();
                return ModMain.FrmDownloadForge;
            case FormMain.PageSubType.DownloadNeoForge:
                ModMain.FrmDownloadNeoForge ??= new PageDownloadNeoForge();
                return ModMain.FrmDownloadNeoForge;
            case FormMain.PageSubType.DownloadCleanroom:
                ModMain.FrmDownloadCleanroom ??= new PageDownloadCleanroom();
                return ModMain.FrmDownloadCleanroom;
            case FormMain.PageSubType.DownloadLiteLoader:
                ModMain.FrmDownloadLiteLoader ??= new PageDownloadLiteLoader();
                return ModMain.FrmDownloadLiteLoader;
            case FormMain.PageSubType.DownloadFabric:
                ModMain.FrmDownloadFabric ??= new PageDownloadFabric();
                return ModMain.FrmDownloadFabric;
            case FormMain.PageSubType.DownloadQuilt:
                ModMain.FrmDownloadQuilt ??= new PageDownloadQuilt();
                return ModMain.FrmDownloadQuilt;
            case FormMain.PageSubType.DownloadLabyMod:
                ModMain.FrmDownloadLabyMod ??= new PageDownloadLabyMod();
                return ModMain.FrmDownloadLabyMod;
            case FormMain.PageSubType.DownloadLegacyFabric:
                ModMain.FrmDownloadLegacyFabric ??= new PageDownloadLegacyFabric();
                return ModMain.FrmDownloadLegacyFabric;
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
                ModMain.FrmMain.PageRight.Opacity = 1d;
                ModMain.FrmMain.PageRight.PageOnEnter();
            }, 30, true)
        }, "PageLeft PageChange");
    }
}

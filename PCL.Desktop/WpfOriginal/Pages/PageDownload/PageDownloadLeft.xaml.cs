using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageDownloadLeft : IRefreshable
{
    public void Refresh()
    {
        Refresh(ModMain.frmMain.pageCurrentSub);
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
                break;
            }
        }

        ModMain.Hint(Lang.Text("Download.Left.Hint.Refreshing"), log: false);
    }

    // 点击返回
    private void ItemAll_Click(object sender, MouseButtonEventArgs e)
    {
        if (!ItemAll.Checked)
            return;
        ModMain.frmDownloadInstall.ExitSelectPage();
    }

    // 版本筛选回调
    public string VersionFilter { get; private set; } = "all";

    private void PageCheck(object sender, ModBase.RouteEventArgs e)
    {
        if (sender is MyListItem { Tag: { } tag })
        {
            var tagVal = tag.ToString();
            VersionFilter = tagVal switch
            {
                "0" => "all",
                "1" => "release",
                "2" => "snapshot",
                "3" => "beforerelease",
                "4" => "aprilfools",
                _ => "all"
            };
            ModMain.frmDownloadInstall?.ApplyVersionFilter(VersionFilter);
        }
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
        ItemAll.Check += PageCheck;
        ItemRelease.Check += PageCheck;
        ItemSnapshot.Check += PageCheck;
        ItemBeforeRelease.Check += PageCheck;
        ItemAprilFools.Check += PageCheck;
    }

    public object PageGet(FormMain.PageSubType ID)
    {
        if (ID == default)
            ID = pageID;
        switch (ID)
        {
            case FormMain.PageSubType.DownloadInstall:
            {
                if (ModMain.frmDownloadInstall is null)
                    ModMain.frmDownloadInstall = new PageDownloadInstall();
                return ModMain.frmDownloadInstall;
            }

            default:
            {
                throw new Exception(Lang.Text("Download.Left.Error.UnknownSubPageType", (int)ID));
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
            ModBase.Log(ex, "切换分页面失败（ID " + (int)id + "）", ModBase.LogLevel.Feedback);
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
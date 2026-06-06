using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App.Localization;

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
                ItemAll.Checked = true;
                break;
            }
        }

        ModMain.Hint(Lang.Text("Download.Left.Hint.Refreshing"), Log: false);
    }

    // 点击返回
    private void ItemAll_Click(object sender, MouseButtonEventArgs e)
    {
        if (!ItemAll.Checked)
            return;
        ModMain.FrmDownloadInstall.ExitSelectPage();
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
            ModMain.FrmDownloadInstall?.ApplyVersionFilter(VersionFilter);
        }
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
        ItemAll.Check += PageCheck;
        ItemRelease.Check += PageCheck;
        ItemSnapshot.Check += PageCheck;
        ItemBeforeRelease.Check += PageCheck;
        ItemAprilFools.Check += PageCheck;
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

            default:
            {
                throw new Exception(Lang.Text("Download.Left.Error.UnknownSubPageType", (int)ID));
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
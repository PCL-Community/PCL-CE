using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageInstanceSavesLeft : IRefreshable
{
    public static string currentSave;

    // 初始化
    private bool isLoad;

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (isLoad)
            return;
        isLoad = true;
    }

    private void BtnOpenFolder_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ModBase.OpenExplorer($@"{currentSave}\");
    }

    #region 龙猫牌 页面管理

    /// <summary>
    ///     当前页面的编号。从 0 开始计算。
    /// </summary>
    public FormMain.PageSubType pageID = FormMain.PageSubType.Default;

    public PageInstanceSavesLeft()
    {
        InitializeComponent();
        Loaded += Page_Loaded;
        ItemInfo.Check += PageCheck;
        ItemDatapack.Check += PageCheck;
        BtnOpenFolder.Click += BtnOpenFolder_Click;
    }

    /// <summary>
    ///     勾选事件改变页面。
    /// </summary>
    private void PageCheck(object sender, ModBase.RouteEventArgs e)
    {
        if (sender is MyListItem item && item.Tag is not null)
            PageChange((FormMain.PageSubType)ModBase.Val(item.Tag));
    }

    public object PageGet(FormMain.PageSubType id = FormMain.PageSubType.Default)
    {
        if ((int)id == -1)
            id = pageID;
        switch (id)
        {
            case FormMain.PageSubType.VersionSavesInfo:
            {
                if (ModMain.frmInstanceSavesInfo is null)
                    ModMain.frmInstanceSavesInfo = new PageInstanceSavesInfo();
                return ModMain.frmInstanceSavesInfo;
            }
            case FormMain.PageSubType.VersionSavesDatapack:
            {
                if (ModMain.frmInstanceSavesDatapack is null)
                    ModMain.frmInstanceSavesDatapack = new PageInstanceSavesDatapack();
                return ModMain.frmInstanceSavesDatapack;
            }

            default:
            {
                throw new Exception(Lang.Text("Instance.Saves.Left.UnknownSubPage", (int)id));
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
            ModBase.Log(ex, Lang.Text("Instance.Saves.Left.SwitchFailed", (int)id), ModBase.LogLevel.Feedback);
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

    public void RefreshButton_Click(object sender, EventArgs e) // 由边栏按钮匿名调用
    {
        Refresh((FormMain.PageSubType)ModBase.Val(((MyIconButton)sender).Tag));
    }

    public void Refresh()
    {
        Refresh(ModMain.frmMain.PageCurrentSub);
    }

    public void Refresh(FormMain.PageSubType subType)
    {
        switch (subType)
        {
            case FormMain.PageSubType.VersionSavesDatapack:
            {
                if (ModMain.frmInstanceSavesDatapack is null)
                    ModMain.frmInstanceSavesDatapack = new PageInstanceSavesDatapack();
                if (ItemDatapack.Checked)
                    ModMain.frmInstanceSavesDatapack.Refresh();
                else
                    ItemDatapack.Checked = true;

                break;
            }
        }

        HintService.Hint(Lang.Text("Instance.Saves.Left.Refreshing"));
    }

    #endregion
}
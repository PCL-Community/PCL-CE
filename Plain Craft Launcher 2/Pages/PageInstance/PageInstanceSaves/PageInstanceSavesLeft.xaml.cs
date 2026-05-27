using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PCL;

public partial class PageInstanceSavesLeft : IRefreshable
{
    public static string CurrentSave;

    // 初始化
    private bool IsLoad;

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (IsLoad)
            return;
        IsLoad = true;
    }

    private void BtnOpenFolder_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        ModBase.OpenExplorer($@"{CurrentSave}\");
    }

    #region 龙猫牌 页面管理

    /// <summary>
    ///     当前页面的编号。从 0 开始计算。
    /// </summary>
    public FormMain.PageSubType PageID = FormMain.PageSubType.Default;

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

    public object PageGet(FormMain.PageSubType ID = FormMain.PageSubType.Default)
    {
        if ((int)ID == -1)
            ID = PageID;
        switch (ID)
        {
            case FormMain.PageSubType.VersionSavesInfo:
            {
                if (ModMain.FrmInstanceSavesInfo is null)
                    ModMain.FrmInstanceSavesInfo = new PageInstanceSavesInfo();
                return ModMain.FrmInstanceSavesInfo;
            }
            case FormMain.PageSubType.VersionSavesDatapack:
            {
                if (ModMain.FrmInstanceSavesDatapack is null)
                    ModMain.FrmInstanceSavesDatapack = new PageInstanceSavesDatapack();
                return ModMain.FrmInstanceSavesDatapack;
            }

            default:
            {
                throw new Exception("未知的实例设置子页面种类：" + (int)ID);
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

    public void RefreshButton_Click(object sender, EventArgs e) // 由边栏按钮匿名调用
    {
        Refresh((FormMain.PageSubType)ModBase.Val(((MyIconButton)sender).Tag));
    }

    public void Refresh()
    {
        Refresh(ModMain.FrmMain.PageCurrentSub);
    }

    public void Refresh(FormMain.PageSubType SubType)
    {
        switch (SubType)
        {
            case FormMain.PageSubType.VersionSavesDatapack:
            {
                if (ModMain.FrmInstanceSavesDatapack is null)
                    ModMain.FrmInstanceSavesDatapack = new PageInstanceSavesDatapack();
                if (ItemDatapack.Checked)
                    ModMain.FrmInstanceSavesDatapack.Refresh();
                else
                    ItemDatapack.Checked = true;

                break;
            }
        }

        ModMain.Hint("刷新中……");
    }

    #endregion
}
using System.Windows;
using System.Windows.Controls;
using PCL.Core.App;

namespace PCL;

public partial class PageToolsLeft
{
    private bool IsLoad;
    private bool IsPageSwitched; // 如果在 Loaded 前切换到其他页面，会导致触发 Loaded 时再次切换一次

    public PageToolsLeft()
    {
        InitializeComponent();
        AnimatedControl = PanItem;
        Loaded += PageLinkLeft_Loaded;
        Unloaded += PageOtherLeft_Unloaded;
    }

    private void PageLinkLeft_Loaded(object sender, RoutedEventArgs e)
    {
        var IsHiddenPage = false;
        var hide = Config.Preference.Hide;

        if (ItemGameLink.Checked && hide.ToolsGameLink) IsHiddenPage = true;
        if (ItemTest.Checked && hide.ToolsTest) IsHiddenPage = true;
        if (ItemLauncherHelp.Checked && hide.ToolsHelp) IsHiddenPage = true;
        if (PageSetupUI.HiddenForceShow)
            IsHiddenPage = false;
        // 若页面错误，或尚未加载，则继续
        if (IsLoad && !IsHiddenPage)
            return;
        IsLoad = true;
        // 刷新子页面隐藏情况
        PageSetupUI.HiddenRefresh();
        // 选择第一个未被禁用的子页面
        if (IsPageSwitched) 
            return;
        var hideCfg = Config.Preference.Hide;
        if (!hideCfg.ToolsGameLink)
            ItemGameLink.SetChecked(true, false, false);
        else if (!hideCfg.ToolsTest)
            ItemTest.SetChecked(true, false, false);
        else if (!hideCfg.ToolsHelp)
            ItemLauncherHelp.SetChecked(true, false, false);
        else
            ItemGameLink.SetChecked(true, false, false);
    }

    private void PageOtherLeft_Unloaded(object sender, RoutedEventArgs e)
    {
        IsPageSwitched = false;
    }

    public void Refresh(object sender, EventArgs e)
    {
        var button = (MyIconButton)sender;
        if (button.Tag is null)
            return;
        double id = ModBase.Val(button.Tag);
        switch (id)
        {
            case (double)FormMain.PageSubType.ToolsGameLink:
            {
                if (ModMain.FrmToolsGameLink is null)
                    ModMain.FrmToolsGameLink = new PageToolsGameLink();
                ModMain.FrmToolsGameLink.Reload();
                ItemGameLink.Checked = true;
                break;
            }
            case (double)FormMain.PageSubType.ToolsLauncherHelp:
            {
                if (ModMain.FrmToolsHelp is null)
                    ModMain.FrmToolsHelp = new PageToolsHelp();
                ModMain.FrmToolsHelp.Refresh();
                ItemLauncherHelp.Checked = true;
                break;
            }
        }
    }

    public static void RefreshHelp()
    {
        ModMain.FrmToolsHelp.PageLoaderRestart();
        ModMain.FrmToolsHelp.SearchBox.Text = "";
    }

    #region 页面切换

    /// <summary>
    ///     当前页面的编号。
    /// </summary>
    public FormMain.PageSubType PageID = FormMain.PageSubType.ToolsGameLink;

    /// <summary>
    ///     勾选事件改变页面。
    /// </summary>
    private void PageCheck(object senderRaw, ModBase.RouteEventArgs e)
    {
        var sender = (MyListItem)senderRaw;
        // 尚未初始化控件属性时，sender.Tag 为 Nothing，会导致切换到页面 0
        // 若使用 IsLoaded，则会导致模拟点击不被执行（模拟点击切换页面时，控件的 IsLoaded 为 False）
        if (sender.Tag is not null)
            PageChange((FormMain.PageSubType)ModBase.Val(sender.Tag));
    }

    public object PageGet(FormMain.PageSubType? ID = null)
    {
        var targetID = ID ?? PageID;
        switch (ID)
        {
            case FormMain.PageSubType.ToolsGameLink:
            {
                if (ModMain.FrmToolsGameLink is null)
                    ModMain.FrmToolsGameLink = new PageToolsGameLink();
                return ModMain.FrmToolsGameLink;
            }
            case FormMain.PageSubType.ToolsTest:
            {
                if (ModMain.FrmToolsTest is null)
                    ModMain.FrmToolsTest = new PageToolsTest();
                return ModMain.FrmToolsTest;
            }
            case FormMain.PageSubType.ToolsLauncherHelp:
            {
                if (ModMain.FrmToolsHelp is null)
                    ModMain.FrmToolsHelp = new PageToolsHelp();
                return ModMain.FrmToolsHelp;
            }

            default:
            {
                throw new Exception("未知的更多子页面种类：" + (int)ID);
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
        IsPageSwitched = true;
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
using System.Windows;
using System.Windows.Controls;
using PCL.Core.App;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageSetupLeft
{
    private bool isLoad;
    private bool isPageSwitched; // 如果在 Loaded 前切换到其他页面，会导致触发 Loaded 时再次切换一次

    private void PageSetupLeft_Loaded(object sender, RoutedEventArgs e)
    {
        // 是否处于隐藏的子页面
        var isHiddenPage = false;
        var hide = Config.Preference.Hide;

        if (ItemLaunch.Checked && hide.SetupLaunch) isHiddenPage = true;
        if (ItemJava.Checked && hide.SetupJava) isHiddenPage = true;
        if (ItemGameManage.Checked && hide.SetupGameManage)  isHiddenPage = true;
        if (ItemGameLink.Checked && hide.SetupGameLink) isHiddenPage = true;
        if (ItemUI.Checked && hide.SetupUi) isHiddenPage = true;
        if (ItemLauncherLanguage.Checked && hide.SetupLauncherLanguage) isHiddenPage = true;
        if (ItemLauncherMisc.Checked && hide.SetupLauncherMisc) isHiddenPage = true;
        if (ItemAbout.Checked && hide.SetupAbout) isHiddenPage = true;
        if (ItemUpdate.Checked && hide.SetupUpdate) isHiddenPage = true;
        if (ItemFeedback.Checked && hide.SetupFeedback) isHiddenPage = true;
        if (ItemLog.Checked && hide.SetupLog) isHiddenPage = true;
        if (PageSetupUI.HiddenForceShow)
            isHiddenPage = false;
        // 若页面错误，或尚未加载，则继续
        if (isLoad && !isHiddenPage)
            return;
        isLoad = true;
        // 刷新子页面隐藏情况
        PageSetupUI.HiddenRefresh();
        // 选择第一个未被禁用的子页面
        if (isPageSwitched)
            return;
        var hideCfg = Config.Preference.Hide;
        if (!hideCfg.SetupLaunch) 
            ItemLaunch.SetChecked(true, false, false);
        else if (!hideCfg.SetupJava) 
            ItemJava.SetChecked(true, false, false);    
        else if (!hideCfg.SetupGameManage) 
            ItemGameManage.SetChecked(true, false, false);
        else if (!hideCfg.SetupGameLink) 
            ItemGameLink.SetChecked(true, false, false);    
        else if (!hideCfg.SetupUi) 
            ItemUI.SetChecked(true, false, false);
        else if (!hideCfg.SetupLauncherLanguage)
            ItemLauncherLanguage.SetChecked(true, false, false);
        else if (!hideCfg.SetupLauncherMisc) 
            ItemLauncherMisc.SetChecked(true, false, false);
        else if (!hideCfg.SetupAbout) 
            ItemAbout.SetChecked(true, false, false);   
        else if (!hideCfg.SetupUpdate) 
            ItemUpdate.SetChecked(true, false, false);
        else if (!hideCfg.SetupFeedback) 
            ItemFeedback.SetChecked(true, false, false);
        else if (!hideCfg.SetupLog) 
            ItemLog.SetChecked(true, false, false);
        else 
            ItemLaunch.SetChecked(true, false, false);
    }

    private void PageOtherLeft_Unloaded(object sender, RoutedEventArgs e)
    {
        isPageSwitched = false;
    }

    public void Reset(object sender, EventArgs e)
    {
        switch (ModBase.Val(((MyIconButton)sender).Tag))
        {
            case (double)FormMain.PageSubType.SetupLaunch:
            {
                if (ModMain.MyMsgBox(Lang.Text("Setup.Left.Reset.Launch.Message"), Lang.Text("Setup.Left.Reset.Title"), button2: Lang.Text("Common.Action.Cancel"), isWarn: true) == 1)
                {
                    if (ModMain.frmSetupLaunch is null)
                        ModMain.frmSetupLaunch = new PageSetupLaunch();
                    ModMain.frmSetupLaunch.Reset();
                    ItemLaunch.Checked = true;
                }

                break;
            }
            case (double)FormMain.PageSubType.SetupUI:
            {
                if (ModMain.MyMsgBox(Lang.Text("Setup.Left.Reset.Ui.Message"),
                        Lang.Text("Setup.Left.Reset.Title"), button2: Lang.Text("Common.Action.Cancel"), isWarn: true) == 1)
                {
                    if (ModMain.frmSetupUI is null)
                        ModMain.frmSetupUI = new PageSetupUI();
                    ModMain.frmSetupUI.Reset();
                    ItemUI.Checked = true;
                }

                break;
            }
            case (double)FormMain.PageSubType.SetupGameManage:
            {
                if (ModMain.MyMsgBox(Lang.Text("Setup.Left.Reset.GameManage.Message"), Lang.Text("Setup.Left.Reset.Title"), button2: Lang.Text("Common.Action.Cancel"), isWarn: true) == 1)
                {
                    if (ModMain.frmSetupGameManage is null)
                        ModMain.frmSetupGameManage = new PageSetupGameManage();
                    ModMain.frmSetupGameManage.Reset();
                    ItemGameManage.Checked = true;
                }

                break;
            }
            case (double)FormMain.PageSubType.SetupGameLink:
            {
                if (ModMain.MyMsgBox(Lang.Text("Setup.Left.Reset.GameLink.Message"), Lang.Text("Setup.Left.Reset.Title"), button2: Lang.Text("Common.Action.Cancel"), isWarn: true) == 1)
                {
                    if (ModMain.frmSetupGameLink is null)
                        ModMain.frmSetupGameLink = new PageSetupGameLink();
                    ModMain.frmSetupGameLink.Reset();
                    ItemGameLink.Checked = true;
                }

                break;
            }
            case (double)FormMain.PageSubType.SetupLauncherLanguage:
            {
                if (ModMain.MyMsgBox(Lang.Text("Setup.Left.Reset.Language.Message"), Lang.Text("Setup.Left.Reset.Title"), button2: Lang.Text("Common.Action.Cancel"), isWarn: true) == 1)
                {
                    if (ModMain.frmSetupLauncherLanguage is null)
                        ModMain.frmSetupLauncherLanguage = new PageSetupLauncherLanguage();
                    ModMain.frmSetupLauncherLanguage.Reset();
                    ItemLauncherLanguage.Checked = true;
                }

                break;
            }
            case (double)FormMain.PageSubType.SetupLauncherMisc:
            {
                if (ModMain.MyMsgBox(Lang.Text("Setup.Left.Reset.Misc.Message"), Lang.Text("Setup.Left.Reset.Title"), button2: Lang.Text("Common.Action.Cancel"), isWarn: true) == 1)
                {
                    if (ModMain.frmSetupLauncherMisc is null)
                        ModMain.frmSetupLauncherMisc = new PageSetupLauncherMisc();
                    ModMain.frmSetupLauncherMisc.Reset();
                    ItemLauncherMisc.Checked = true;
                }

                break;
            }
        }
    }

    public static void TryFeedback() // Handles ItemFeedback.Click
    {
        ModBase.RunInNewThread(() =>
        {
            if (!ModBase.CanFeedback(true))
                return;
            switch (ModMain.MyMsgBox(Lang.Text("Setup.Left.Feedback.Message"), Lang.Text("Setup.Left.Feedback.Title"),
                        Lang.Text("Setup.Left.Feedback.SubmitNew"), Lang.Text("Setup.Left.Feedback.ViewList"), Lang.Text("Common.Action.Cancel")))
            {
                case 1:
                {
                    ModBase.Feedback();
                    break;
                }
                case 2:
                {
                    ModBase.OpenWebsite("https://github.com/PCL-Community/PCL2-CE/issues/");
                    break;
                }
            }
        });
    }

    public void Refresh(object sender, EventArgs e) // 由边栏按钮匿名调用
    {
        switch (ModBase.Val(((MyIconButton)sender).Tag))
        {
            case (double)FormMain.PageSubType.SetupFeedback:
            {
                if (ModMain.frmSetupFeedback is not null) ModMain.frmSetupFeedback.Loader.Start(isForceRestart: true);
                ItemFeedback.Checked = true;
                break;
            }
            case (double)FormMain.PageSubType.SetupJava:
            {
                if (ModMain.frmSetupJava is not null) ModMain.frmSetupJava.loader.Start(isForceRestart: true);
                ItemJava.Checked = true;
                break;
            }
        }

        HintService.Hint(Lang.Text("Setup.Left.Refreshing"), log: false);
    }

    #region 页面切换

    /// <summary>
    ///     当前页面的编号。从左往右从 0 开始计算。
    /// </summary>
    public FormMain.PageSubType pageID;

    public PageSetupLeft()
    {
        InitializeComponent();
        // 选择第一个未被禁用的子页面
        var hideCfg = Config.Preference.Hide;
        if (!hideCfg.SetupLaunch)
            pageID = FormMain.PageSubType.SetupLaunch;
        else if (!hideCfg.SetupJava)
            pageID = FormMain.PageSubType.SetupJava;
        else if (!hideCfg.SetupGameManage)
            pageID = FormMain.PageSubType.SetupGameManage;
        else if (!hideCfg.SetupGameLink)
            pageID = FormMain.PageSubType.SetupGameLink;    
        else if (!hideCfg.SetupUi)
            pageID = FormMain.PageSubType.SetupUI;
        else if (!hideCfg.SetupLauncherLanguage)
            pageID = FormMain.PageSubType.SetupLauncherLanguage;
        else if (!hideCfg.SetupLauncherMisc)
            pageID = FormMain.PageSubType.SetupLauncherMisc;
        else if (!hideCfg.SetupAbout)
            pageID = FormMain.PageSubType.SetupAbout;        
        else if (!hideCfg.SetupUpdate)
            pageID = FormMain.PageSubType.SetupUpdate;
        else if (!hideCfg.SetupFeedback)
            pageID = FormMain.PageSubType.SetupFeedback;
        else if (!hideCfg.SetupLog)
            pageID = FormMain.PageSubType.SetupLog;
        else
            pageID = FormMain.PageSubType.SetupLaunch;
        AnimatedControl = PanItem;
        Loaded += PageSetupLeft_Loaded;
        Unloaded += PageOtherLeft_Unloaded;
    }

    /// <summary>
    ///     勾选事件改变页面。
    /// </summary>
    private void PageCheck(object senderRaw, ModBase.RouteEventArgs e)
    {
        var sender = (MyListItem)senderRaw;
        // 尚未初始化控件属性时，sender.Tag 为 Nothing，会跳过切换，且由于 PageID 默认为 0 而切换到第一个页面
        // 若使用 IsLoaded，则会导致模拟点击不被执行（模拟点击切换页面时，控件的 IsLoaded 为 False）
        if (sender.Tag is not null)
            PageChange((FormMain.PageSubType)ModBase.Val(sender.Tag));
    }

    /// <summary>
    ///     获取当前导航指定的右页面。
    /// </summary>
    public object PageGet(FormMain.PageSubType? id = null)
    {
        var targetID = id ?? pageID;
        switch (id)
        {
            case FormMain.PageSubType.SetupLaunch:
            {
                if (ModMain.frmSetupLaunch is null)
                    ModMain.frmSetupLaunch = new PageSetupLaunch();
                return ModMain.frmSetupLaunch;
            }
            case FormMain.PageSubType.SetupUI:
            {
                if (ModMain.frmSetupUI is null)
                    ModMain.frmSetupUI = new PageSetupUI();
                return ModMain.frmSetupUI;
            }
            case FormMain.PageSubType.SetupGameManage:
            {
                if (ModMain.frmSetupGameManage is null)
                    ModMain.frmSetupGameManage = new PageSetupGameManage();
                return ModMain.frmSetupGameManage;
            }
            case FormMain.PageSubType.SetupUpdate:
            {
                if (ModMain.frmSetupUpdate is null)
                    ModMain.frmSetupUpdate = new PageSetupUpdate();
                return ModMain.frmSetupUpdate;
            }
            case FormMain.PageSubType.SetupAbout:
            {
                if (ModMain.frmSetupAbout is null)
                    ModMain.frmSetupAbout = new PageSetupAbout();
                return ModMain.frmSetupAbout;
            }
            case FormMain.PageSubType.SetupLog:
            {
                if (ModMain.frmSetupLog is null)
                    ModMain.frmSetupLog = new PageSetupLog();
                return ModMain.frmSetupLog;
            }
            case FormMain.PageSubType.SetupFeedback:
            {
                if (ModMain.frmSetupFeedback is null)
                    ModMain.frmSetupFeedback = new PageSetupFeedback();
                return ModMain.frmSetupFeedback;
            }
            case FormMain.PageSubType.SetupGameLink:
            {
                if (ModMain.frmSetupGameLink is null)
                    ModMain.frmSetupGameLink = new PageSetupGameLink();
                return ModMain.frmSetupGameLink;
            }
            case FormMain.PageSubType.SetupLauncherLanguage:
            {
                if (ModMain.frmSetupLauncherLanguage is null)
                    ModMain.frmSetupLauncherLanguage = new PageSetupLauncherLanguage();
                return ModMain.frmSetupLauncherLanguage;
            }
            case FormMain.PageSubType.SetupLauncherMisc:
            {
                if (ModMain.frmSetupLauncherMisc is null)
                    ModMain.frmSetupLauncherMisc = new PageSetupLauncherMisc();
                return ModMain.frmSetupLauncherMisc;
            }
            case FormMain.PageSubType.SetupJava:
            {
                if (ModMain.frmSetupJava is null)
                    ModMain.frmSetupJava = new PageSetupJava();
                return ModMain.frmSetupJava;
            }

            default:
            {
                throw new Exception("未知的设置子页面种类：" + (int)id);
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
        isPageSwitched = true;
        try
        {
            PageChangeRun((MyPageRight)PageGet(id));
            pageID = id;
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                $"切换分页面失败（ID {(int)id}）",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
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

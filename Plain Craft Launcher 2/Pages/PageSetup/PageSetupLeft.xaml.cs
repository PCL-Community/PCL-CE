using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic;
using PCL.Core.App;
using PCL.Core.App.Localization;

namespace PCL;

public partial class PageSetupLeft
{
    private bool IsLoad;
    private bool IsPageSwitched; // 如果在 Loaded 前切换到其他页面，会导致触发 Loaded 时再次切换一次

    private void PageSetupLeft_Loaded(object sender, RoutedEventArgs e)
    {
        // 是否处于隐藏的子页面
        var IsHiddenPage = false;
        var hide = Config.Preference.Hide;

        if (ItemLaunch.Checked && hide.SetupLaunch) IsHiddenPage = true;
        if (ItemJava.Checked && hide.SetupJava) IsHiddenPage = true;
        if (ItemGameManage.Checked && hide.SetupGameManage)  IsHiddenPage = true;
        if (ItemGameLink.Checked && hide.SetupGameLink) IsHiddenPage = true;
        if (ItemUI.Checked && hide.SetupUi) IsHiddenPage = true;
        if (ItemLauncherLanguage.Checked && hide.SetupLauncherLanguage) IsHiddenPage = true;
        if (ItemLauncherMisc.Checked && hide.SetupLauncherMisc) IsHiddenPage = true;
        if (ItemAbout.Checked && hide.SetupAbout) IsHiddenPage = true;
        if (ItemUpdate.Checked && hide.SetupUpdate) IsHiddenPage = true;
        if (ItemFeedback.Checked && hide.SetupFeedback) IsHiddenPage = true;
        if (ItemLog.Checked && hide.SetupLog) IsHiddenPage = true;
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
        IsPageSwitched = false;
    }

    public void Reset(object sender, EventArgs e)
    {
        switch (ModBase.Val(((MyIconButton)sender).Tag))
        {
            case (double)FormMain.PageSubType.SetupLaunch:
            {
                if (ModMain.MyMsgBox("是否要初始化 游戏-启动 页面的所有设置？该操作不可撤销。", "初始化确认", Button2: Lang.Text("Common.Action.Cancel"), IsWarn: true) == 1)
                {
                    if (ModMain.FrmSetupLaunch is null)
                        ModMain.FrmSetupLaunch = new PageSetupLaunch();
                    ModMain.FrmSetupLaunch.Reset();
                    ItemLaunch.Checked = true;
                }

                break;
            }
            case (double)FormMain.PageSubType.SetupUI:
            {
                if (ModMain.MyMsgBox("""
                                     是否要初始化 启动器-个性化 页面的所有设置？该操作不可撤销。
                                     （背景图片与音乐、主页等外部文件不会被删除）
                                     """,
                        "初始化确认", Button2: Lang.Text("Common.Action.Cancel"), IsWarn: true) == 1)
                {
                    if (ModMain.FrmSetupUI is null)
                        ModMain.FrmSetupUI = new PageSetupUI();
                    ModMain.FrmSetupUI.Reset();
                    ItemUI.Checked = true;
                }

                break;
            }
            case (double)FormMain.PageSubType.SetupGameManage:
            {
                if (ModMain.MyMsgBox("是否要初始化 游戏-管理 页面的所有设置？该操作不可撤销。", "初始化确认", Button2: Lang.Text("Common.Action.Cancel"), IsWarn: true) == 1)
                {
                    if (ModMain.FrmSetupGameManage is null)
                        ModMain.FrmSetupGameManage = new PageSetupGameManage();
                    ModMain.FrmSetupGameManage.Reset();
                    ItemGameManage.Checked = true;
                }

                break;
            }
            case (double)FormMain.PageSubType.SetupGameLink:
            {
                if (ModMain.MyMsgBox("是否要初始化 工具-联机 页面的所有设置？该操作不可撤销。", "初始化确认", Button2: Lang.Text("Common.Action.Cancel"), IsWarn: true) == 1)
                {
                    if (ModMain.FrmSetupGameLink is null)
                        ModMain.FrmSetupGameLink = new PageSetupGameLink();
                    ModMain.FrmSetupGameLink.Reset();
                    ItemGameLink.Checked = true;
                }

                break;
            }
            case (double)FormMain.PageSubType.SetupLauncherLanguage:
            {
                if (ModMain.MyMsgBox("是否要初始化 启动器-语言 页面的所有设置？该操作不可撤销。", "初始化确认", Button2: Lang.Text("Common.Action.Cancel"), IsWarn: true) == 1)
                {
                    if (ModMain.FrmSetupLauncherLanguage is null)
                        ModMain.FrmSetupLauncherLanguage = new PageSetupLauncherLanguage();
                    ModMain.FrmSetupLauncherLanguage.Reset();
                    ItemLauncherLanguage.Checked = true;
                }

                break;
            }
            case (double)FormMain.PageSubType.SetupLauncherMisc:
            {
                if (ModMain.MyMsgBox("是否要初始化 启动器-杂项 页面的所有设置？该操作不可撤销。", "初始化确认", Button2: Lang.Text("Common.Action.Cancel"), IsWarn: true) == 1)
                {
                    if (ModMain.FrmSetupLauncherMisc is null)
                        ModMain.FrmSetupLauncherMisc = new PageSetupLauncherMisc();
                    ModMain.FrmSetupLauncherMisc.Reset();
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
            switch (ModMain.MyMsgBox("""
                                     在提交新反馈前，建议先搜索反馈列表，以避免重复提交。
                                     如果无法打开该网页，请尝试使用加速器或 VPN。
                                     """, "反馈",
                        "提交新反馈", "查看反馈列表", Lang.Text("Common.Action.Cancel")))
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
                if (ModMain.FrmSetupFeedback is not null) ModMain.FrmSetupFeedback.Loader.Start(IsForceRestart: true);
                ItemFeedback.Checked = true;
                break;
            }
            case (double)FormMain.PageSubType.SetupJava:
            {
                if (ModMain.FrmSetupJava is not null) ModMain.FrmSetupJava.Loader.Start(IsForceRestart: true);
                ItemJava.Checked = true;
                break;
            }
        }

        ModMain.Hint("正在刷新……", Log: false);
    }

    #region 页面切换

    /// <summary>
    ///     当前页面的编号。从左往右从 0 开始计算。
    /// </summary>
    public FormMain.PageSubType PageID;

    public PageSetupLeft()
    {
        InitializeComponent();
        // 选择第一个未被禁用的子页面
        var hideCfg = Config.Preference.Hide;
        if (!hideCfg.SetupLaunch)
            PageID = FormMain.PageSubType.SetupLaunch;
        else if (!hideCfg.SetupJava)
            PageID = FormMain.PageSubType.SetupJava;
        else if (!hideCfg.SetupGameManage)
            PageID = FormMain.PageSubType.SetupGameManage;
        else if (!hideCfg.SetupGameLink)
            PageID = FormMain.PageSubType.SetupGameLink;    
        else if (!hideCfg.SetupUi)
            PageID = FormMain.PageSubType.SetupUI;
        else if (!hideCfg.SetupLauncherLanguage)
            PageID = FormMain.PageSubType.SetupLauncherLanguage;
        else if (!hideCfg.SetupLauncherMisc)
            PageID = FormMain.PageSubType.SetupLauncherMisc;
        else if (!hideCfg.SetupAbout)
            PageID = FormMain.PageSubType.SetupAbout;        
        else if (!hideCfg.SetupUpdate)
            PageID = FormMain.PageSubType.SetupUpdate;
        else if (!hideCfg.SetupFeedback)
            PageID = FormMain.PageSubType.SetupFeedback;
        else if (!hideCfg.SetupLog)
            PageID = FormMain.PageSubType.SetupLog;
        else
            PageID = FormMain.PageSubType.SetupLaunch;
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
    public object PageGet(FormMain.PageSubType? ID = null)
    {
        var targetID = ID ?? PageID;
        switch (ID)
        {
            case FormMain.PageSubType.SetupLaunch:
            {
                if (ModMain.FrmSetupLaunch is null)
                    ModMain.FrmSetupLaunch = new PageSetupLaunch();
                return ModMain.FrmSetupLaunch;
            }
            case FormMain.PageSubType.SetupUI:
            {
                if (ModMain.FrmSetupUI is null)
                    ModMain.FrmSetupUI = new PageSetupUI();
                return ModMain.FrmSetupUI;
            }
            case FormMain.PageSubType.SetupGameManage:
            {
                if (ModMain.FrmSetupGameManage is null)
                    ModMain.FrmSetupGameManage = new PageSetupGameManage();
                return ModMain.FrmSetupGameManage;
            }
            case FormMain.PageSubType.SetupUpdate:
            {
                if (ModMain.FrmSetupUpdate is null)
                    ModMain.FrmSetupUpdate = new PageSetupUpdate();
                return ModMain.FrmSetupUpdate;
            }
            case FormMain.PageSubType.SetupAbout:
            {
                if (ModMain.FrmSetupAbout is null)
                    ModMain.FrmSetupAbout = new PageSetupAbout();
                return ModMain.FrmSetupAbout;
            }
            case FormMain.PageSubType.SetupLog:
            {
                if (ModMain.FrmSetupLog is null)
                    ModMain.FrmSetupLog = new PageSetupLog();
                return ModMain.FrmSetupLog;
            }
            case FormMain.PageSubType.SetupFeedback:
            {
                if (ModMain.FrmSetupFeedback is null)
                    ModMain.FrmSetupFeedback = new PageSetupFeedback();
                return ModMain.FrmSetupFeedback;
            }
            case FormMain.PageSubType.SetupGameLink:
            {
                if (ModMain.FrmSetupGameLink is null)
                    ModMain.FrmSetupGameLink = new PageSetupGameLink();
                return ModMain.FrmSetupGameLink;
            }
            case FormMain.PageSubType.SetupLauncherLanguage:
            {
                if (ModMain.FrmSetupLauncherLanguage is null)
                    ModMain.FrmSetupLauncherLanguage = new PageSetupLauncherLanguage();
                return ModMain.FrmSetupLauncherLanguage;
            }
            case FormMain.PageSubType.SetupLauncherMisc:
            {
                if (ModMain.FrmSetupLauncherMisc is null)
                    ModMain.FrmSetupLauncherMisc = new PageSetupLauncherMisc();
                return ModMain.FrmSetupLauncherMisc;
            }
            case FormMain.PageSubType.SetupJava:
            {
                if (ModMain.FrmSetupJava is null)
                    ModMain.FrmSetupJava = new PageSetupJava();
                return ModMain.FrmSetupJava;
            }

            default:
            {
                throw new Exception("未知的设置子页面种类：" + (int)ID);
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
            ModBase.Log(ex, $"切换分页面失败（ID {(int)ID}）", ModBase.LogLevel.Feedback);
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

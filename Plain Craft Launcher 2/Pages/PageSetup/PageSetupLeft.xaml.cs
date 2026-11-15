using System.Windows;
using System.Windows.Controls;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Hints;
using PCL.Core.UI.Dialogs;

namespace PCL;

public partial class PageSetupLeft : IPageView
{
    private readonly PageSetupLeftViewModel _vm = new();
    private bool isLoad;
    private bool isPageSwitched; // 如果在 Loaded 前切换到其他页面，会导致触发 Loaded 时再次切换一次

    private void PageSetupLeft_Loaded(object sender, RoutedEventArgs e) => RefreshNavigation();

    public void OnNavigatedTo(object? parameter) => RefreshNavigation();

    public void OnNavigatedFrom()
    {
    }

    private void RefreshNavigation()
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

    public async void Reset(object sender, EventArgs e)
    {
        var ctl = sender as Control;
        if (ctl is not null)
            ctl.IsEnabled = false;
        var aniHeld = false;
        try
        {
            var id = (FormMain.PageSubType)Common.Val(((MyIconButton)sender).Tag);
            if (!await _vm.ConfirmResetAsync(id))
                return;
            Animations.AniBeginControl();
            aniHeld = true;
            switch (id)
            {
                case FormMain.PageSubType.SetupLaunch:
                {
                    if (Launcher.frmSetupLaunch is null)
                        Launcher.frmSetupLaunch = new PageSetupLaunch();
                    Launcher.frmSetupLaunch.Reset();
                    ItemLaunch.Checked = true;
                    break;
                }
                case FormMain.PageSubType.SetupUI:
                {
                    if (Launcher.frmSetupUi is null)
                        Launcher.frmSetupUi = new PageSetupUI();
                    Launcher.frmSetupUi.Reset();
                    ItemUI.Checked = true;
                    break;
                }
                case FormMain.PageSubType.SetupGameManage:
                {
                    if (Launcher.frmSetupGameManage is null)
                        Launcher.frmSetupGameManage = new PageSetupGameManage();
                    Launcher.frmSetupGameManage.Reset();
                    ItemGameManage.Checked = true;
                    break;
                }
                case FormMain.PageSubType.SetupGameLink:
                {
                    if (Launcher.frmSetupGameLink is null)
                        Launcher.frmSetupGameLink = new PageSetupGameLink();
                    Launcher.frmSetupGameLink.Reset();
                    ItemGameLink.Checked = true;
                    break;
                }
                case FormMain.PageSubType.SetupLauncherLanguage:
                {
                    if (Launcher.frmSetupLauncherLanguage is null)
                        Launcher.frmSetupLauncherLanguage = new PageSetupLauncherLanguage();
                    Launcher.frmSetupLauncherLanguage.Reset();
                    ItemLauncherLanguage.Checked = true;
                    break;
                }
                case FormMain.PageSubType.SetupLauncherMisc:
                {
                    if (Launcher.frmSetupLauncherMisc is null)
                        Launcher.frmSetupLauncherMisc = new PageSetupLauncherMisc();
                    Launcher.frmSetupLauncherMisc.Reset();
                    ItemLauncherMisc.Checked = true;
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            Common.Log(ex, "重置设置子页面失败", Common.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
        finally
        {
            if (aniHeld)
                Animations.AniEndControl();
            if (ctl is not null)
                ctl.IsEnabled = true;
        }
    }

    public static void TryFeedback() // Handles ItemFeedback.Click
    {
        Common.RunInNewThread(() =>
        {
            if (!Common.CanFeedback(true))
                return;
            switch ((DialogService.Instance.Show(new TextDialogRequest
            {
                Title = Lang.Text("Setup.Left.Feedback.Title"),
                Content = Lang.Text("Setup.Left.Feedback.Message"),
                Buttons = DialogButtons.Create("Setup.Left.Feedback.SubmitNew", "Setup.Left.Feedback.ViewList", "Common.Action.Cancel"),
                ForceWait = true
            }).Result?.Button ?? 0))
            {
                case 1:
                {
                    Common.Feedback();
                    break;
                }
                case 2:
                {
                    Common.OpenWebsite("https://github.com/PCL-Community/PCL2-CE/issues/");
                    break;
                }
            }
        });
    }

    public void Refresh(object sender, EventArgs e) // 由边栏按钮匿名调用
    {
        switch (Common.Val(((MyIconButton)sender).Tag))
        {
            case (double)FormMain.PageSubType.SetupFeedback:
            {
                if (Launcher.frmSetupFeedback is not null) Launcher.frmSetupFeedback.Loader.Start(isForceRestart: true);
                ItemFeedback.Checked = true;
                break;
            }
            case (double)FormMain.PageSubType.SetupJava:
            {
                if (Launcher.frmSetupJava is not null) Launcher.frmSetupJava.loader.Start(isForceRestart: true);
                ItemJava.Checked = true;
                break;
            }
        }

        HintService.Show(Hints.Info(Lang.Text("Setup.Left.Refreshing"), log: false));
    }

    #region 页面切换

    /// <summary>
    /// 当前页面的编号。从左往右从 0 开始计算。
    /// </summary>
    public FormMain.PageSubType pageID
    {
        get => _vm.PageID;
        set => _vm.PageID = value;
    }

    public PageSetupLeft()
    {
        InitializeComponent();
        AnimatedControl = PanItem;
        Loaded += PageSetupLeft_Loaded;
        Unloaded += PageOtherLeft_Unloaded;
    }

    /// <summary>
    /// 勾选事件改变页面。
    /// </summary>
    private void PageCheck(object senderRaw, Common.RouteEventArgs e)
    {
        var sender = (MyListItem)senderRaw;
        // 尚未初始化控件属性时，sender.Tag 为 Nothing，会跳过切换，且由于 PageID 默认为 0 而切换到第一个页面
        // 若使用 IsLoaded，则会导致模拟点击不被执行（模拟点击切换页面时，控件的 IsLoaded 为 False）
        if (sender.Tag is not null)
            PageChange((FormMain.PageSubType)Common.Val(sender.Tag));
    }

    /// <summary>
    /// 获取当前导航指定的右页面。
    /// </summary>
    public object PageGet(FormMain.PageSubType? id = null)
    {
        var targetID = id ?? pageID;
        switch (id)
        {
            case FormMain.PageSubType.SetupLaunch:
            {
                if (Launcher.frmSetupLaunch is null)
                    Launcher.frmSetupLaunch = new PageSetupLaunch();
                return Launcher.frmSetupLaunch;
            }
            case FormMain.PageSubType.SetupUI:
            {
                if (Launcher.frmSetupUi is null)
                    Launcher.frmSetupUi = new PageSetupUI();
                return Launcher.frmSetupUi;
            }
            case FormMain.PageSubType.SetupGameManage:
            {
                if (Launcher.frmSetupGameManage is null)
                    Launcher.frmSetupGameManage = new PageSetupGameManage();
                return Launcher.frmSetupGameManage;
            }
            case FormMain.PageSubType.SetupUpdate:
            {
                if (Launcher.frmSetupUpdate is null)
                    Launcher.frmSetupUpdate = new PageSetupUpdate();
                return Launcher.frmSetupUpdate;
            }
            case FormMain.PageSubType.SetupAbout:
            {
                if (Launcher.frmSetupAbout is null)
                    Launcher.frmSetupAbout = new PageSetupAbout();
                return Launcher.frmSetupAbout;
            }
            case FormMain.PageSubType.SetupLog:
            {
                if (Launcher.frmSetupLog is null)
                    Launcher.frmSetupLog = new PageSetupLog();
                return Launcher.frmSetupLog;
            }
            case FormMain.PageSubType.SetupFeedback:
            {
                if (Launcher.frmSetupFeedback is null)
                    Launcher.frmSetupFeedback = new PageSetupFeedback();
                return Launcher.frmSetupFeedback;
            }
            case FormMain.PageSubType.SetupGameLink:
            {
                if (Launcher.frmSetupGameLink is null)
                    Launcher.frmSetupGameLink = new PageSetupGameLink();
                return Launcher.frmSetupGameLink;
            }
            case FormMain.PageSubType.SetupLauncherLanguage:
            {
                if (Launcher.frmSetupLauncherLanguage is null)
                    Launcher.frmSetupLauncherLanguage = new PageSetupLauncherLanguage();
                return Launcher.frmSetupLauncherLanguage;
            }
            case FormMain.PageSubType.SetupLauncherMisc:
            {
                if (Launcher.frmSetupLauncherMisc is null)
                    Launcher.frmSetupLauncherMisc = new PageSetupLauncherMisc();
                return Launcher.frmSetupLauncherMisc;
            }
            case FormMain.PageSubType.SetupJava:
            {
                if (Launcher.frmSetupJava is null)
                    Launcher.frmSetupJava = new PageSetupJava();
                return Launcher.frmSetupJava;
            }

            default:
            {
                throw new Exception("未知的设置子页面种类：" + (int)id);
            }
        }
    }

    /// <summary>
    /// 切换现有页面。
    /// </summary>
    public void PageChange(FormMain.PageSubType id)
    {
        if (pageID == id)
            return;
        Animations.AniBeginControl();
        isPageSwitched = true;
        try
        {
            PageChangeRun((MyPageRight)PageGet(id));
            pageID = id;
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                $"切换分页面失败（ID {(int)id}）",
                Common.LogLevel.Feedback,
                userSummary: Lang.Text("Setup.Error.OperationFailed"));
        }
        finally
        {
            Animations.AniEndControl();
        }
    }

    private static void PageChangeRun(MyPageRight target)
    {
        Animations.AniStop("FrmMain PageChangeRight"); // 停止主页面的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
        if (target.Parent is not null)
            target.SetValue(ContentPresenter.ContentProperty, null);
        Launcher.frmMain.pageRight = target;
        ((MyPageRight)Launcher.frmMain.PanMainRight.Child).PageOnExit();
        Animations.AniStart(new[]
        {
            Animations.AaCode(() =>
            {
                ((MyPageRight)Launcher.frmMain.PanMainRight.Child).PageOnForceExit();
                Launcher.frmMain.PanMainRight.Child = Launcher.frmMain.pageRight;
                Launcher.frmMain.pageRight.Opacity = 0d;
            }, 130),
            Animations.AaCode(() =>
            {
                // 延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                Launcher.frmMain.pageRight.Opacity = 1d;
                Launcher.frmMain.pageRight.PageOnEnter();
            }, 30, true)
        }, "PageLeft PageChange");
    }

    #endregion
}

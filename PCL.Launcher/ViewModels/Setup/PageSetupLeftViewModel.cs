using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.ViewModel;
using PCL.Navigation;

namespace PCL;

/// <summary>设置页面左侧导航栏的视图模型。</summary>
public class PageSetupLeftViewModel : ViewModelBase
{
    public PageSubType PageId { get; set; } = FirstVisible();

    /// <summary>当前设置页可用的子页数量；少于 2 项时 WPF 会隐藏整个设置侧栏。</summary>
    public static int CountVisiblePages(bool forceShow)
    {
        if (forceShow) return 11;
        var hide = Config.Preference.Hide;
        var count = 0;
        if (!hide.SetupLaunch) count++;
        if (!hide.SetupJava) count++;
        if (!hide.SetupGameManage) count++;
        if (!hide.SetupGameLink) count++;
        if (!hide.SetupUi) count++;
        if (!hide.SetupLauncherLanguage) count++;
        if (!hide.SetupLauncherMisc) count++;
        if (!hide.SetupAbout) count++;
        if (!hide.SetupUpdate) count++;
        if (!hide.SetupFeedback) count++;
        if (!hide.SetupLog) count++;
        return count;
    }

    public static PageSubType FirstVisible()
    {
        var hide = Config.Preference.Hide;
        if (!hide.SetupLaunch) return PageSubType.SetupLaunch;
        if (!hide.SetupJava) return PageSubType.SetupJava;
        if (!hide.SetupGameManage) return PageSubType.SetupGameManage;
        if (!hide.SetupGameLink) return PageSubType.SetupGameLink;
        if (!hide.SetupUi) return PageSubType.SetupUi;
        if (!hide.SetupLauncherLanguage) return PageSubType.SetupLauncherLanguage;
        if (!hide.SetupLauncherMisc) return PageSubType.SetupLauncherMisc;
        if (!hide.SetupAbout) return PageSubType.SetupAbout;
        if (!hide.SetupUpdate) return PageSubType.SetupUpdate;
        if (!hide.SetupFeedback) return PageSubType.SetupFeedback;
        if (!hide.SetupLog) return PageSubType.SetupLog;
        return PageSubType.SetupLaunch;
    }

    public static bool IsSubPageHidden(PageSubType sub)
    {
        var hide = Config.Preference.Hide;
        return sub switch
        {
            PageSubType.SetupLaunch => hide.SetupLaunch,
            PageSubType.SetupJava => hide.SetupJava,
            PageSubType.SetupGameManage => hide.SetupGameManage,
            PageSubType.SetupGameLink => hide.SetupGameLink,
            PageSubType.SetupUi => hide.SetupUi,
            PageSubType.SetupLauncherLanguage => hide.SetupLauncherLanguage,
            PageSubType.SetupLauncherMisc => hide.SetupLauncherMisc,
            PageSubType.SetupAbout => hide.SetupAbout,
            PageSubType.SetupUpdate => hide.SetupUpdate,
            PageSubType.SetupFeedback => hide.SetupFeedback,
            PageSubType.SetupLog => hide.SetupLog,
            _ => false
        };
    }

    public async Task<bool> ConfirmResetAsync(PageSubType id)
    {
        var messageKey = id switch
        {
            PageSubType.SetupLaunch => "Setup.Left.Reset.Launch.Message",
            PageSubType.SetupUi => "Setup.Left.Reset.Ui.Message",
            PageSubType.SetupGameManage => "Setup.Left.Reset.GameManage.Message",
            PageSubType.SetupGameLink => "Setup.Left.Reset.GameLink.Message",
            PageSubType.SetupLauncherLanguage => "Setup.Left.Reset.Language.Message",
            PageSubType.SetupLauncherMisc => "Setup.Left.Reset.Misc.Message",
            _ => null
        };
        if (messageKey is null) return false;
        var result = await Dialog.ShowAsync(
            new TextDialogRequest
            {
                Title = Lang.Text("Setup.Left.Reset.Title"),
                Content = Lang.Text(messageKey),
                Buttons = DialogButtons.OkCancel,
                IsWarn = true
            });
        return result.IsConfirmed;
    }
}
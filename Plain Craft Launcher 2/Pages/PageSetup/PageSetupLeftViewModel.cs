using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.UI.Dialogs;
using PCL.Core.ViewModel;

namespace PCL;

/// <summary>设置页面左侧导航栏的视图模型。</summary>
public partial class PageSetupLeftViewModel : ViewModelBase
{
    public PageSetupLeftViewModel()
    {
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
    }

    public FormMain.PageSubType PageID { get; set; }

    public async Task<bool> ConfirmResetAsync(FormMain.PageSubType id)
    {
        var messageKey = id switch
        {
            FormMain.PageSubType.SetupLaunch => "Setup.Left.Reset.Launch.Message",
            FormMain.PageSubType.SetupUI => "Setup.Left.Reset.Ui.Message",
            FormMain.PageSubType.SetupGameManage => "Setup.Left.Reset.GameManage.Message",
            FormMain.PageSubType.SetupGameLink => "Setup.Left.Reset.GameLink.Message",
            FormMain.PageSubType.SetupLauncherLanguage => "Setup.Left.Reset.Language.Message",
            FormMain.PageSubType.SetupLauncherMisc => "Setup.Left.Reset.Misc.Message",
            _ => null
        };
        if (messageKey is null)
            return false;
        var result = (await Dialog.ShowAsync(new TextDialogRequest
        {
            Title = Lang.Text("Setup.Left.Reset.Title"),
            Content = Lang.Text(messageKey),
            Buttons = DialogButtons.OkCancel,
            IsWarn = true
        })).Button;
        return result == 1;
    }
}

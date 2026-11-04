using Avalonia.Controls;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Launch;

/// <summary>
///     离线档案创建视图：填写玩家名并选择 UUID 标准，创建后由启动页左栏切换到档案皮肤视图。
/// </summary>
public partial class PageLoginOffline : LoginPageBase
{
    private enum UuidMode
    {
        Standard,
        Legacy,
        Custom
    }

    private readonly MyButton _btnBack;
    private readonly MyButton _btnLogin;
    private readonly MyRadioBox _radioUuidCustom;
    private readonly MyRadioBox _radioUuidLegacy;
    private readonly MyRadioBox _radioUuidStandard;
    private readonly MyTextBox _textName;
    private readonly MyTextBox _textUuid;
    private readonly TextBlock _textUuidTitle;

    private UuidMode _mode = UuidMode.Standard;
    private bool _syncingMode;

    public PageLoginOffline()
    {
        InitializeComponent();
        _textName = this.FindControl<MyTextBox>("TextName")!;
        _textUuid = this.FindControl<MyTextBox>("TextUuid")!;
        _textUuidTitle = this.FindControl<TextBlock>("TextUuidTitle")!;
        _radioUuidStandard = this.FindControl<MyRadioBox>("RadioUuidStandard")!;
        _radioUuidLegacy = this.FindControl<MyRadioBox>("RadioUuidLegacy")!;
        _radioUuidCustom = this.FindControl<MyRadioBox>("RadioUuidCustom")!;
        _btnBack = this.FindControl<MyButton>("BtnBack")!;
        _btnLogin = this.FindControl<MyButton>("BtnLogin")!;

        _radioUuidStandard.Checked = true;
        _radioUuidStandard.Check += (_, _) => SetMode(UuidMode.Standard);
        _radioUuidLegacy.Check += (_, _) => SetMode(UuidMode.Legacy);
        _radioUuidCustom.Check += (_, _) => SetMode(UuidMode.Custom);
        _btnBack.Click += (_, _) => RequestBack();
        _btnLogin.Click += (_, _) => _ = LoginAsync();
    }

    public override void Reload()
    {
        SetMode(_mode);
    }

    private void SetMode(UuidMode mode)
    {
        if (_syncingMode)
            return;
        _syncingMode = true;
        try
        {
            _mode = mode;
            _radioUuidStandard.Checked = mode == UuidMode.Standard;
            _radioUuidLegacy.Checked = mode == UuidMode.Legacy;
            _radioUuidCustom.Checked = mode == UuidMode.Custom;
            var showCustomUuid = mode == UuidMode.Custom;
            _textUuidTitle.IsVisible = showCustomUuid;
            _textUuid.IsVisible = showCustomUuid;
        }
        finally
        {
            _syncingMode = false;
        }
    }

    private async Task LoginAsync()
    {
        try
        {
            var username = _textName.Text ?? "";
            if (!new PCL.Core.Utils.Validate.RegexValidator("^[A-z0-9_]{3,16}$")
                    .Validate(username).IsValid)
            {
                var result = await DialogService.Instance.ShowAsync(new TextDialogRequest
                {
                    Title = Lang.Text("Launch.Account.Offline.InvalidPlayerId.Title"),
                    Content = Lang.Text("Launch.Account.Offline.InvalidPlayerId.Message"),
                    Buttons = DialogButtons.ContinueCancel,
                    IsWarn = true
                });
                if (result.Button == 2)
                    return;
            }

            string userUuid;
            if (_mode == UuidMode.Custom)
            {
                var uuidInput = (_textUuid.Text ?? "").Replace("-", "");
                var uuidValidateResult = new PCL.Core.Utils.Validate.RegexValidator("^[a-fA-F0-9]{32}$")
                    .Validate(uuidInput);
                if (!uuidValidateResult.IsValid)
                {
                    HintService.Show(Hints.Error(Lang.Text("Launch.Account.Offline.InvalidUuid", uuidValidateResult)));
                    return;
                }

                userUuid = uuidInput;
            }
            else if (_mode == UuidMode.Legacy)
            {
                userUuid = LaunchLoginHelper.GetOfflineUuid(username, isLegacy: true);
            }
            else
            {
                userUuid = LaunchLoginHelper.GetOfflineUuid(username);
            }

            ProfileService.Add(new McProfile
            {
                ProfileType = ProfileType.Offline,
                Uuid = userUuid,
                UserName = username,
                Description = "",
                AccessToken = userUuid,
                ClientToken = userUuid,
                ProfileId = Guid.NewGuid().ToString("N")
            });
            ProfileService.IsCreatingProfile = false;
            HintService.Show(Hints.Success(Lang.Text("Launch.Account.Profile.Created")));
            RequestRefresh();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Launch.Account.Profile.Error.Write"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Launch.Account.Profile.Error.Write"));
        }
    }
}
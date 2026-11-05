using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.UI.Dialogs;
using PCL.Core.UI.Hints;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Launch;

/// <summary>
///     登录档案列表：选择已有档案、删除或复制 UUID，并通过“新建档案”选择验证方式。
///     档案数据来自 <see cref="ProfileService" />；选择档案后由启动页左栏切换到档案皮肤视图。
/// </summary>
public partial class PageLoginProfile : LoginPageBase
{
    private readonly MyIconButton _btnNew;
    private readonly MyHint _hintCreate;
    private readonly MyHint _hintMicrosoft;
    private readonly MyHint _hintSelect;
    private readonly StackPanel _panProfiles;

    public PageLoginProfile()
    {
        InitializeComponent();
        _panProfiles = this.FindControl<StackPanel>("PanProfiles")!;
        _hintCreate = this.FindControl<MyHint>("HintCreate")!;
        _hintMicrosoft = this.FindControl<MyHint>("HintMicrosoft")!;
        _hintSelect = this.FindControl<MyHint>("HintSelect")!;
        _btnNew = this.FindControl<MyIconButton>("BtnNew")!;
        _btnNew.Click += (_, _) => CreateProfile();
        AttachedToVisualTree += (_, _) => LocalizationService.LanguageChanged += Reload;
        DetachedFromVisualTree += (_, _) => LocalizationService.LanguageChanged -= Reload;
    }

    public override void Reload()
    {
        _ = ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        List<McProfile> profiles;
        try
        {
            profiles = await Task.Run(() =>
            {
                ProfileService.Load();
                return ProfileService.Profiles.ToList();
            });
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Launch.Account.Profile.Error.Read"),
                LauncherLogLevel.Feedback,
                userSummary: Lang.Text("Launch.Account.Profile.Error.Read"));
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => BuildList(profiles));
    }

    private void BuildList(List<McProfile> profiles)
    {
        _panProfiles.Children.Clear();
        var isEmpty = profiles.Count == 0;
        _hintSelect.IsVisible = !isEmpty;
        _hintCreate.IsVisible = isEmpty;
        _hintMicrosoft.IsVisible = isEmpty;
        if (isEmpty)
            States.Hint.LaunchWithProfile = true;

        foreach (var profile in profiles)
            _panProfiles.Children.Add(CreateProfileItem(profile));
    }

    private Control CreateProfileItem(McProfile profile)
    {
        var item = new MyListItem
        {
            Title = profile.UserName,
            Info = LaunchLoginHelper.GetProfileInfo(profile),
            Type = MyListItem.CheckType.Clickable,
            tag = profile,
            Margin = new Thickness(8, 2, 10, 2),
            SvgIcon = "lucide/user"
        };

        var headPath = GetHeadCachePath(profile);
        if (headPath is not null)
        {
            item.Logo = headPath;
            item.SvgIcon = "";
        }

        var buttons = new List<MyIconButton>();

        if (profile.ProfileType == ProfileType.Offline)
            AddButton(
                "lucide/pencil",
                "Launch.Account.Profile.ChangeUuid",
                () => LaunchLoginHelper.EditOfflineUuid(profile));
        else
            AddButton("lucide/copy", "Launch.Account.Profile.CopyUuid", () => CopyUuid(profile));

        AddButton(
            "lucide/trash-2",
            "Launch.Account.Profile.Delete",
            () => _ = DeleteProfileAsync(profile));

        item.Buttons = buttons;
        item.Click += (_, _) => SelectProfile(profile);
        return item;

        void AddButton(string svgIcon, string toolTipKey, Action action)
        {
            var button = new MyIconButton
            {
                Height = 22,
                Width = 22,
                SvgIcon = svgIcon,
                Margin = new Thickness(4, 0, 0, 0)
            };
            ToolTip.SetTip(button, Lang.Text(toolTipKey));
            button.Click += (_, _) => action();
            buttons.Add(button);
        }
    }

    private static string? GetHeadCachePath(McProfile profile)
    {
        if (string.IsNullOrEmpty(profile.SkinHeadId))
            return null;
        var path = Path.Combine(
            LauncherPaths.Temp,
            "Cache",
            "Skin",
            "Head",
            profile.SkinHeadId + ".png");
        var file = new FileInfo(path);
        return file.Exists && file.Length > 0L ? path : null;
    }

    private void SelectProfile(McProfile profile)
    {
        ProfileService.Select(profile);
        ProfileService.IsCreatingProfile = false;
        LauncherCommon.Log($"[Profile] 选定档案: {profile.UserName}, 以 {profile.ProfileType} 方式验证");
        GameLaunch.mcLoginMsLoader.State = LoadState.Waiting;
        GameLaunch.mcLoginAuthLoader.State = LoadState.Waiting;
        GameLaunch.mcLoginLegacyLoader.State = LoadState.Waiting;
        RequestRefresh();
    }

    private void CopyUuid(McProfile profile)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;
        _ = clipboard.SetTextAsync(profile.Uuid);
        HintService.Show(Hints.Success(Lang.Text("Launch.Account.Profile.Uuid.Hint")));
    }

    private async Task DeleteProfileAsync(McProfile profile)
    {
        try
        {
            var result = await DialogService.Instance.ShowAsync(
                new TextDialogRequest
                {
                    Title = Lang.Text("Launch.Account.Profile.DeleteConfirm.Title"),
                    Content = Lang.Text("Launch.Account.Profile.DeleteConfirm.Message"),
                    Buttons = DialogButtons.ContinueCancel,
                    IsWarn = true
                });
            if (result.Button == 2)
                return;
            ProfileService.Remove(profile);
            HintService.Show(Hints.Success(Lang.Text("Launch.Account.Profile.Deleted")));
            Reload();
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "删除档案失败", LauncherLogLevel.Feedback);
        }
    }

    private void CreateProfile()
    {
        _ = CreateProfileAsync();
    }

    private async Task CreateProfileAsync()
    {
        try
        {
            var options = new List<DialogOption>
            {
                new()
                {
                    Text = Lang.Text("Launch.Account.Type.Microsoft")
                },
                new()
                {
                    Text = Lang.Text("Launch.Account.Type.ThirdParty")
                },
                new()
                {
                    Text = Lang.Text("Launch.Account.Type.Offline")
                }
            };
            var result = await DialogService.Instance.ShowAsync(
                new SelectDialogRequest
                {
                    Title = Lang.Text("Launch.Account.Profile.Create.SelectAuthType.Title"),
                    Options = options,
                    Buttons = DialogButtons.From(
                        DialogButton.Create(Lang.Text("Common.Action.Continue")),
                        DialogButton.Create(Lang.Text("Common.Action.Cancel"))),
                    HighLight = false
                });
            if (!result.IsConfirmed)
                return;

            var type = result.SelectedIndex switch
            {
                0 => GameLaunch.McLoginType.Ms,
                1 => GameLaunch.McLoginType.Auth,
                _ => GameLaunch.McLoginType.Legacy
            };
            ProfileService.IsCreatingProfile = true;
            RequestLoginType(type);
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(ex, "选择验证方式失败", LauncherLogLevel.Feedback);
        }
    }
}